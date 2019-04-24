module Aornota.Gibet.Server.Bridge.Hub

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User

open Elmish.Bridge
open System.Data

type ServerInput =
    | RemoteServerInput of RemoteServerInput
    | Disconnected

type ConnectionState = {
    ConnectionId : ConnectionId
    AffinityId : AffinityId }

type HubState =
    | NotRegistered
    | Unauth of ConnectionState // TODO-NMB: Unauthenticated "subscriptions"?...
    | Auth of ConnectionState * UserId * hasUsers : bool

let signedIn hubStates userId =
    hubStates
    |> List.exists (fun hubState ->
        match hubState with
        | NotRegistered | Unauth _ -> false
        | Auth(_, otherUserId, _) -> otherUserId = userId)
let signedInDifferentConnection hubStates (userId, connectionId) =
    hubStates
    |> List.exists (fun hubState ->
        match hubState with
        | NotRegistered | Unauth _ -> false
        | Auth(connectionState, otherUserId, _) -> otherUserId = userId && connectionState.ConnectionId <> connectionId)

let sameConnection connectionId hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth connectionState | Auth(connectionState, _, _) -> connectionState.ConnectionId = connectionId
let sameUserSameAffinityDifferentConnectionSignedIn (userId, affinityId, connectionId) hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(connectionState, otherUserId, _) -> otherUserId = userId && connectionState.AffinityId = affinityId && connectionState.ConnectionId <> connectionId
let differentUserHasUsers userId hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(_, otherUserId, true) -> otherUserId <> userId
    | Auth _ -> false
let hasUsers () hubState =
    match hubState with
    | NotRegistered -> false
    | Unauth _ -> false
    | Auth(_, _, true) -> true
    | Auth _ -> false

let hub =
    ServerHub<HubState, ServerInput, RemoteUiInput>()
        .RegisterServer(RemoteServerInput)
