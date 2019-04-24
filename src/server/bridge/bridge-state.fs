module Aornota.Gibet.Server.Bridge.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.Hub

open System

open Elmish

open Serilog
open Aornota.Gibet.Common.Domain.User

let private serverStarted = DateTimeOffset.UtcNow

let initialize (clientDispatch:Dispatch<RemoteUiInput>) () : HubState * Cmd<ServerInput> =
    Initialized |> clientDispatch
    NotRegistered, Cmd.none

let transition clientDispatch input state : HubState * Cmd<ServerInput> =
    let state =
        match input, state with
        | RemoteServerInput(Register(affinityId, connectionId)), NotRegistered ->
            let connectionState = {
                ConnectionId = connectionId |> Option.defaultValue (ConnectionId.Create())
                AffinityId = affinityId  }
            (connectionState.ConnectionId, serverStarted) |> Registered |> clientDispatch
            connectionState |> Unauth
        | RemoteServerInput Activity, Auth(connectionState, userId, hasUsers) -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
            userId |> UserActivity |> hub.SendClientIf (userId |> differentUserHasUsers)
            (connectionState, userId, hasUsers) |> Auth
        | RemoteServerInput(SignedIn userId), Unauth connectionState ->
            userId |> UserSignedIn |> hub.SendClientIf (userId |> differentUserHasUsers)
            (connectionState, userId, false) |> Auth
        | RemoteServerInput SignedOut, Auth(connectionState, userId, _) ->
            None |> ForceSignOut |> hub.SendServerIf ((userId, connectionState.AffinityId, connectionState.ConnectionId) |> sameUserSameAffinityDifferentConnectionSignedIn)
            connectionState |> Unauth
        | RemoteServerInput(ForceSignOut forcedSignOutReason), Auth(connectionState, userId, _) ->
            forcedSignOutReason |> ForceUserSignOut |> clientDispatch
            if (userId, connectionState.ConnectionId) |> signedInDifferentConnection (hub.GetModels()) |> not then
                userId |> UserSignedOut |> hub.SendClientIf (userId |> differentUserHasUsers)
            connectionState |> Unauth
        | RemoteServerInput HasUsers, Auth(connectionState, userId, false) ->
            (connectionState, userId, true) |> Auth

        // TODO-NMB: More RemoteServerInput...

        | Disconnected, Auth(connectionState, userId, _) ->
            if (userId, connectionState.ConnectionId) |> signedInDifferentConnection (hub.GetModels()) |> not then
                userId |> UserSignedOut |> hub.SendClientIf (userId |> differentUserHasUsers)
            NotRegistered
        | Disconnected, Unauth _ ->
            NotRegistered
        | _ ->
            Log.Logger.Warning("Unexpected input when {state} -> {input}", state, input)
            state
    state, Cmd.none
