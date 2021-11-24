module ProgcompMarker.Mark

open System.Text
open Suave
open Suave.Json
open Suave.RequestErrors
open Suave.Writers
open Suave.Logging
open Suave.Successful
open Common
open Data

let private logger = Log.create "Mark"


let markHandler i : WebPart =
    context
        (fun ctx ->
            match (getAnswers (string i)).Result with
            | Ok d ->
                mapJson
                    (fun (req: MarkRequest) ->
                        let score =
                            Array.fold2 (fun acc e1 e2 -> acc + if e1 = e2 then 1 else 0) 0 req.Data d

                        logger.log
                            LogLevel.Info
                            (Message.eventX
                                $"Marking results for for user %s{req.User} problem %u{i}: %u{score}/%i{d.Length}")

                        logger.log LogLevel.Info (Message.eventX $"Solutions: %A{req.Data}")
                        logger.log LogLevel.Info (Message.eventX $"Answers: %A{d}")

                        { Id = i
                          Score = score
                          MaxScore = d.Length })
            //            | Error e -> NOT_FOUND e
            )
