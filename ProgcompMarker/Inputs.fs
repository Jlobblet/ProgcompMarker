module ProgcompMarker.Inputs

open System.Text
open Suave
open Suave.Json
open Suave.Logging
open Suave.Successful
open Common
open Data

let private logger = Log.create "Input"

let inputsHandler i : WebPart =
    context (fun _ ->
        match getInputs (string i) with
        | Result.Error e ->
            $"Internal server error: %s{e}"
            |> UTF8.bytes
            |> ServerErrors.internal_error
        | Ok (_, d) ->
            logger.log LogLevel.Info (Message.eventX $"Serving inputs for problem %u{i}")

            { Id = i; Data = d }
            |> toJson
            |> Encoding.UTF8.GetString
            |> OK)
