module ProgcompMarker.Inputs

open System.Text
open Suave
open Suave.Json
open Suave.Logging
open Suave.RequestErrors
open Suave.Successful
open Common
open Data

let private logger = Log.create "Input"

let inputsHandler i : WebPart =
    context
        (fun ctx ->
            match (getInputs (string i)).Result with
            | Ok d ->
                logger.log LogLevel.Info (Message.eventX $"Serving inputs for problem %u{i}")

                { Id = i; Data = d }
                |> toJson
                |> Encoding.UTF8.GetString
                |> OK
            //            | Error e -> NOT_FOUND e
            )
