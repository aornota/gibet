module Aornota.Gibet.Ui.Program.ServerApi

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Api.CounterApi

open Fable.Remoting.Client

let counterApi : CounterApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<CounterApi>
