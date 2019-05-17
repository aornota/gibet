module Aornota.Gibet.Ui.Pages.Chat.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Ui.Common.LocalStorage
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.ServerApi
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System

open Elmish

open Thoth.Json

let [<Literal>] private KEY__CHAT_LATEST_TIMESTAMP_SEEN = "gibet-ui-chat-latest-timestamp-seen"

let [<Literal>] private QUERY_BATCH_SIZE = 10

// TODO-NMB: Test query batch size (and None)...
let private queryBatchSize = ifDebug None (Some QUERY_BATCH_SIZE)

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

let private readyState latestTimestampSeen connectionId authUser =
    let getChatMessagesCmd =
        Cmd.OfAsync.either chatApi.getChatMessages (connectionId, authUser.Jwt, queryBatchSize) GetChatMessagesResult GetChatMessagesExn |> Cmd.map GetChatMessagesApiInput
    let state = {
        LatestTimestampSeen = latestTimestampSeen
        UnseenCount = 0
        UnseenTaggedCount = 0
        ShowingMarkdownSyntaxModal = false
        NewChatMessageKey = Guid.NewGuid()
        NewChatMessage = String.Empty
        NewChatMessageChanged = false
        SendChatMessageApiStatus = None
        MoreChatMessagesApiStatus = None
        ChatMessagesData = Pending }
    state, getChatMessagesCmd

let private chatMessageData chatMessage sinceSent status : ChatMessageData = chatMessage, DateTimeOffset.UtcNow.AddSeconds(float -sinceSent), status

let private exists chatMessageId (chatMessages:ChatMessageData list) = chatMessages |> List.exists (fun (chatMessage, _, _) -> chatMessage.ChatMessageId = chatMessageId)

let private tryFind chatMessageId (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    match chatMessagesData with | Received((chatMessages, _), _) -> chatMessages |> tryFindChatMessage chatMessageId | _ -> None
// TODO-NMB: Rework for ChatMessageReceived vs. MoreChatMessagesResult...
let private addChatMessages (chatMessages:ChatMessageData list) count chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    let rec add current chatMessages =
        match chatMessages with
        | data :: tail ->
            let chatMessage, _, _ = data
            if current |> exists chatMessage.ChatMessageId then Error(sprintf "addChatMessages: %A already exists" chatMessage.ChatMessageId)
            else add (data :: current) tail
        | _ -> Ok current
    match chatMessagesData with
    | Received((current, _), currentChatMessagesRvn) ->
        match validateNextRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "addChatMessages: %s" error)
        | None ->
            match add current chatMessages with
            | Ok chatMessages -> Ok(Received((chatMessages, count), chatMessagesRvn))
            | Error error -> Error error
    | _ -> Error "addChatMessages: not Received"
let private expire chatMessageIds count chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) = // note: silently ignore unknown chatMessageIds
    match chatMessagesData with
    | Received((chatMessages, _), currentChatMessagesRvn) ->
        match validateNextRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "expireChatMessages: %s" error)
        | None ->
            let chatMessages =
                chatMessages
                |> List.map (fun (chatMessage, timestamp, status) ->
                    if chatMessageIds |> List.contains chatMessage.ChatMessageId then chatMessage, timestamp, MessageExpired
                    else chatMessage, timestamp, status)
            Ok(Received((chatMessages, count), chatMessagesRvn))
    | _ -> Error "expireChatMessage: not Received"
let private remove chatMessageId (chatMessagesData:RemoteData<ChatMessageData list * int, string>) = // note: silently ignore unknown chatMessageId
    match chatMessagesData with
    | Received((chatMessages, count), chatMessagesRvn) ->
        let chatMessages =
            chatMessages
            |> List.choose (fun (chatMessage, timestamp, status) ->
                if chatMessage.ChatMessageId = chatMessageId then None
                else Some(chatMessage, timestamp, status))
        Ok(Received((chatMessages, count), chatMessagesRvn))
    | _ -> Error "removeChatMessage: not Received"

let private handleRemoteChatInput remoteChatInput (pageState, readyState) = // TODO-NMB: See inside...
    match remoteChatInput with
    | ChatMessageReceived(chatMessage, ordinal, sinceSent, count, chatMessagesRvn) ->
        let chatMessageData = chatMessageData chatMessage sinceSent (MessageReceived ordinal)
        match readyState.ChatMessagesData |> addChatMessages [ chatMessageData ] count chatMessagesRvn with
        | Ok chatMessagesData ->
            let readyState = { readyState with ChatMessagesData = chatMessagesData }
            // TODO-NMB: Update unseen counts and page title if (not IsCurrentPage || ACTIVITY)?...
            Ready(pageState, readyState), ifDebug (sprintf "%A received (%i available) -> ChatMessageData now %A" chatMessage.ChatMessageId count chatMessagesRvn |> infoToastCmd) Cmd.none
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | ChatMessagesExpired(chatMessageIds, count, chatMessagesRvn) ->
        match readyState.ChatMessagesData |> expire chatMessageIds count chatMessagesRvn with
        | Ok chatMessagesData ->
            let readyState = { readyState with ChatMessagesData = chatMessagesData }
            Ready(pageState, readyState), ifDebug (sprintf "%A expired (%i available) -> ChatMessageData now %A" chatMessageIds count chatMessagesRvn |> infoToastCmd) Cmd.none
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error

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

let private handleUpdateIsCurrentPage isCurrentPage state = // TODO-NMB: See inside...
    match state with
    | ReadingLastTimestampSeen pageState -> ReadingLastTimestampSeen { pageState with IsCurrentPage = isCurrentPage }, Cmd.none
    | Ready(pageState, readyState) ->
        let pageState = { pageState with IsCurrentPage = isCurrentPage }
        if isCurrentPage then
            let readyState = { readyState with UnseenCount = 0 ; UnseenTaggedCount = 0 }
            // TODO-NMB: Also update LatestTimestampSeen (from ChatMessageData) - and persist to local storage if changed [and not None]?...
            Ready(pageState, readyState), Cmd.none
        else Ready(pageState, readyState), Cmd.none

let private handleGetChatMessagesApiInput getChatMessagesApiInput (pageState, readyState) =
    match readyState.ChatMessagesData with
    | Pending ->
        match getChatMessagesApiInput with
        | GetChatMessagesResult(Ok(chatMessages, count, chatMessagesRvn)) ->
            let chatMessageDatas = chatMessages |> List.map (fun (chatMessage, ordinal, sinceSent) -> chatMessageData chatMessage sinceSent (MessageReceived ordinal))
            let toastCmd = ifDebug (sprintf "Got %i chat message/s (%i available) -> ChatMessagesData %A" chatMessageDatas.Length count chatMessagesRvn |> infoToastCmd) Cmd.none
            Ready(pageState, { readyState with ChatMessagesData = Received((chatMessageDatas, count), chatMessagesRvn) }), toastCmd
        | GetChatMessagesResult(Error error) ->
            Ready(pageState, { readyState with ChatMessagesData = Failed error }), ifDebug (sprintf "GetChatMessagesResult error -> %s" error |> errorToastCmd) Cmd.none
        | GetChatMessagesExn exn -> Ready(pageState, readyState), GetChatMessagesApiInput(GetChatMessagesResult(Error exn.Message)) |> Cmd.ofMsg
    | _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when ChatMessagesData is not Pending (%A)" getChatMessagesApiInput readyState)

let private handleMoreChatMessagesApiInput moreChatMessagesApiInput (pageState, readyState) =
    match readyState.MoreChatMessagesApiStatus with
    | Some ApiPending ->
        match moreChatMessagesApiInput with
        | MoreChatMessagesResult(Ok(chatMessages, chatMessagesRvn)) ->
            // TODO-NMB: Needs to be handled differently (cf. ChatMessageReceived) - plus (maybe) update unseen counts and page title if (not IsCurrentPage || ACTIVITY)?...
            (* let count = 0 // TEMP-NMB...
            let chatMessages = chatMessages |> List.map (fun (chatMessage, ordinal, sinceSent) -> chatMessageData chatMessage sinceSent (MessageReceived ordinal))
            let toastCmd = ifDebug (sprintf "Got %i more chat message/s -> ChatMessagesData %A" chatMessages.Length chatMessagesRvn |> infoToastCmd) Cmd.none
            Ready(pageState, { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }), toastCmd *)
            Ready(pageState, readyState), Cmd.none
        | MoreChatMessagesResult(Error error) ->
            let cmd = ifDebug (sprintf "MoreChatMessagesResult error -> %s" error |> errorToastCmd) Cmd.none
            Ready(pageState, { readyState with MoreChatMessagesApiStatus = Some(ApiFailed error) }), cmd
        | MoreChatMessagesExn exn -> Ready(pageState, readyState), MoreChatMessagesApiInput(MoreChatMessagesResult(Error exn.Message)) |> Cmd.ofMsg
    | _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when MoreChatMessagesApiStatus is not Pending (%A)" moreChatMessagesApiInput readyState)

let private handleSendChatMessageApiInput sendChatMessageApiInput (pageState, readyState) =
    match readyState.SendChatMessageApiStatus with
    | Some ApiPending ->
        match sendChatMessageApiInput with
        | SendChatMessageResult(Ok _) ->
            let readyState = { readyState with NewChatMessageKey = Guid.NewGuid() ; NewChatMessage = String.Empty ; NewChatMessageChanged = false ; SendChatMessageApiStatus = None }
            Ready(pageState, readyState), "Chat message sent" |> successToastCmd
        | SendChatMessageResult(Error error) ->
            let cmd = ifDebug (sprintf "SendChatMessageResult error -> %s" error |> errorToastCmd) Cmd.none
            Ready(pageState, { readyState with SendChatMessageApiStatus = Some(ApiFailed error) }), cmd
        | SendChatMessageExn exn -> Ready(pageState, readyState), SendChatMessageApiInput(SendChatMessageResult(Error exn.Message)) |> Cmd.ofMsg
    | _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when SendChatMessageApiStatus is not Pending (%A)" sendChatMessageApiInput readyState)

let initialize isCurrentPage (_:AuthUser) = ReadingLastTimestampSeen { IsCurrentPage = isCurrentPage }, readLastTimestampSeenCmd

let transition connectionId authUser (usersData:RemoteData<UserData list, string>) input state : State * Cmd<Input> =
    match input, state with
    // Note: AddMessage | UpdatePageTitle will have been handled by Program.State.transition.
    | RemoteChatInput remoteChatInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleRemoteChatInput remoteChatInput
    | LastTimestampSeenInput lastTimestampSeenInput, _ -> state |> handleLastTimestampSeenInput connectionId authUser lastTimestampSeenInput
    | UpdateIsCurrentPage isCurrentPage, _ -> state |> handleUpdateIsCurrentPage isCurrentPage
    | ActivityWhenCurrentPage, ReadingLastTimestampSeen _ -> state, Cmd.none
    | ActivityWhenCurrentPage, Ready({ IsCurrentPage = true }, _) -> state |> handleUpdateIsCurrentPage true
    | ShowMarkdownSyntaxModal, Ready(pageState, readyState) ->
        if not readyState.ShowingMarkdownSyntaxModal then Ready(pageState, { readyState with ShowingMarkdownSyntaxModal = true }), Cmd.none
        else state, shouldNeverHappenCmd "Unexpected ShowMarkdownSyntaxModal when ShowingMarkdownSyntaxModal"
    | CloseMarkdownSyntaxModal, Ready(pageState, readyState) ->
        if readyState.ShowingMarkdownSyntaxModal then Ready(pageState, { readyState with ShowingMarkdownSyntaxModal = false }), Cmd.none
        else state, shouldNeverHappenCmd "Unexpected CloseMarkdownSyntaxModal when not ShowingMarkdownSyntaxModal"
    | NewChatMessageChanged newChatMessage, Ready(pageState, readyState) ->
        match readyState.SendChatMessageApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected NewChatMessageChanged when SendChatMessageApiStatus is Pending"
        | _ -> Ready(pageState, { readyState with NewChatMessage = newChatMessage }), Cmd.none
    | SendChatMessage, Ready(pageState, readyState) -> // TODO-NMB: See inside...
        match readyState.SendChatMessageApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected SendChatMessage when SendChatMessageApiStatus is Pending"
        | _ ->
            // TODO-NMB: Parse (and modify) readyState.NewChatMessage to get TaggedUsers...
            let chatMessage = {
                ChatMessageId = ChatMessageId.Create()
                Sender = authUser.User.UserId, authUser.User.UserName
                Payload = Markdown readyState.NewChatMessage
                TaggedUsers = [] }
            let cmd = Cmd.OfAsync.either chatApi.sendChatMessage (authUser.Jwt, chatMessage) SendChatMessageResult SendChatMessageExn |> Cmd.map SendChatMessageApiInput
            Ready(pageState, { readyState with SendChatMessageApiStatus = Some ApiPending }), cmd
    | RemoveChatMessage chatMessageId, Ready(pageState, readyState) ->
        match readyState.ChatMessagesData |> remove chatMessageId with
        | Ok chatMessagesData -> Ready(pageState, { readyState with ChatMessagesData = chatMessagesData }), Cmd.none
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | MoreChatMessages belowOrdinal, Ready(pageState, readyState) ->
        match readyState.MoreChatMessagesApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected MoreChatMessages when MoreChatMessagesApiStatus is Pending"
        | _ ->
            let cmd =
                Cmd.OfAsync.either chatApi.moreChatMessages (authUser.Jwt, belowOrdinal, queryBatchSize) MoreChatMessagesResult MoreChatMessagesExn |> Cmd.map MoreChatMessagesApiInput
            Ready(pageState, { readyState with MoreChatMessagesApiStatus = Some ApiPending }), Cmd.none
    | GetChatMessagesApiInput getChatMessagesApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleGetChatMessagesApiInput getChatMessagesApiInput
    | MoreChatMessagesApiInput moreChatMessagesApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleMoreChatMessagesApiInput moreChatMessagesApiInput
    | SendChatMessageApiInput sendChatMessageApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleSendChatMessageApiInput sendChatMessageApiInput
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)
