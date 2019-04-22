module Aornota.Gibet.Common.Bridge

open Aornota.Gibet.Common.Domain.Affinity

open System

type ConnectionId = | ConnectionId of Guid with
    static member Create() = Guid.NewGuid() |> ConnectionId

type Connection = ConnectionId * AffinityId

type RemoteUiInput = // TODO-NMB: All server->ui inputs (e.g. UserSignedIn | UserSignedOut | &c.)...
    | Connected of ConnectionId
    // TODO-NMB?...| UserActivity of UserId
    | ToDo

type RemoteServerInput = // TODO-NMB: All ui->server [and api->server] inputs (e.g. UserSignedIn | &c.)...
    | Connect of AffinityId
    | Disconnected
    // TODO-NMB?...| UserActivity
    | ToDo

let [<Literal>] BRIDGE_ENDPOINT = "/socket"
