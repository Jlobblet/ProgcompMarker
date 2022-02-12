module ProgcompMarker.Data

open System
open System.Collections.Concurrent
open System.IO
open System.Threading.Tasks

[<Literal>]
let dataDir = "data"

type CacheDictionary = ConcurrentDictionary<string, DateTimeOffset * string []>
let private cache = CacheDictionary()

let private add fp =
    let updatedTime = File.GetLastWriteTimeUtc fp |> DateTimeOffset
    let t =
        task {
            let! data = File.ReadAllLinesAsync(fp)
            return updatedTime, data
        }
    t.Result

let private update fp (oldTime, oldLines) =
    let updatedTime = File.GetLastWriteTimeUtc fp |> DateTimeOffset
    let t =
        task {
            if oldTime >= updatedTime then
                return oldTime, oldLines
            else
                let! data = File.ReadAllLinesAsync(fp)
                return updatedTime, data
        }
    t.Result

let getFromCache (fp: string) =
    task {
        return cache.AddOrUpdate(fp, add, update)
    }

let private getData file problem =
    let fp = Path.Combine(dataDir, problem, file)

    if File.Exists fp then
        task {
            let! lines = getFromCache fp
            return Ok lines
        }
    else
        "File not found" |> Error |> Task.FromResult

let getInputs = getData "inputs.txt"

let getAnswers = getData "answers.txt"
