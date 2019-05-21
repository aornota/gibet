module Aornota.Gibet.Ui.Pages.Chat.Common

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System
open System.Text.RegularExpressions

type LatestChatSeenInput =
    | ReadLatestChatSeenResult of Result<Guid * int option, string> option
    | ReadLatestChatSeenExn of exn
    | WriteLatestChatSeenOk of unit
    | WriteLatestChatSeenExn of exn

type GetChatMessagesApiInput =
    | GetChatMessagesResult of Result<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    | GetChatMessagesExn of exn

type MoreChatMessagesApiInput =
    | MoreChatMessagesResult of Result<(ChatMessage * int * float<second>) list * Guid * Rvn, string>
    | MoreChatMessagesExn of exn

type SendChatMessageApiInput =
    | SendChatMessageResult of Result<unit, string>
    | SendChatMessageExn of exn

type Input =
    | AddMessage of Message // note: handled by Program.State.transition
    | UpdatePageTitle // note: handled by Program.State.transition
    | RemoteChatInput of RemoteChatInput
    | LatestChatSeenInput of LatestChatSeenInput
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
    LatestChatSeen : (Guid * int option) option
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
    | ReadingLatestChatSeen of PageState
    | Ready of PageState * ReadyState

let [<Literal>] private QUERY_BATCH_SIZE = 10

let queryBatchSize = ifDebug None (Some QUERY_BATCH_SIZE)

let processTags (Markdown payload) (users:UserData list) =
    // Note: Differs from ..\..\..\dev-console\test-regex.fs because Regex behaviour is different once transpiled with Fable.
    let matches = Regex.Matches(payload, "(@\w+)|(@{[^}]*})")
    let tags = [
        for i in 0 .. matches.Count - 1 do
            let value = matches.[i].Value
            if not (String.IsNullOrWhiteSpace value) then
                if value.StartsWith("@{") then yield value, value.Substring(2, value.Length - 3)
                else yield value, value.Substring 1 ]
    let replacer (payload:string, taggedUsers) (replace, userName) =
        match users |> List.filter (fun (user, _, _) -> user.UserName = UserName userName && user.UserType <> PersonaNonGrata) with
        | [ (user, _, _) ] -> payload.Replace(replace, sprintf "**%s**" userName), user.UserId :: taggedUsers
        | _ :: _ | [] -> payload.Replace(replace, sprintf "_%s_" replace), taggedUsers
    let payload, taggedUsers = tags |> List.fold replacer (payload, [])
    Markdown payload, taggedUsers
