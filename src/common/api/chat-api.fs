module Aornota.Gibet.Common.Api.ChatApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure

type ChatApi = {
    getChatMessages : ConnectionId * Jwt * int -> AsyncResult<(ChatMessage * int * float<second>) list * int * Rvn, string>
    moreChatMessages : Jwt * int * int -> AsyncResult<(ChatMessage * int * float<second>) list * int * Rvn, string>
    sendChatMessage : Jwt * ChatMessage -> AsyncResult<ChatMessageId, string> }
