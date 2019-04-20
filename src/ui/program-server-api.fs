module Aornota.Gibet.Ui.Program.ServerApi

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Api.ICounterApi // TEMP-NMB...

open Fable.Remoting.Client

let counterApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ICounterApi>
