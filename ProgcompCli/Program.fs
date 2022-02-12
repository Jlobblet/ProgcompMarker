open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Text.Json
open FSharpPlus
open FSharpPlus.Data
open FsToolkit.ErrorHandling
open Common
open FsToolkit.ErrorHandling.Operator.Result
open ProgcompCli.Args

let getInputs (endpoint: UriBuilder) (httpClient: HttpClient) settings =
    // If this function fails it can just crash the program because we need input data
    // So no asyncResult here
    let inputsResponse =
        endpoint.Path <- $"/inputs/%u{settings.ProblemNumber}"
        httpClient.GetAsync(endpoint.Uri).Result

    if not inputsResponse.IsSuccessStatusCode then
        eprintfn $"Error reading data from server: %O{inputsResponse}"
        exit 1

    let response =
        JsonSerializer.Deserialize<InputResponse>(inputsResponse.Content.ReadAsStream())

    printfn "Retrieved input data"

    response

let getProcessStartInfo settings data =
    let info = ProcessStartInfo()
    info.FileName <- settings.ExecutablePath
    info.RedirectStandardInput <- true
    info.RedirectStandardOutput <- true

    settings.ExecutableArgs
    |> Array.iter info.ArgumentList.Add

    if settings.InputMode = InputAsArgs then
        // Send data after a -- if the executable has been given arguments
        if not <| Array.isEmpty settings.ExecutableArgs then
            info.ArgumentList.Add "--"

        Array.iter info.ArgumentList.Add data

    info

type RunSolutionError =
    | ProcessStartError of exn
    | ExitCode of int

let runSolution (data: string []) settings (info: ProcessStartInfo) =
    result {
        printfn "Running solution..."

        let! proc =
            info
            |> Result.protect Process.Start
            |> Result.mapError ProcessStartError

        if settings.InputMode = InputToStdIn then
            data |> Array.iter proc.StandardInput.WriteLine

            proc.StandardInput.Close()

        proc.WaitForExit()

        if proc.ExitCode <> 0 then
            eprintfn $"Process exited with nonzero exit code %i{proc.ExitCode}"
            return! Error(ExitCode proc.ExitCode)
        else

            printfn "Complete!"

            return
                proc
                    .StandardOutput
                    .ReadToEnd()
                    .Split(Environment.NewLine)
                |> Array.filter (not << String.IsNullOrWhiteSpace)
    }

type SendResultsError =
    | PostError of exn
    | ResponseError of HttpStatusCode
    | JsonError of exn

let sendResults (endpoint: UriBuilder) (httpClient: HttpClient) (inputs: InputResponse) settings answers =
    asyncResult {
        let req =
            { Id = inputs.Id
              User = settings.Username
              Data = answers }

        let content =
            new StringContent(JsonSerializer.Serialize req, Encoding.UTF8, "application/json")

        printfn "Sending solutions to server..."

        let! responseTask =
            endpoint.Path <- $"/mark/%u{inputs.Id}"

            (endpoint.Uri, content)
            |> Result.protect httpClient.PostAsync
            |> Result.mapError PostError

        let! markResponse = responseTask

        if not markResponse.IsSuccessStatusCode then
            eprintfn $"Error reading data from server: %s{markResponse.ToString()}"
            return! Error(ResponseError markResponse.StatusCode)

        return!
            markResponse.Content.ReadAsStream()
            |> Result.protect JsonSerializer.Deserialize<MarkResponse>
            |> Result.mapError JsonError
    }

type RunAndMarkError =
    | RunSolutionError of RunSolutionError
    | SendResultsError of SendResultsError

let runAndMark endpoint httpClient settings info (inputs: InputResponse) =
    let handleExn =
        function
        | Ok () -> ()
        | Error e -> printfn $"%A{e}"

    asyncResult {
        let! answers =
            runSolution inputs.Data settings info
            |> Result.mapError RunSolutionError

        let! score =
            sendResults endpoint httpClient inputs settings answers
            |>> (Result.mapError SendResultsError)

        printfn $"Score: %i{score.Score}/%i{score.MaxScore}"
    }
    |> Async.RunSynchronously
    |> handleExn

let fileWatcherMode endpoint httpClient settings info inputs =
    printfn "Submitting results when file changes"

    let onChanged _ (args: FileSystemEventArgs) =
        if args.ChangeType = WatcherChangeTypes.Changed then
            printfn $"{DateTime.Now}: File changed, submitting automatically..."
            runAndMark endpoint httpClient settings info inputs

    let onDeleted _ _ =
        printfn "File deleted, exiting"
        exit 0

    let dir =
        Path.GetFullPath settings.ExecutablePath
        |> Path.GetDirectoryName

    let sol = Path.GetFileName settings.ExecutablePath
    use watcher = new FileSystemWatcher(dir, sol)
    watcher.NotifyFilter <- NotifyFilters.LastWrite
    watcher.IncludeSubdirectories <- false
    watcher.Changed.AddHandler onChanged
    watcher.Deleted.AddHandler onDeleted
    watcher.EnableRaisingEvents <- true
    printfn "Press enter at any point to exit"
    Console.ReadLine() |> ignore<string>

[<EntryPoint>]
let main argv =
    if not <| File.Exists ConfigFile then
        Settings.firstTimeSetup ()

    let settings = Settings.fromArgv argv

    let executable = settings.ExecutablePath

    if not <| File.Exists executable then
        eprintfn $"Could not find %s{executable} to run."
        exit 1

    let endpoint =
#if DEBUG
        UriBuilder "http://127.0.0.1:8080/"
#else
        settings.Endpoint
#endif

    use httpClient = new HttpClient()

    let inputs = getInputs endpoint httpClient settings

    let info = getProcessStartInfo settings inputs.Data

    if settings.SubmissionMode = RunOnce then
        runAndMark endpoint httpClient settings info inputs
    elif settings.SubmissionMode = FileWatcherRepeat then
        // Run once manually
        runAndMark endpoint httpClient settings info inputs
        // Run the rest automatically
        fileWatcherMode endpoint httpClient settings info inputs

    0
