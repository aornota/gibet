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
        | RemoteServerInput UserActivity, Connected connectionState -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
            match connectionState.User with
            | Some(userId, _) ->
                Log.Logger.Warning("TODO-NMB: {userId} |> UserActive |> clientDispatch (for signed-in-HasUsers-different-UserId)...", userId)
            | None -> ()
            connectionState |> Connected
        // TODO-NMB: More RemoteServerInput...
        | Disconnected, Connected _ -> // TODO-NMB: Send RemoteUiInput.UserSignedOut if last connection for User?...
            NotRegistered
        | _ ->
            Log.Logger.Warning("Unexpected input when {state} -> {input}", state, input)
            state
    state, Cmd.none
