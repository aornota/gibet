module Aornota.Gibet.Server.Bridge.HubState

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User

type ServerInput =
    | RemoteServerInput of RemoteServerInput
    | Disconnected

type ConnectionState = {
    ConnectionId : ConnectionId
    AffinityId : AffinityId }

type Subscriptions = {
    HasUsers : bool
    HasChatMessages : bool }

type HubState =
    | NotRegistered
    | Unauth of ConnectionState
    | Auth of ConnectionState * UserId * Subscriptions

let signedIn userId hubStates =
    hubStates
    |> List.exists (fun hubState ->
        match hubState with
        | NotRegistered | Unauth _ -> false
        | Auth(_, otherUserId, _) -> otherUserId = userId)
let signedInDifferentConnection userId connectionId hubStates =
    hubStates
    |> List.exists (fun hubState ->
        match hubState with
        | NotRegistered | Unauth _ -> false
        | Auth(connectionState, otherUserId, _) -> otherUserId = userId && connectionState.ConnectionId <> connectionId)

let sameConnection connectionId hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth connectionState | Auth(connectionState, _, _) -> connectionState.ConnectionId = connectionId
let sameUser userId hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(_, otherUserId, _) -> otherUserId = userId
let sameUserSameAffinityDifferentConnection userId affinityId connectionId hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(connectionState, otherUserId, _) -> otherUserId = userId && connectionState.AffinityId = affinityId && connectionState.ConnectionId <> connectionId
let differentUserHasUsers userId hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(_, otherUserId, subscriptions) when subscriptions.HasUsers -> otherUserId <> userId
    | Auth _ -> false
let hasUsers hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(_, _, subscriptions) when subscriptions.HasUsers -> true
    | Auth _ -> false
let hasChatMessages hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(_, _, subscriptions) when subscriptions.HasChatMessages -> true
    | Auth _ -> false
