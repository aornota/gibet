module Aornota.Gibet.Common.Bridge

open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

open System

type ConnectionId = | ConnectionId of Guid with
    static member Create() = Guid.NewGuid() |> ConnectionId

type RemoteUiInput =
    | Initialized // sent from Server.Bridge.State.initialize - and used to ensure that UI does not call Bridge.Send prematurely (which can cause "Still in CONNECTING state" websocket errors)
    | Registered of ConnectionId * serverStarted : DateTimeOffset
    | UserActivity of UserId
    | UserSignedIn of UserId
    | UserSignedOut of UserId
    | ForceUserSignOut of ForcedSignOutReason option
    | UserUpdated of User * usersRvn : Rvn
    | UserAdded of User * usersRvn : Rvn
    // TODO-NMB: More?...
    | UnexpectedServerInput of string

type RemoteServerInput =
    // Sent from UI:
    | Register of AffinityId * ConnectionId option
    | Activity
    // Sent from Server:
    | SignedIn of UserId
    | SignedOut
    | ForceSignOut of ForcedSignOutReason option
    | HasUsers
    // TODO-NMB: More?...

let [<Literal>] BRIDGE_ENDPOINT = "/bridge"
