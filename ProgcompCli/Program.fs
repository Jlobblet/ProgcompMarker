open System
open System.IO
open System.Net.Http
open Common
open ProgcompCli.Settings
open ProgcompCli.Input
open ProgcompCli.Run

[<EntryPoint>]
let main argv =
    if not <| File.Exists ConfigFile then
        Settings.firstTimeSetup ()

    let settings = Settings.fromArgv argv

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
