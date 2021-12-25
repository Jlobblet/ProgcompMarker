open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open Common
open ProgcompCli.Args

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

    let httpClient = new HttpClient()

    let inputsResponse =
        endpoint.Path <- $"/inputs/%u{settings.ProblemNumber}"
        httpClient.GetAsync(endpoint.Uri).Result

    if not inputsResponse.IsSuccessStatusCode then
        eprintfn $"Error reading data from server: %O{inputsResponse}"
        exit 1

    let inputs =
        JsonSerializer.Deserialize<InputResponse>(inputsResponse.Content.ReadAsStream())

    let data = inputs.Data

    printfn "Retrieved input data"

    let info = ProcessStartInfo()
    info.FileName <- executable
    info.RedirectStandardInput <- true
    info.RedirectStandardOutput <- true
    settings.ExecutableArgs |> Array.iter info.ArgumentList.Add
        
    if settings.InputMode = InputAsArgs then
        if not <| Array.isEmpty settings.ExecutableArgs then
            info.ArgumentList.Add "--"
            
        data |> Array.iter info.ArgumentList.Add

    printfn "Running solution..."

    let proc = Process.Start info
    
    if settings.InputMode = InputToStdIn then
        data |> Array.iter proc.StandardInput.WriteLine
        proc.StandardInput.Close()

    proc.WaitForExit()

    if proc.ExitCode <> 0 then
        eprintfn $"Process exited with exit code %i{proc.ExitCode}"
        exit 1

    printfn "Complete!"

    let answers =
        proc
            .StandardOutput
            .ReadToEnd()
            .Split(Environment.NewLine)
        |> Array.filter (not << String.IsNullOrWhiteSpace)

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

    let score =
        JsonSerializer.Deserialize<MarkResponse>(markResponse.Content.ReadAsStream())

    printfn $"Score: %i{score.Score}/%i{score.MaxScore}"

    0
