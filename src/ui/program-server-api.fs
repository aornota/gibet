module Aornota.Gibet.Ui.Program.ServerApi

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Api.UserApi

open Fable.Remoting.Client

let userApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<UserApi>
