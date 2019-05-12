module Aornota.Gibet.Server.Bridge.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Logger

open System

open Elmish

open Serilog

let private logger = Log.Logger |> sourcedLogger "Bridge.State"

let private serverStarted = DateTimeOffset.UtcNow

let private sendIfNotSignedIn userId connectionId =
    if not (hub.GetModels() |> signedInDifferentConnection userId connectionId) then hub.SendClientIf (differentUserHasUsers userId) (UserSignedOut userId)

// #region handleUnexpectedInput
let private handleUnexpectedInput clientDispatch (input:ServerInput) (state:HubState) =
    let unexpectedInputWhenState = "Unexpected {input} when {state}"
    clientDispatch (UnexpectedServerInput(sprintf "Unexpected %A when %A" input state))
#if DEBUG
    logger.Warning(unexpectedInputWhenState, input, state)
#else
    logger.Error(unexpectedInputWhenState, input, state)
#endif
    state
// #endregion

let private handleRemoteServerInput clientDispatch input state =
    match input, state with
    | Register(affinityId, connectionId), NotRegistered ->
        let connectionState = {
            ConnectionId = connectionId |> Option.defaultValue (ConnectionId.Create())
            AffinityId = affinityId }
        clientDispatch (Registered(connectionState.ConnectionId, serverStarted))
        Unauth connectionState
    | Activity, Auth(connectionState, userId, hasUsers) -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        hub.SendClientIf (differentUserHasUsers userId) (UserActivity userId)
        Auth(connectionState, userId, hasUsers)
    | SignedIn userId, Unauth connectionState ->
        hub.SendClientIf (differentUserHasUsers userId) (UserSignedIn userId)
        Auth(connectionState, userId, false)
    | SignedOut, Auth(connectionState, userId, _) ->
        hub.SendServerIf (sameUserSameAffinityDifferentConnection userId connectionState.AffinityId connectionState.ConnectionId) (ForceSignOut SelfSameAffinityDifferentConnection)
        sendIfNotSignedIn userId connectionState.ConnectionId
        Unauth connectionState
    | ForceSignOut forcedSignOutReason, Auth(connectionState, userId, _) ->
        clientDispatch (ForceUserSignOut forcedSignOutReason)
        sendIfNotSignedIn userId connectionState.ConnectionId
        Unauth connectionState
    | ForceChangePassword byUserName, Auth(connectionState, userId, hasUsers) ->
        clientDispatch (ForceUserChangePassword byUserName)
        Auth(connectionState, userId, hasUsers)
    | HasUsers, Auth(connectionState, userId, false) -> Auth(connectionState, userId, true)
    // TODO-NMB: More RemoteServerInput?...
    | _ -> state |> handleUnexpectedInput clientDispatch (RemoteServerInput input)

let private handleDisconnected clientDispatch state =
    match state with
    | Auth(connectionState, userId, _) ->
        sendIfNotSignedIn userId connectionState.ConnectionId
        NotRegistered
    | Unauth _ -> NotRegistered
    | _ -> state |> handleUnexpectedInput clientDispatch Disconnected

let initialize clientDispatch () : HubState * Cmd<ServerInput> =
    clientDispatch Initialized
    NotRegistered, Cmd.none

let transition clientDispatch input state : HubState * Cmd<ServerInput> =
    let state =
        match input, state with
        | RemoteServerInput input, _ -> state |> handleRemoteServerInput clientDispatch input
        | Disconnected, _ -> state |> handleDisconnected clientDispatch
    state, Cmd.none
