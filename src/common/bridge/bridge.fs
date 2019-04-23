module Aornota.Gibet.Common.Bridge

open Aornota.Gibet.Common.Domain.Affinity

open System

type ConnectionId = | ConnectionId of Guid with
    static member Create() = Guid.NewGuid() |> ConnectionId

type RemoteUiInput = // TODO-NMB: All server->ui inputs (e.g. UserSignedIn | UserSignedOut | &c.)...
    | Initialized // sent from Server.Bridge.State.initialize - and used to ensure that UI does not call Bridge.Send prematurely (which can cause "Still in CONNECTING state" websocket errors)
    | Registered of ConnectionId * DateTimeOffset
    // TODO-NMB?...| UserActivity of UserId

type RemoteServerInput = // TODO-NMB: All ui->server [and api->server] inputs (e.g. UserSignedIn | &c.)...
    | Register of AffinityId
    // TODO-NMB?...| UserActivity

let [<Literal>] BRIDGE_ENDPOINT = "/bridge"
