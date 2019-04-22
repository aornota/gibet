module Aornota.Gibet.Server.Bridge.Hub

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User

open Elmish.Bridge

type ServerInput =
    | RemoteServer of RemoteServerInput
    | Disconnected

// TODO-NMB?...type UnauthSubscriptions = ...

type AuthSubscriptions = {
    HasUsers : bool } // TODO-NMB: More?...

type ConnectionState = {
    ConnectionId : ConnectionId
    AffinityId : AffinityId
    // TODO-NMB?...UnauthSubscriptions : UnauthSubscriptions
    User : (UserId * AuthSubscriptions) option }

type HubState =
    | NotConnected
    | Connected of ConnectionState

let hub =
    ServerHub<HubState, ServerInput, RemoteUiInput>()
        .RegisterServer(RemoteServer)
