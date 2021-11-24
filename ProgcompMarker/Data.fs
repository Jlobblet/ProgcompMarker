module ProgcompMarker.Data

open System
open System.Collections.Concurrent
open System.IO
open System.Threading.Tasks

[<Literal>]
let dataDir = "data"

type CacheDictionary = ConcurrentDictionary<string, DateTime * string []>
let private cache = CacheDictionary()

let getFromCache (fp: string) =
    task {
        let updatedTime = File.GetLastWriteTimeUtc fp

        let add =
            Func<_, _>
                (fun fp ->
                    let t =
                        task {
                            let! data = File.ReadAllLinesAsync(fp)
                            return updatedTime, data
                        }

                    t.Result)

        let update =
            Func<_, _, _>
                (fun fp (oldTime, oldLines) ->
                    let t =
                        task {
                            if oldTime >= updatedTime then
                                return oldTime, oldLines
                            else
                                let! data = File.ReadAllLinesAsync(fp)
                                return updatedTime, data
                        }

                    t.Result)

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
