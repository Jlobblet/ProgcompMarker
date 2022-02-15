module ProgcompCli.Input

open System
open System.Net.Http
open System.Text.Json
open Common
open ProgcompCli.Settings

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
