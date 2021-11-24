module ProgcompMarker.Mark

open Suave
open Suave.Json
open Suave.Logging
open Common
open Data

let private logger = Log.create "Mark"

let markHandler i : WebPart =
    context
        (fun _ ->
            match (getAnswers (string i)).Result with
            | Result.Error e -> $"Internal server error: %s{e}" |> UTF8.bytes |> ServerErrors.internal_error 
            | Ok answers ->
                mapJson
                    (fun (req: MarkRequest) ->
                        let score =
                            Array.fold2 (fun acc e1 e2 -> acc + if e1 = e2 then 1 else 0) 0 req.Data answers

                        logger.log
                            LogLevel.Info
                            (Message.eventX
                                $"Marking results for for user %s{req.User} problem %u{i}: %u{score}/%i{answers.Length}")

                        logger.log LogLevel.Info (Message.eventX $"Solutions: %A{req.Data}")
                        logger.log LogLevel.Info (Message.eventX $"Answers: %A{answers}")

                        { Id = i
                          Score = score
                          MaxScore = answers.Length })
            )
