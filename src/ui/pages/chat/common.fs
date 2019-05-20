module Aornota.Gibet.Ui.Pages.Chat.Common

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Shared

open System

type LastTimestampSeenInput =
    | ReadLastTimestampSeenResult of Result<DateTimeOffset, string> option
    | ReadLastTimestampSeenExn of exn
    | WriteLastTimestampSeenOk of unit
    | WriteLastTimestampSeenExn of exn

type GetChatMessagesApiInput =
    | GetChatMessagesResult of Result<(ChatMessage * int * float<second>) list * int * Rvn, string>
    | GetChatMessagesExn of exn

type MoreChatMessagesApiInput =
    | MoreChatMessagesResult of Result<(ChatMessage * int * float<second>) list * Rvn, string>
    | MoreChatMessagesExn of exn

type SendChatMessageApiInput =
    | SendChatMessageResult of Result<unit, string>
    | SendChatMessageExn of exn

type Input =
    | AddMessage of Message // note: handled by Program.State.transition
    | UpdatePageTitle // note: handled by Program.State.transition
    | RemoteChatInput of RemoteChatInput
    | LastTimestampSeenInput of LastTimestampSeenInput
    | UpdateIsCurrentPage of bool
    | ActivityWhenCurrentPage
    | ShowMarkdownSyntaxModal
    | CloseMarkdownSyntaxModal
    | NewChatMessageChanged of string
    | SendChatMessage
    | RemoveChatMessage of ChatMessageId
    | MoreChatMessages of belowOrdinal : int
    | GetChatMessagesApiInput of GetChatMessagesApiInput
    | MoreChatMessagesApiInput of MoreChatMessagesApiInput
    | SendChatMessageApiInput of SendChatMessageApiInput

type PageState = { IsCurrentPage : bool }

type ChatMessageStatus = | MessageReceived of ordinal : int | MessageExpired

type ChatMessageData = ChatMessage * DateTimeOffset * ChatMessageStatus

type ReadyState = {
    LatestTimestampSeen : DateTimeOffset option
    UnseenCount : int
    UnseenTaggedCount : int
    ShowingMarkdownSyntaxModal : bool
    NewChatMessageKey : Guid
    NewChatMessage : string
    NewChatMessageChanged : bool
    SendChatMessageApiStatus : ApiStatus<string> option
    MoreChatMessagesApiStatus : ApiStatus<string> option
    ChatMessagesData : RemoteData<ChatMessageData list * int, string> }

type State =
    | ReadingLastTimestampSeen of PageState
    | Ready of PageState * ReadyState
