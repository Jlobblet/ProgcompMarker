module rec ProgcompMarker.Mark

open System
open System.Collections.Concurrent
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open FsToolkit.ErrorHandling
open Suave
open Suave.Json
open Suave.Logging
open ProgcompMarker.Mark
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

let private addResult (user: string) (problem: uint64) (score: Score) =
    let score =
        match score with
        | ScoreMaxScore (score, _) -> score
        | CaseValidScore (_, _, score) -> score

    let now = DateTimeOffset.UtcNow
    let add = Func<_, _>(fun (_, _) -> (score, now))

    let update =
        Func<_, _, _> (fun (_, _) (oldScore, lastTime) ->
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
    let contents = JsonSerializer.Serialize(resultsDict, serializeOptions)

    let now = DateTimeOffset.UtcNow

    let filename = sprintf "PROGCOMP-RESULTS-%s" (now.ToString "O")

    let dir = "output"
    let filepath = Path.Join(dir, filename)

    Directory.CreateDirectory dir
    |> ignore<DirectoryInfo>

    File.WriteAllText(filepath, contents)

let markHandler i : WebPart =
    context (fun _ ->
        mapJson (fun (req: MarkRequest) ->
            result {
                let marker = getAnswerMarker (string i)
                let! score = marker(req.Data).GetAwaiter().GetResult()

                logger.log
                    LogLevel.Info
                    (Message.eventX $"Marking results for for user %s{req.User} problem %u{i}: %A{score}")

                addResult req.User i score
                |> ignore<int * DateTimeOffset>

                saveResultsToFile ()

                return { Id = i; Score = score }
            }))
