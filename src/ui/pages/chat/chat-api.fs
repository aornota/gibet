module Aornota.Gibet.Ui.Pages.Chat.ChatApi

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Api.ChatApi

open Fable.Remoting.Client

let chatApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ChatApi>
