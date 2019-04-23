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
        | RemoteServerInput(Register(affinityId, connectionId)), NotRegistered ->
            let connectionId = connectionId |> Option.defaultValue (ConnectionId.Create())
            let connectionState = {
                ConnectionId = connectionId
                AffinityId = affinityId
                User = None }
            (connectionState.ConnectionId, serverStarted) |> Registered |> clientDispatch
            connectionState |> Connected
        | RemoteServerInput Activity, Connected connectionState -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
            match connectionState.User with
            | Some(userId, _) ->
                Log.Logger.Warning("TODO-NMB: {userId} |> UserActivity |> clientDispatch (for different-UserId-HasUsers)...", userId)
            | None -> ()
            connectionState |> Connected
        | RemoteServerInput(SignedIn userId), Connected connectionState ->
            let authSubscriptions = { HasUsers = false }
            let connectionState =
                match connectionState.User with
                | Some _ -> connectionState
                | None ->
                    Log.Logger.Warning("TODO-NMB: {userId} |> UserSignedIn |> clientDispatch (for different-UserId-HasUsers)...", userId)
                    { connectionState with User = (userId, authSubscriptions) |> Some }
            connectionState |> Connected
        | RemoteServerInput(SignedOut userId), Connected connectionState ->
            let connectionState =
                match connectionState.User with
                | Some (userId, _) ->
                    // TODO-NMB: Use SendServerIf [ForceSignOut] to sign out same-AffinityId-not-self-HasUsers? And check *there* if no signed in connections for userId?...
                    Log.Logger.Warning("TODO-NMB: ({userId}, None) |> ForceUserSignOut |> clientDispatch (for same-AffinityId-not-self-HasUsers)...", userId)
                    Log.Logger.Warning("TODO-NMB: {userId} |> UserSignedOut |> clientDispatch (for different-UserId-HasUsers) - if userId has no signed in connections for different-AffinityID...", userId)
                    { connectionState with User = None }
                | None -> connectionState
            connectionState |> Connected
        | RemoteServerInput HasUsers, Connected connectionState ->
            match connectionState.User with
            | Some(userId, authSubscriptions) ->
                let authSubscriptions = { authSubscriptions with HasUsers = true }
                { connectionState with User = (userId, authSubscriptions) |> Some } |> Connected
            | None -> connectionState |> Connected
        // TODO-NMB: More RemoteServerInput...
        | Disconnected, Connected connectionState ->
            match connectionState.User with
            | Some(userId, _) ->
                Log.Logger.Warning("TODO-NMB: {userId} |> UserSignedOut |> clientDispatch (for different-UserId-HasUsers) - if userId has no signed in connections (for any-AffinityID_...", userId)
            | None -> ()
            NotRegistered
        | _ ->
            Log.Logger.Warning("Unexpected input when {state} -> {input}", state, input)
            state
    state, Cmd.none
