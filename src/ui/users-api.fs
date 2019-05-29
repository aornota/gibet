module Aornota.Gibet.Ui.UsersApi

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Api.UsersApi

open Fable.Remoting.Client

let usersApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<UsersApi>
