module Aornota.Gibet.Server.Bridge.Hub

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Bridge.HubState

open Elmish.Bridge

let hub =
    ServerHub<HubState, ServerInput, RemoteUiInput>()
        .RegisterServer(RemoteServerInput)
