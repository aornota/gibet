module Aornota.Gibet.Common.Bridge

open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User

open System

type ConnectionId = | ConnectionId of Guid with
    static member Create() = Guid.NewGuid() |> ConnectionId

type RemoteUiInput =
    | Initialized // sent from Server.Bridge.State.initialize - and used to ensure that UI does not call Bridge.Send prematurely (which can cause "Still in CONNECTING state" websocket errors)
    | Registered of ConnectionId * serverStarted : DateTimeOffset
    | UserActivity of UserId
    // TODO-NMB...UserSignedIn of UserId
    // TODO-NMB...UserSignedOut of UserId
    // TODO-NMB...ForceUserSignOut of UserId * ForceSignOutReason option (e.g. PasswordReset | UserTypeChanged)
    // TODO-NMB: More server->ui inputs (e.g. UserSignedIn | UserSignedOut | &c.)...

type RemoteServerInput =
    | Register of AffinityId * ConnectionId option
    | Activity
    | SignedIn of UserId
    | SignedOut of UserId
    | HasUsers
    // TODO-NMB...ForceSignOut of ForceSignOutReason option?
    // TODO-NMB: More ui->server [and api->server] inputs (e.g. UserSignedIn | &c.)...

let [<Literal>] BRIDGE_ENDPOINT = "/bridge"
