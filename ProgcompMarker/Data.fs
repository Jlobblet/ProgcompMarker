module ProgcompMarker.Data

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.IO
open FSharpPlus
open FsToolkit.ErrorHandling
open Common
open Suave.Logging

let private logger = Log.create "Data"

[<Literal>]
let dataDir = "data"

type CacheDictionary = ConcurrentDictionary<string, DateTime * string []>
let private cache = CacheDictionary()

let private add fp =
    let updatedTime = File.GetLastWriteTimeUtc fp
    let data = File.ReadAllLines(fp)
    updatedTime, data

let private update fp (oldTime, oldLines) =
    let updatedTime = File.GetLastWriteTimeUtc fp

    if oldTime >= updatedTime then
        oldTime, oldLines
    else
        let data =
            File.ReadAllLines(fp)
            |> Array.filter (not << String.IsNullOrWhiteSpace)

        updatedTime, data

let private getFromCache (fp: string) = cache.AddOrUpdate(fp, add, update)

let private getData file problem =
    let fp = Path.Combine(dataDir, problem, file)

    if File.Exists fp then
        Ok(getFromCache fp)
    else
        Result.Error "File not found"

let getInputs = getData "inputs.txt"

let externalMarker fp problem (answers: string []) =
    taskResult {
        let info =
            ProcessStartInfo(FileName = fp, RedirectStandardInput = true, RedirectStandardOutput = true)

        let proc = Process.Start info
        let! _, inputs = getInputs problem
        inputs |> Array.iter proc.StandardInput.WriteLine
        answers |> Array.iter proc.StandardInput.WriteLine
        proc.StandardInput.Close()

        logger.log LogLevel.Info (Message.eventX $"Running external marking script %s{fp}")

        return!
            match proc.WaitForExit 10_000, proc.ExitCode with
            | true, 0 ->
                proc.StandardOutput.ReadLine().Split(" ")
                |> Array.map int
                |> Array.exactlyThree
                |> CaseValidScore
                |> Ok
            | true, ec ->
                logger.log
                    LogLevel.Error
                    (Message.eventX $"External marking script %s{fp} exited with nonzero exit code %i{ec}")

                Result.Error $"Marking script failed with exit code %i{ec}"
            | false, _ ->
                logger.log LogLevel.Error (Message.eventX $"External marking script %s{fp} timed out")
                Result.Error "Timed out"
    }

let equalityMarker problem (answers: string []) =
    taskResult {
        let! _, data = getData "answers.txt" problem

        let! zipped =
            (data, answers)
            |> Result.protect (uncurry Array.zip)
            |> Result.mapError string

        let score =
            zipped
            |> Array.filter (uncurry (=))
            |> Array.length

        return ScoreMaxScore(score, zipped.Length)
    }

let getAnswerMarker problem =
    let fp =
        Path.Combine(dataDir, problem, "mark")
        |> Path.GetFullPath

    if File.Exists fp then
        externalMarker fp problem
    else
        equalityMarker problem
