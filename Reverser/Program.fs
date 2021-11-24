open System

stdin.ReadToEnd().Split(Environment.NewLine)
|> Array.map (fun s -> s.ToCharArray() |> Array.rev |> String)
|> String.concat Environment.NewLine
|> printfn "%s"
