module ProgcompCli.Args

open System
open System.IO
open Argu

let ConfigFile = "app.config"

type Arguments =
    | [<Unique; AltCommandLine("-w")>] Auto_Submit
    | [<Unique; AltCommandLine("-a")>] Pass_Input_As_Args
    | [<Mandatory; NoCommandLine>] Endpoint of string
    | [<Mandatory; NoCommandLine>] Username of string
    | Executable_Args of arg: string list
    | [<MainCommand; Mandatory>] Problem_And_Executable of number: uint64 * path: string

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Auto_Submit -> "Automatically run the provided executable again when the file is updated."
            | Pass_Input_As_Args -> "If set, pass the problem input as arguments rather than to standard input."
            | Endpoint _ -> "URL to the server that will send problem inputs and mark answers."
            | Username _ -> "A username that is used to differentiate submissions."
            | Executable_Args _ -> "Arguments to be passed to the executable."
            | Problem_And_Executable _ ->
                "Problem number to run and path to the executable to run problem inputs again."

let errorHandler =
    ProcessExiter(
        colorizer =
            function
            | ErrorCode.HelpText -> None
            | _ -> Some ConsoleColor.Red
    )

let private Parser =
    ArgumentParser<Arguments>(errorHandler = errorHandler, usageStringCharacterWidth = Console.WindowWidth)

type SubmissionMode =
    | RunOnce
    | FileWatcherRepeat
    static member PostProcess result =
        if result then
            FileWatcherRepeat
        else
            RunOnce

type InputMode =
    | InputToStdIn
    | InputAsArgs
    static member PostProcess result =
        if result then
            InputAsArgs
        else
            InputToStdIn

type Settings =
    { SubmissionMode: SubmissionMode
      PassInputAsArgs: InputMode
      Endpoint: UriBuilder
      Username: string
      ExecutableArgs: string []
      ExecutablePath: string
      ProblemNumber: uint64 }

[<RequireQualifiedAccess>]
module Settings =
    let firstTimeSetup () =
        if Console.IsInputRedirected then
            eprintfn "Error: cannot run first-time setup if input is redirected."
            exit 1

        printfn "No configuration file found. First time setup:"
        let mutable retry = true
        let mutable endpoint = ""

        while retry do
            printf "Please enter the endpoint for the server: "
            endpoint <- Console.ReadLine()
            printf $"Is %s{endpoint} correct? (Y/n)"
            let key = Console.ReadKey()
            printf "\n"

            if [| ConsoleKey.Enter; ConsoleKey.Y |]
               |> Array.contains key.Key then
                retry <- false

        // Shadow to enforce immutability now
        let endpoint = endpoint

        let mutable username = ""
        retry <- true

        while retry do
            printf "Please enter a username: "
            username <- Console.ReadLine()
            printf $"Is %s{username} correct? (Y/n)"
            let key = Console.ReadKey()
            printf "\n"

            if [| ConsoleKey.Enter; ConsoleKey.Y |]
               |> Array.contains key.Key then
                retry <- false

        let username = username

        let args = [ Endpoint endpoint; Username username ]

        let config =
            Parser.PrintAppSettingsArguments(args, true)
            + "\n"

        File.WriteAllText(ConfigFile, config, Text.Encoding.UTF8)

        printf "Submit solution? (y/N)"
        let key = Console.ReadKey()
        printf "\n"

        if key.Key <> ConsoleKey.Y then exit 0

    let private parseNonempty name s =
        if String.IsNullOrWhiteSpace s then
            failwith $"%s{name} cannot be blank."
        else
            s

    let fromArgv argv =
        let configurationReader =
            ConfigurationReader.FromAppSettingsFile ConfigFile

        let results = Parser.Parse(argv, configurationReader)

        let submissionMode =
            results.Contains <@ Auto_Submit @>
            |> SubmissionMode.PostProcess

        let passInputAsArgs =
            results.Contains <@ Pass_Input_As_Args @>
            |> InputMode.PostProcess

        let endpoint =
            results.PostProcessResult(<@ Endpoint @>, parseNonempty (nameof Endpoint) >> UriBuilder)

        let username =
            results.PostProcessResult(<@ Username @>, parseNonempty (nameof Username))

        let executableArgs =
            results.TryGetResult <@ Executable_Args @>
            |> Option.fold (fun _ -> Array.ofList) Array.empty

        let problemNumber, executablePath =
            results.GetResult <@ Problem_And_Executable @>

        { SubmissionMode = submissionMode
          PassInputAsArgs = passInputAsArgs
          Endpoint = endpoint
          Username = username
          ExecutableArgs = executableArgs
          ExecutablePath = executablePath
          ProblemNumber = problemNumber }
