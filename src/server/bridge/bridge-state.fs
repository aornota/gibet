module Aornota.Gibet.Server.Bridge.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.Hub

open System

open Elmish

open Serilog

let private serverStarted = DateTimeOffset.UtcNow

let private logUnexpectedInput(state:HubState, input) =
    Log.Logger.Warning("Unexpected input when {state} -> {input}", state, input)

let private handleRemoteServerInput clientDispatch input state =
    match input, state with
    | Register(affinityId, connectionId), NotRegistered ->
        let connectionState = {
            ConnectionId = connectionId |> Option.defaultValue (ConnectionId.Create())
            AffinityId = affinityId  }
        (connectionState.ConnectionId, serverStarted) |> Registered |> clientDispatch
        connectionState |> Unauth
    | Activity, Auth(connectionState, userId, hasUsers) -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        userId |> UserActivity |> hub.SendClientIf (userId |> differentUserHasUsers)
        (connectionState, userId, hasUsers) |> Auth
    | SignedIn userId, Unauth connectionState ->
        userId |> UserSignedIn |> hub.SendClientIf (userId |> differentUserHasUsers)
        (connectionState, userId, false) |> Auth
    | SignedOut, Auth(connectionState, userId, _) ->
        None |> ForceSignOut |> hub.SendServerIf ((userId, connectionState.AffinityId, connectionState.ConnectionId) |> sameUserSameAffinityDifferentConnection)
        connectionState |> Unauth
    | ForceSignOut forcedSignOutReason, Auth(connectionState, userId, _) ->
        forcedSignOutReason |> ForceUserSignOut |> clientDispatch
        if hub.GetModels() |> signedInDifferentConnection (userId, connectionState.ConnectionId) |> not then
            userId |> UserSignedOut |> hub.SendClientIf (userId |> differentUserHasUsers)
        connectionState |> Unauth
    | HasUsers, Auth(connectionState, userId, false) ->
        (connectionState, userId, true) |> Auth
    // TODO-NMB: More RemoteServerInput?...
    | _ ->
        (state, input) |> logUnexpectedInput
        state

let private handleDisconnected clientDispatch state =
    match state with
    | Auth(connectionState, userId, _) ->
        if hub.GetModels() |> signedInDifferentConnection (userId, connectionState.ConnectionId) |> not then
            userId |> UserSignedOut |> hub.SendClientIf (userId |> differentUserHasUsers)
        NotRegistered
    | Unauth _ ->
        NotRegistered
    | _ ->
        (state, Disconnected) |> logUnexpectedInput
        state

let initialize (clientDispatch:Dispatch<RemoteUiInput>) () : HubState * Cmd<ServerInput> =
    Initialized |> clientDispatch
    NotRegistered, Cmd.none

let transition clientDispatch input state : HubState * Cmd<ServerInput> =
    let state =
        match input, state with
        | RemoteServerInput input, _ -> handleRemoteServerInput clientDispatch input state
        | Disconnected, _ -> handleDisconnected clientDispatch state
        (* TEMP-NMB...
        | _ ->
            (state, input) |> logUnexpectedInput
            state *)
    state, Cmd.none
