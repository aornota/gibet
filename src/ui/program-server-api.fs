module Aornota.Gibet.Ui.Program.ServerApi

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Api.ICounterApi

open Fable.Remoting.Client

let counterApi : ICounterApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ICounterApi>
