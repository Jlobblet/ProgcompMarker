open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open Common
open ProgcompCli.Args

let getInputs (endpoint: UriBuilder) (httpClient: HttpClient) settings =
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
        if not <| Array.isEmpty settings.ExecutableArgs then
            info.ArgumentList.Add "--"

        Array.iter info.ArgumentList.Add data

    info

let runSolution (data: string []) settings (info: ProcessStartInfo) =
    printfn "Running solution..."

    let proc = Process.Start info

    if settings.InputMode = InputToStdIn then
        data |> Array.iter proc.StandardInput.WriteLine

        proc.StandardInput.Close()

    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        eprintfn $"Process exited with nonzero exit code %i{proc.ExitCode}, quitting"
        exit 1

    printfn "Complete!"

    proc
        .StandardOutput
        .ReadToEnd()
        .Split(Environment.NewLine)
    |> Array.filter (not << String.IsNullOrWhiteSpace)

let sendResults (endpoint: UriBuilder) (httpClient: HttpClient) (inputs: InputResponse) settings answers =
    let req =
        { Id = inputs.Id
          User = settings.Username
          Data = answers }

    let content =
        new StringContent(JsonSerializer.Serialize req, Encoding.UTF8, "application/json")

    printfn "Sending solutions to server..."

    let markResponse =
        endpoint.Path <- $"/mark/%u{inputs.Id}"
        httpClient.PostAsync(endpoint.Uri, content).Result

    if not markResponse.IsSuccessStatusCode then
        eprintfn $"Error reading data from server: %s{markResponse.ToString()}"
        exit 1

    JsonSerializer.Deserialize<MarkResponse>(markResponse.Content.ReadAsStream())

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

    let answers = runSolution inputs.Data settings info

    let score =
        sendResults endpoint httpClient inputs settings answers

    printfn $"Score: %i{score.Score}/%i{score.MaxScore}"

    0
