module Aornota.Gibet.Common.Domain.Chat

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.UnitsOfMeasure

open System

type ChatMessageId = | ChatMessageId of Guid with static member Create() = ChatMessageId(Guid.NewGuid())

type ChatMessage = {
    ChatMessageId : ChatMessageId
    Sender : UserId * UserName
    Payload : Markdown
    Tagged : UserId list }

let [<Literal>] CHAT_MESSAGE_LIFETIME = 0.083<hour> // TODO-NMB: Change to, e.g., 24.<hour>...

let validateChatMessage (Markdown payload) =
    if String.IsNullOrWhiteSpace payload then Some "Chat message must not be blank"
    else if payload.Trim().Length > 2_000 then Some "Chat message is too long"
    else None
