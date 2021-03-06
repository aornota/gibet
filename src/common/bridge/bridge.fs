module Aornota.Gibet.Common.Bridge

open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Rvn
open Aornota.Gibet.Common.UnitsOfMeasure

open System

type AffinityId = | AffinityId of Guid with static member Create() = AffinityId(Guid.NewGuid())
type ConnectionId = | ConnectionId of Guid with static member Create() = ConnectionId(Guid.NewGuid())

type UserUpdateType =
    | PasswordChanged
    | ImageChanged of ImageChangeType option
    | PasswordReset
    | UserTypeChanged

type RemoteUsersInput =
    | UserActivity of UserId
    | UserSignedIn of UserId
    | UserSignedOut of UserId
    | ForceUserSignOut of ForcedSignOutReason
    | ForceUserChangePassword of byUserName : UserName
    | UserUpdated of User * UserUpdateType * usersRvn : Rvn
    | UserAdded of User * usersRvn : Rvn

type RemoteChatInput =
    | ChatMessageReceived of ConnectionId * ChatMessage * ordinal : int * count : int * key : Guid * chatMessagesRvn : Rvn
    | ChatMessageEdited of ChatMessage * count : int * key : Guid * chatMessagesRvn : Rvn
    | ChatMessageDeleted of ChatMessageId * count : int * key : Guid * chatMessagesRvn : Rvn
    | ChatMessagesExpired of ChatMessageId list * count : int * key : Guid * chatMessagesRvn : Rvn

type RemoteUiInput =
    | Initialized // sent from Server.Bridge.State.initialize - and used to ensure that UI does not call Bridge.Send prematurely (which can cause "Still in CONNECTING state" websocket errors)
    | Registered of ConnectionId * sinceServerStarted : float<second>
    | RemoteUsersInput of RemoteUsersInput
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
