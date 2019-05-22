module Aornota.Gibet.Common.Api.ChatApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure

open System

type ChatApi = {
    getChatMessages : ConnectionId * Jwt * int option -> AsyncResult<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    moreChatMessages : Jwt * int * int option -> AsyncResult<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    sendChatMessage : Jwt * UserId * UserName * Markdown * UserId list -> AsyncResult<unit, string>
    editChatMessage : Jwt * ChatMessageId * UserId * Markdown * UserId list * Rvn -> AsyncResult<unit, string>
    deleteChatMessage : Jwt * ChatMessageId * UserId * bool * Rvn -> AsyncResult<unit, string> }
