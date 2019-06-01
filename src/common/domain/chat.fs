module Aornota.Gibet.Common.Domain.Chat

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Rvn
open Aornota.Gibet.Common.UnitsOfMeasure

open System

type ChatMessageId = | ChatMessageId of Guid with static member Create() = ChatMessageId(Guid.NewGuid())

type ChatMessage = {
    ChatMessageId : ChatMessageId
    Rvn : Rvn
    Sender : UserId * UserName
    Payload : Markdown
    ProcessedPayload : Markdown
    TaggedUsers : UserId list
    Edited : bool }

let [<Literal>] private CHAT_MESSAGE_LIFETIME = 24.<hour>

let chatMessageLifetime = ifDebug 0.1<hour> CHAT_MESSAGE_LIFETIME

let validateChatMessage (Markdown payload) =
    if String.IsNullOrWhiteSpace(payload) then Some "Chat message must not be blank"
    else if payload.Trim().Length > 2_000 then Some "Chat message is too long"
    else None
