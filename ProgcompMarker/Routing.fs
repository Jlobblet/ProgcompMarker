module ProgcompMarker.Routing

open Suave
open Suave.Filters
open Suave.Operators
open Inputs
open Mark

let app =
    choose [ GET
             >=> choose [ pathScan "/inputs/%u" inputsHandler ]
             POST
             >=> choose [ pathScan "/mark/%u" markHandler ] ]
