module Aornota.Gibet.Server.Bridge.State

open Aornota.Gibet.Common.Bridge
//open Aornota.Gibet.Common.Domain.Affinity
//open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Bridge.Hub

open Elmish

let initialize (clientDispatch:Dispatch<RemoteUiInput>) () =
    NotConnected, Cmd.none

let transition clientDispatch input state : HubState * Cmd<ServerInput> = // TODO-NMB: Logging (or via withConsoleTrace)?...
    match input with
    | RemoteServer _ -> // TODO-NMB...
        state, Cmd.none
    | Disconnected ->
        // TODO-NMB: Send RemoteUiInput.UserSignedOut if last connection for User?...
        NotConnected, Cmd.none
