module Aornota.Gibet.Server.Bridge.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.SourcedLogger

open System

open Elmish

open Serilog

let private serverStarted = DateTimeOffset.UtcNow

let private sendIfNotSignedIn userId connectionId =
    if not (serverHub.GetModels() |> signedInDifferentConnection userId connectionId) then serverHub.SendClientIf (differentUserHasUsers userId) (UserSignedOut userId)

// #region handleUnexpectedInput
let private handleUnexpectedInput (logger:ILogger) clientDispatch (input:ServerInput) (state:HubState) =
    let unexpectedInputWhenState = "Unexpected {input} when {state}"
    clientDispatch (UnexpectedServerInput(sprintf "Unexpected %A when %A" input state))
#if DEBUG
    logger.Warning(unexpectedInputWhenState, input, state)
#else
    logger.Error(unexpectedInputWhenState, input, state)
#endif
    state
// #endregion

let private handleRemoteServerInput logger clientDispatch input state =
    match input, state with
    | Register(affinityId, connectionId), NotRegistered ->
        let connectionState = {
            ConnectionId = connectionId |> Option.defaultValue (ConnectionId.Create())
            AffinityId = affinityId }
        let sinceServerStarted = (DateTimeOffset.UtcNow - serverStarted).TotalSeconds * 1.<second>
        clientDispatch (Registered(connectionState.ConnectionId, sinceServerStarted))
        Unauth connectionState
    | Activity, Auth(connectionState, userId, hasUsers) -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        serverHub.SendClientIf (differentUserHasUsers userId) (UserActivity userId)
        Auth(connectionState, userId, hasUsers)
    | SignedIn userId, Unauth connectionState ->
        serverHub.SendClientIf (differentUserHasUsers userId) (UserSignedIn userId)
        Auth(connectionState, userId, { HasUsers = false ; HasChatMessages = false })
    | SignedOut, Auth(connectionState, userId, _) ->
        serverHub.SendServerIf (sameUserSameAffinityDifferentConnection userId connectionState.AffinityId connectionState.ConnectionId) (ForceSignOut SelfSameAffinityDifferentConnection)
        sendIfNotSignedIn userId connectionState.ConnectionId
        Unauth connectionState
    | ForceSignOut forcedSignOutReason, Auth(connectionState, userId, _) ->
        clientDispatch (ForceUserSignOut forcedSignOutReason)
        sendIfNotSignedIn userId connectionState.ConnectionId
        Unauth connectionState
    | ForceChangePassword byUserName, Auth(connectionState, userId, hasUsers) ->
        clientDispatch (ForceUserChangePassword byUserName)
        Auth(connectionState, userId, hasUsers)
    | HasUsers, Auth(connectionState, userId, subscriptions) when not subscriptions.HasUsers -> Auth(connectionState, userId, { subscriptions with HasUsers = true })
    | HasChatMessages, Auth(connectionState, userId, subscriptions) when not subscriptions.HasChatMessages -> Auth(connectionState, userId, { subscriptions with HasChatMessages = true })
    | _ -> state |> handleUnexpectedInput logger clientDispatch (RemoteServerInput input)

let private handleDisconnected logger clientDispatch state =
    match state with
    | Auth(connectionState, userId, _) ->
        sendIfNotSignedIn userId connectionState.ConnectionId
        NotRegistered
    | Unauth _ -> NotRegistered
    | _ -> state |> handleUnexpectedInput logger clientDispatch Disconnected

let initialize clientDispatch () : HubState * Cmd<ServerInput> =
    clientDispatch Initialized
    NotRegistered, Cmd.none

let transition logger clientDispatch input state : HubState * Cmd<ServerInput> =
    let sourcedLogger, logger = logger |> sourcedLogger "Bridge.State", ()
    let state =
        match input, state with
        | RemoteServerInput input, _ -> state |> handleRemoteServerInput sourcedLogger clientDispatch input
        | Disconnected, _ -> state |> handleDisconnected sourcedLogger clientDispatch
    state, Cmd.none
