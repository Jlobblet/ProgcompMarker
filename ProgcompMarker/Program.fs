open System.Threading
open System.Threading.Tasks
open Suave
open ProgcompMarker.Routing

[<EntryPoint>]
let main _ =
    let cts = new CancellationTokenSource()

    let conf = { defaultConfig with cancellationToken = cts.Token }

    let _, server = startWebServerAsync conf app

    Async.Start(server, cts.Token)
    Task.Delay(-1).Wait()

    cts.Cancel()

    0 // return an integer exit code
