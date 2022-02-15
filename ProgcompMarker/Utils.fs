[<Microsoft.FSharp.Core.AutoOpen>]
module ProgcompMarker.Utils

let uncurry f (a, b) = f a b

module Array =
    let exactlyThree (arr: _ []) =
        if arr.Length <> 3 then
            failwith $"Array length is %i{arr.Length} (expected 3)"

        arr[0], arr[1], arr[2]
