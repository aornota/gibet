module Aornota.Gibet.Ui.Pages.Chat.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.LocalStorage
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.ServerApi
open Aornota.Gibet.Ui.Shared

open System

open Elmish

open Thoth.Json

let [<Literal>] private KEY__CHAT_LATEST_TIMESTAMP_SEEN = "gibet-ui-chat-latest-timestamp-seen"

let [<Literal>] private BATCH_SIZE = 10

let private addMessageCmd messageType text = addMessageCmd messageType text (AddMessage >> Cmd.ofMsg)
let private addDebugErrorCmd error = addDebugErrorCmd error (AddMessage >> Cmd.ofMsg)
let private shouldNeverHappenCmd error = shouldNeverHappenCmd error (AddMessage >> Cmd.ofMsg)

let private readLastTimestampSeenCmd =
    let readLastTimestampSeen () = async {
        (* TEMP-NMB...
        do! ifDebugSleepAsync 250 1000 *)
        return readJson(Key KEY__CHAT_LATEST_TIMESTAMP_SEEN) |> Option.map (fun (Json json) -> Decode.Auto.fromString<DateTimeOffset> json) }
    Cmd.OfAsync.either readLastTimestampSeen () ReadLastTimestampSeenResult ReadLastTimestampSeenExn |> Cmd.map LastTimestampSeenInput
let private writeLastTimestampSeenCmd latestTimestampSeen =
    let writeLastTimestampSeen latestTimestampSeen = async {
        writeJson (Key KEY__CHAT_LATEST_TIMESTAMP_SEEN) (Json(Encode.Auto.toString<DateTimeOffset>(SPACE_COUNT, latestTimestampSeen))) }
    Cmd.OfAsync.either writeLastTimestampSeen latestTimestampSeen WriteLastTimestampSeenOk WriteLastTimestampSeenExn |> Cmd.map LastTimestampSeenInput

let private getChatMessagesCmd connection jwt max =
    Cmd.OfAsync.either chatApi.getChatMessages (connection, jwt, max) GetChatMessagesResult GetChatMessagesExn |> Cmd.map GetChatMessagesInput
let private moreChatMessagesCmd jwt belowOrdinal max =
    Cmd.OfAsync.either chatApi.moreChatMessages (jwt, belowOrdinal, max) GetChatMessagesResult GetChatMessagesExn |> Cmd.map MoreChatMessagesInput
let private sendChatMessageCmd jwt chatMessage =
    Cmd.OfAsync.either chatApi.sendChatMessage (jwt, chatMessage) SendChatMessageResult SendChatMessageExn |> Cmd.map SendChatMessageInput

let private readyState latestTimestampSeen connectionId authUser =
    let cmd = getChatMessagesCmd connectionId authUser.Jwt BATCH_SIZE
    let state = {
        LatestTimestampSeen = latestTimestampSeen
        UnseenCount = 0
        UnseenTaggedCount = 0
        NewChatMessageKey = Guid.NewGuid()
        NewChatMessage = String.Empty
        NewChatMessageChanged = false
        SendChatMessageApiStatus = None
        MoreChatMessagesApiStatus = None
        ChatMessagesData = Pending }
    state, cmd

let private handleRemoteChatInput remoteChatInput (pageState, readyState) =
    match remoteChatInput with
    | ChatMessageReceived(chatMessage, ordinal, sinceSent, count, chatMessagesRvn) ->
        // TODO-NMB: cf. UserAdded - and update unseen counts [and page title] if (not IsCurrentPage || ACTIVITY)?...
        Ready(pageState, readyState), Cmd.none
    | ChatMessagesExpired(chatMessageIds, count, chatMessagesRvn) ->
        // TODO-NMB: cf. UserSignedIn (&c.) - but no need to update unseen counts or update page title?...
        Ready(pageState, readyState), Cmd.none

let private handleLastTimestampSeenInput connectionId authUser lastTimestampSeenInput state =
    match lastTimestampSeenInput, state with
    | ReadLastTimestampSeenResult(Some(Ok latestTimestampSeen)), ReadingLastTimestampSeen pageState ->
        let readyState, cmd = readyState (Some latestTimestampSeen) connectionId authUser
        Ready(pageState, readyState ), cmd
    | ReadLastTimestampSeenResult None, ReadingLastTimestampSeen pageState ->
        let readyState, cmd = readyState None connectionId authUser
        Ready(pageState, readyState ), cmd
    | ReadLastTimestampSeenResult(Some(Error error)), ReadingLastTimestampSeen _ ->
        let cmds = Cmd.batch [
            addDebugErrorCmd (sprintf "ReadLastTimestampSeenResult error -> %s" error)
            LastTimestampSeenInput(ReadLastTimestampSeenResult None) |> Cmd.ofMsg ]
        state, cmds
    | ReadLastTimestampSeenExn exn, ReadingLastTimestampSeen _ -> state, LastTimestampSeenInput(ReadLastTimestampSeenResult(Some(Error exn.Message))) |> Cmd.ofMsg
    | WriteLastTimestampSeenOk _, _ -> state, Cmd.none
    | WriteLastTimestampSeenExn exn, _ -> state, addDebugErrorCmd (sprintf "WriteLastTimestampSeenExn -> %s" exn.Message)
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState lastTimestampSeenInput state)

let private handleUpdateIsCurrentPage isCurrentPage state =
    match state with
    | ReadingLastTimestampSeen pageState -> ReadingLastTimestampSeen { pageState with IsCurrentPage = isCurrentPage }, Cmd.none
    | Ready(pageState, readyState) ->
        let pageState = { pageState with IsCurrentPage = isCurrentPage }
        if isCurrentPage then
            let readyState = { readyState with UnseenCount = 0 ; UnseenTaggedCount = 0 }
            // TODO-NMB: Also update LatestTimestampSeen (from ChatMessageData) - and persist to local storage if changed [and not None]?...
            Ready(pageState, readyState), Cmd.none
        else Ready(pageState, readyState), Cmd.none

// TODO-NMB...

let initialize isCurrentPage (_:AuthUser) = ReadingLastTimestampSeen { IsCurrentPage = isCurrentPage }, readLastTimestampSeenCmd

let transition connectionId (authUser:AuthUser) input state : State * Cmd<Input> =
    match input, state with
    // Note: AddMessage | UpdatePageTitle will have been handled by Program.State.transition.
    | RemoteChatInput remoteChatInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleRemoteChatInput remoteChatInput
    | LastTimestampSeenInput lastTimestampSeenInput, _ -> state |> handleLastTimestampSeenInput connectionId authUser lastTimestampSeenInput
    | UpdateIsCurrentPage isCurrentPage, _ -> state |> handleUpdateIsCurrentPage isCurrentPage
    | ActivityWhenCurrentPage, ReadingLastTimestampSeen _ -> state, Cmd.none
    | ActivityWhenCurrentPage, Ready({ IsCurrentPage = true }, _) -> state |> handleUpdateIsCurrentPage true
    (*
    // TODO-NMB...| ShowMarkdownSyntaxModal, Ready(pageState, readyState) ->
    // TODO-NMB...| NewChatMessageChanged newChatMessage, Ready(pageState, readyState) ->
    // TODO-NMB...| SendChatMessage, Ready(pageState, readyState) ->
    // TODO-NMB...| DismissChatMessage chatMessageID, Ready(pageState, readyState) ->
    // TODO-NMB...| MoreChatMessages, Ready(pageState, readyState) ->
    // TODO-NMB...| GetChatMessagesInput getChatMessagesInput, Ready(pageState, readyState) ->
    // TODO-NMB...| MoreChatMessagesInput getChatMessagesInput, Ready(pageState, readyState) ->
    // TODO-NMB...| SendChatMessageInput sendChatMessageInput, Ready(pageState, readyState) ->
    *)
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)
