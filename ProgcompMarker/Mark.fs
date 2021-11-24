module rec ProgcompMarker.Mark

open System
open System.Collections.Concurrent
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open ProgcompMarker.Mark
open Suave
open Suave.Json
open Suave.Logging
open Common
open Data

let private logger = Log.create "Mark"

type ResultsDict = ConcurrentDictionary<string * uint64, int * DateTimeOffset>
let private resultsDict = ResultsDict()

type ResultsDictConverter() =
    inherit JsonConverter<ResultsDict>()

    override this.Write(writer, value, options) =
        writer.WriteStartObject()

        for kvp in value do
            let (user, problem), (score, time) = kvp.Key, kvp.Value
            writer.WritePropertyName $"%s{user}-%i{problem}"
            writer.WriteStartArray()
            writer.WriteNumberValue score
            writer.WriteStringValue(time.ToString "O")
            writer.WriteEndArray()

        writer.WriteEndObject()

    override this.Read(reader, typeToConvert, options) = failwith "todo"

let private addResult (user: string) (problem: uint64) (score: int) =
    let now = DateTimeOffset.UtcNow
    let add = Func<_, _>(fun (_, _) -> (score, now))

    let update =
        Func<_, _, _>
            (fun (_, _) (oldScore, lastTime) ->
                if oldScore < score then
                    score, now
                else
                    oldScore, lastTime)

    resultsDict.AddOrUpdate((user, problem), add, update)

let private serializeOptions =
    let o = JsonSerializerOptions()
    o.Converters.Add(ResultsDictConverter())
    o

let private saveResultsToFile () =
    let contents =
        JsonSerializer.Serialize(resultsDict, serializeOptions)

    let now = DateTimeOffset.UtcNow

    let filename =
        sprintf "PROGCOMP-RESULTS-%s" (now.ToString "O")

    let dir = "output"
    let filepath = Path.Join(dir, filename)

    Directory.CreateDirectory dir
    |> ignore<DirectoryInfo>

    File.WriteAllText(filepath, contents)

let markHandler i : WebPart =
    context
        (fun _ ->
            match (getAnswers (string i)).Result with
            | Result.Error e ->
                $"Internal server error: %s{e}"
                |> UTF8.bytes
                |> ServerErrors.internal_error
            | Ok answers ->
                mapJson
                    (fun (req: MarkRequest) ->
                        let score =
                            Array.fold2 (fun acc e1 e2 -> acc + if e1 = e2 then 1 else 0) 0 req.Data answers

                        logger.log
                            LogLevel.Info
                            (Message.eventX
                                $"Marking results for for user %s{req.User} problem %u{i}: %u{score}/%i{answers.Length}")

                        addResult req.User i score
                        |> ignore<int * DateTimeOffset>

                        saveResultsToFile ()

                        { Id = i
                          Score = score
                          MaxScore = answers.Length }))
