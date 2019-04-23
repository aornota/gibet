module Aornota.Gibet.Server.Bridge.State

open Aornota.Gibet.Common.Bridge
//open Aornota.Gibet.Common.Domain.Affinity
//open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Bridge.Hub

open System

open Elmish

open Serilog

let private serverStarted = DateTimeOffset.UtcNow

let initialize (clientDispatch:Dispatch<RemoteUiInput>) () =
    Initialized |> clientDispatch
    NotRegistered, Cmd.none

let transition clientDispatch input state : HubState * Cmd<ServerInput> =
    let state =
        match input, state with
        | RemoteServerInput(Register affinityId), NotRegistered ->
            let connectionState = {
                ConnectionId = ConnectionId.Create()
                AffinityId = affinityId
                User = None }
            (connectionState.ConnectionId, serverStarted) |> Registered |> clientDispatch
            connectionState |> Connected
        // TODO-NMB: More RemoteServerInput...
        | Disconnected, Connected _ -> // TODO-NMB: Send RemoteUiInput.UserSignedOut if last connection for User?...
            NotRegistered
        | _ ->
            Log.Logger.Warning("Unexpected input when {state} -> {input}", state, input)
            state
    state, Cmd.none
