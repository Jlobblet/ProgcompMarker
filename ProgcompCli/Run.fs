module ProgcompCli.Run

open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Text
open Common
open FSharpPlus
open FsToolkit.ErrorHandling
open Settings

let private handleExn =
        function
        | Ok () -> ()
        | Error e -> printfn $"%A{e}"

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
    | ResponseJsonError of exn

let sendResults (endpoint: UriBuilder) (httpClient: HttpClient) settings answers =
    asyncResult {
        let req =
            { Id = settings.ProblemNumber
              User = settings.Username
              Data = answers }

        let content =
            new StringContent(Suave.Json.toJson req |> Encoding.UTF8.GetString, Encoding.UTF8, "application/json")

        printfn "Sending solutions to server..."

        let! responseTask =
            endpoint.Path <- $"/mark/%u{settings.ProblemNumber}"

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
            |> Result.mapError RunSolutionError

        let! scoreResult =
            sendResults endpoint httpClient settings answers
            |>> (Result.mapError SendResultsError)

        let! response = Result.mapError MarkSolutionError scoreResult

        printfn $"%A{response.Score}"
    }
    |> Async.RunSynchronously
    |> handleExn

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

type SendFileError =
    | OpenFileError of exn
    | SendResultsError of SendResultsError
    | MarkSolutionError of string

let sendFile endpoint httpClient settings =
    asyncResult {
        let! answers =
            Result.protect File.ReadAllLines
            <| settings.ExecutablePath
            |> Result.mapError OpenFileError

        let! scoreResult =
            sendResults endpoint httpClient settings answers
            |>> (Result.mapError SendResultsError)

        let! response = Result.mapError MarkSolutionError scoreResult

        printfn $"%A{response.Score}"
    }
    |> Async.RunSynchronously
    |> handleExn
