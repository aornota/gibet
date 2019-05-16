module Aornota.Gibet.Common.Bridge

open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure

open System

type ConnectionId = | ConnectionId of Guid with static member Create() = ConnectionId(Guid.NewGuid())

type UserUpdateType =
    | PasswordChanged
    | ImageChanged of ImageChangeType option
    | PasswordReset
    | UserTypeChanged

type RemoteChatInput =
    | ChatMessageReceived of ChatMessage * int * float<second> * count : int * chatMessagesRvn : Rvn
    | ChatMessagesExpired of ChatMessageId list * count : int * chatMessagesRvn : Rvn

type RemoteUiInput =
    | Initialized // sent from Server.Bridge.State.initialize - and used to ensure that UI does not call Bridge.Send prematurely (which can cause "Still in CONNECTING state" websocket errors)
    | Registered of ConnectionId * sinceServerStarted : float<second>
    | UserActivity of UserId
    | UserSignedIn of UserId
    | UserSignedOut of UserId
    | ForceUserSignOut of ForcedSignOutReason
    | ForceUserChangePassword of byUserName : UserName
    | UserUpdated of User * usersRvn : Rvn * UserUpdateType
    | UserAdded of User * usersRvn : Rvn
    | RemoteChatInput of RemoteChatInput
    | UnexpectedServerInput of string

type RemoteServerInput =
    // Sent from UI:
    | Register of AffinityId * ConnectionId option
    | Activity
    // Sent from Server:
    | SignedIn of UserId
    | SignedOut
    | ForceSignOut of ForcedSignOutReason
    | ForceChangePassword of byUserName : UserName
    | HasUsers
    | HasChatMessages

let [<Literal>] BRIDGE_ENDPOINT = "/bridge"
