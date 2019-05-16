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

type GetChatMessagesInput =
    | GetChatMessagesResult of Result<(ChatMessage * int * float<second>) list * int * Rvn, string>
    | GetChatMessagesExn of exn

type SendChatMessageInput =
    | SendChatMessageResult of Result<ChatMessageId, string>
    | SendChatMessageExn of exn

type Input =
    | AddMessage of Message // note: handled by Program.State.transition
    | UpdatePageTitle // note: handled by Program.State.transition
    | RemoteChatInput of RemoteChatInput
    | LastTimestampSeenInput of LastTimestampSeenInput
    | UpdateIsCurrentPage of bool
    | ActivityWhenCurrentPage
    | ShowMarkdownSyntaxModal
    | NewChatMessageChanged of string
    | SendChatMessage
    | DismissChatMessage of ChatMessageId
    | MoreChatMessages
    | GetChatMessagesInput of GetChatMessagesInput
    | MoreChatMessagesInput of GetChatMessagesInput // intentionally also uses GetChatMessagesInput
    | SendChatMessageInput of SendChatMessageInput

type PageState = { IsCurrentPage : bool }

type ReadyState = {
    LatestTimestampSeen : DateTimeOffset option
    UnseenCount : int
    UnseenTaggedCount : int
    NewChatMessageKey : Guid
    NewChatMessage : string
    NewChatMessageChanged : bool
    SendChatMessageApiStatus : ApiStatus<string> option
    MoreChatMessagesApiStatus : ApiStatus<string> option
    ChatMessagesData : RemoteData<(ChatMessage * int * DateTimeOffset * bool) list * int * Rvn, string> }

type State =
    | ReadingLastTimestampSeen of PageState
    | Ready of PageState * ReadyState
