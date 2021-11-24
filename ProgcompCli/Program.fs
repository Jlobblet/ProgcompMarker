open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open Common

let args = Environment.GetCommandLineArgs()

if args.Length <> 3 then
    eprintfn $"Usage: %s{args.[0]} ID EXECUTABLE"
    exit 1

let problemId =
    match UInt64.TryParse args.[1] with
    | true, u -> u
    | false, _ ->
        eprintfn $"Failed to parse %s{args.[1]} as an unsigned integer."
        exit 1

let executable = args.[2]

if not <| File.Exists executable then
    eprintfn $"Could not find %s{executable} to run."
    exit 1

let user =
    Environment.GetEnvironmentVariable("PROGCOMP_USERNAME", EnvironmentVariableTarget.Process)

if String.IsNullOrWhiteSpace user then
    eprintfn
        "Please set your username in the PROGCOMP_USERNAME environment variable (any string will do - it's used to differentiate you from other participants)."

    exit 1

let endpoint =
#if DEBUG
    UriBuilder "http://127.0.0.1:8080/"
#else
    let e =
        Environment.GetEnvironmentVariable("PROGCOMP_ENDPOINT", EnvironmentVariableTarget.Process)

    if String.IsNullOrWhiteSpace e then
        eprintfn "Please set the PROGCOMP_ENDPOINT environment variable."
        exit 1

    UriBuilder e
#endif

let httpClient = new HttpClient()

let inputsResponse =
    endpoint.Path <- $"/inputs/%u{problemId}"
    httpClient.GetAsync(endpoint.Uri).Result

if not inputsResponse.IsSuccessStatusCode then
    eprintfn $"Error reading data from server: %s{inputsResponse.ToString()}"
    exit 1

let inputs =
    JsonSerializer.Deserialize<InputResponse>(inputsResponse.Content.ReadAsStream())

let data = inputs.Data

printfn "Retrieved input data"

let info = ProcessStartInfo()
info.FileName <- executable
info.RedirectStandardInput <- true
info.RedirectStandardOutput <- true

printfn "Running solution..."

let proc = Process.Start info
data |> Array.iter proc.StandardInput.WriteLine
proc.StandardInput.Close()
proc.WaitForExit()

printfn "Complete!"

let answers =
    proc
        .StandardOutput
        .ReadToEnd()
        .Split(Environment.NewLine)
    |> Array.filter (not << String.IsNullOrWhiteSpace)

let req =
    { Id = inputs.Id
      User = user
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
