module ProgcompMarker.Data

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open System.Threading.Tasks
open FSharpPlus
open FsToolkit.ErrorHandling
open Common

[<Literal>]
let dataDir = "data"

let uncurry f (a, b) = f a b

type CacheDictionary = ConcurrentDictionary<string, DateTime * string []>
let private cache = CacheDictionary()

let getFromCache (fp: string) =
    task {
        let updatedTime = File.GetLastWriteTimeUtc fp

        let add =
            Func<_, _> (fun fp ->
                let t =
                    task {
                        let! data = File.ReadAllLinesAsync(fp)
                        return updatedTime, data
                    }

                t.GetAwaiter().GetResult())

        let update =
            Func<_, _, _> (fun fp (oldTime, oldLines) ->
                let t =
                    task {
                        if oldTime >= updatedTime then
                            return oldTime, oldLines
                        else
                            let! data = File.ReadAllLinesAsync(fp)
                            return updatedTime, data
                    }

                t.GetAwaiter().GetResult())

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

let getAnswers problem =
    let fp = Path.Combine(dataDir, problem, "mark")

    if File.Exists fp then
        taskResult {
            let info = ProcessStartInfo()
            info.FileName <- Path.GetFullPath fp
            info.RedirectStandardInput <- true
            info.RedirectStandardOutput <- true

            let mark (answers: string []) =
                taskResult {
                    let proc = Process.Start info
                    let! _, inputs = getInputs problem
                    inputs |> Array.iter proc.StandardInput.WriteLine
                    answers |> Array.iter proc.StandardInput.WriteLine
                    proc.StandardInput.Close()

                    if proc.WaitForExit 10_000 then
                        return
                            proc.StandardOutput.ReadLine().Split(" ")
                            |> Array.map int
                            |> Array.exactlyThree
                            |> CaseValidScore
                    else
                        return! Error "Timed out"
                }

            return mark
        }
    else
        taskResult {
            let! _, data = getData "answers.txt" problem

            let mark (answers: string []) =
                taskResult {
                    let! zipped =
                        Result.protect (uncurry Array.zip) (data, answers)
                        |> Result.mapError string

                    let score =
                        zipped
                        |> Array.filter (uncurry (=))
                        |> Array.length

                    return ScoreMaxScore(score, zipped.Length)
                }

            return mark
        }
