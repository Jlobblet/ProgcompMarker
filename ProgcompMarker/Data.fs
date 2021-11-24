module ProgcompMarker.Data

open System.IO
open System.Threading.Tasks

[<Literal>]
let dataDir = "data"

let problems =
    Directory.GetDirectories dataDir
    |> Set.ofArray
    |> Set.map Path.GetFileName

let private getData file problem =
    if Set.contains problem problems then
        task {
            let fp = Path.Combine(dataDir, problem, file)
            let! lines = File.ReadAllLinesAsync fp
            return Ok(lines)
        }
    else
        "File not found" |> Error |> Task.FromResult

let getInputs = getData "inputs.txt"

let getAnswers = getData "answers.txt"
