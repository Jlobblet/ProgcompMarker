module ProgcompCli.Run

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open Common
open FSharpPlus
open FsToolkit.ErrorHandling
open Settings

let getProcessStartInfo settings data =
    let info =
        ProcessStartInfo(
            FileName = settings.ExecutablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        )

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

type TaskResult =
    | Write
    | Read of string

let writeAndRead settings (proc: Process) (data: string []) =
    let write =
        if settings.InputMode = InputToStdIn then
            task {
                for d in data do
                    do! proc.StandardInput.WriteLineAsync d

                return Write
            }
        else
            Task.FromResult Write

    let read =
        task {
            let! output = proc.StandardOutput.ReadToEndAsync()
            return Read output
        }

    task {
        let! results = Task.WhenAll [| write; read |]
        proc.StandardInput.Close()

        return
            match results[1] with
            | Read s -> s
            | Write -> failwith "oh no"
    }

let runSolution (data: string []) settings (info: ProcessStartInfo) =
    asyncResult {
        printfn "Running solution..."

        let! proc =
            info
            |> Result.protect Process.Start
            |> Result.mapError ProcessStartError

        let! output = writeAndRead settings proc data
        do! proc.WaitForExitAsync()

        if proc.ExitCode <> 0 then
            eprintfn $"Process exited with nonzero exit code %i{proc.ExitCode}"
            return! Error(ExitCode proc.ExitCode)
        else

            printfn "Complete!"

            return
                output.Split(Environment.NewLine)
                |> Array.filter (not << String.IsNullOrWhiteSpace)
    }

type SendResultsError =
    | PostError of exn
    | ResponseError of HttpStatusCode
    | ResponseJsonError of exn

let sendResults (endpoint: UriBuilder) (httpClient: HttpClient) (inputs: InputResponse) settings answers =
    asyncResult {
        let req =
            { Id = inputs.Id
              User = settings.Username
              Data = answers }

        let content =
            new StringContent(Suave.Json.toJson req |> Encoding.UTF8.GetString, Encoding.UTF8, "application/json")

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

        let! bytes = markResponse.Content.ReadAsByteArrayAsync()

        return!
            bytes
            |> Result.protect Suave.Json.fromJson<Result<MarkResponse, string>>
            |> Result.mapError ResponseJsonError
    }

type RunAndMarkError =
    | RunSolutionError of RunSolutionError
    | SendResultsError of SendResultsError
    | MarkSolutionError of string

let runAndMark endpoint httpClient settings info (inputs: InputResponse) =
    asyncResult {
        let! answers =
            runSolution inputs.Data settings info
            |> Async.map (Result.mapError RunSolutionError)

        let! scoreResult =
            sendResults endpoint httpClient inputs settings answers
            |>> (Result.mapError SendResultsError)

        let! response = Result.mapError MarkSolutionError scoreResult

        printfn $"%A{response.Score}"
    }
    |> Async.RunSynchronously
    |> function
        | Ok () -> ()
        | Error e -> printfn $"%A{e}"

let fileWatcherMode endpoint httpClient settings info inputs =
    printfn "Submitting results when file changes"

    printfn
        "WARNING: file watcher mode may submit multiple times or fail to submit if your IDE keeps the file descriptor open."

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

    use watcher =
        new FileSystemWatcher(dir, sol, NotifyFilter = NotifyFilters.LastWrite, IncludeSubdirectories = false)

    watcher.Changed.AddHandler onChanged
    watcher.Deleted.AddHandler onDeleted
    watcher.EnableRaisingEvents <- true
    printfn "Press enter at any point to exit"
    Console.ReadLine() |> ignore<string>
