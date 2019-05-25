module Aornota.Gibet.Server.Bridge.Hub

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.HubState

open Elmish.Bridge

let serverHub =
    ServerHub<HubState, ServerInput, RemoteUiInput>()
        .RegisterServer(RemoteServerInput)
