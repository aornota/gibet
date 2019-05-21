module Aornota.Gibet.Ui.Pages.Chat.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure
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

let [<Literal>] private KEY__LATEST_CHAT_SEEN = "gibet-ui-latest-chat-seen"

// #region hasActivity
let private hasActivity =
#if ACTIVITY
    true
#else
    false
#endif
// #endregion

let private addMessageCmd messageType text = addMessageCmd messageType text (AddMessage >> Cmd.ofMsg)
let private addDebugErrorCmd error = addDebugErrorCmd error (AddMessage >> Cmd.ofMsg)
let private shouldNeverHappenCmd error = shouldNeverHappenCmd error (AddMessage >> Cmd.ofMsg)

let private readLatestChatSeenCmd =
    let readLatestChatSeen () = async {
        (* TEMP-NMB...
        do! ifDebugSleepAsync 250 1000 *)
        return readJson(Key KEY__LATEST_CHAT_SEEN) |> Option.map (fun (Json json) -> Decode.Auto.fromString<Guid * int option> json) }
    Cmd.OfAsync.either readLatestChatSeen () ReadLatestChatSeenResult ReadLatestChatSeenExn |> Cmd.map LatestChatSeenInput
let private writeLatestChatSeenCmd latestChatSeen =
    let writeLatestChatSeen latestChatSeen = async {
        writeJson (Key KEY__LATEST_CHAT_SEEN) (Json(Encode.Auto.toString<Guid * int option>(SPACE_COUNT, latestChatSeen))) }
    Cmd.OfAsync.either writeLatestChatSeen latestChatSeen WriteLatestChatSeenOk WriteLatestChatSeenExn |> Cmd.map LatestChatSeenInput

let private readyState latestChatSeen connectionId authUser =
    let getChatMessagesCmd, chatMessagesData =
        if canGetChatMessages authUser.User.UserType then
            let cmd = Cmd.OfAsync.either chatApi.getChatMessages (connectionId, authUser.Jwt, queryBatchSize) GetChatMessagesResult GetChatMessagesExn |> Cmd.map GetChatMessagesApiInput
            cmd, Pending
        else Cmd.none, Failed NOT_ALLOWED
    let state = {
        LatestChatSeen = latestChatSeen
        UnseenCount = 0
        UnseenTaggedCount = 0
        ShowingMarkdownSyntaxModal = false
        NewChatMessageKey = Guid.NewGuid()
        NewChatMessage = String.Empty
        NewChatMessageChanged = false
        SendChatMessageApiStatus = None
        MoreChatMessagesApiStatus = None
        ChatMessagesData = chatMessagesData }
    state, getChatMessagesCmd

let private chatMessageData chatMessage sinceSent status : ChatMessageData = chatMessage, DateTimeOffset.UtcNow.AddSeconds(float -sinceSent), status

let private exists chatMessageId (chatMessages:ChatMessageData list) = chatMessages |> List.exists (fun (chatMessage, _, _) -> chatMessage.ChatMessageId = chatMessageId)

let private tryFind chatMessageId (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    match chatMessagesData with
    | Received((chatMessages, _), _) -> chatMessages |> List.tryFind (fun (chatMessage, _, _) -> chatMessage.ChatMessageId = chatMessageId)
    | _ -> None
let private addChatMessage (chatMessageData:ChatMessageData) (count:int) chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    match chatMessagesData with
    | Received((chatMessages, _), currentChatMessagesRvn) ->
        match validateNextRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "addChatMessage: %s" error)
        | None ->
            let chatMessage, _, _ = chatMessageData
            if chatMessages |> exists chatMessage.ChatMessageId then Error(sprintf "addChatMessage: %A already exists" chatMessage.ChatMessageId)
            else Ok(Received((chatMessageData :: chatMessages, count), chatMessagesRvn))
    | _ -> Error "addChatMessage: not Received"
let private addChatMessages (chatMessages:ChatMessageData list) chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    let rec add current chatMessages =
        match chatMessages with
        | data :: tail ->
            let chatMessage, _, _ = data
            if current |> exists chatMessage.ChatMessageId then Error(sprintf "addChatMessages: %A already exists" chatMessage.ChatMessageId)
            else add (data :: current) tail
        | _ -> Ok current
    match chatMessagesData with
    | Received((current, count), currentChatMessagesRvn) ->
        match validateSameRvn currentChatMessagesRvn chatMessagesRvn with
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

let private unseenCounts authUser excludeSelf latestChatSeen (key:Guid) (chatMessages:ChatMessageData list) =
    let authUserId = authUser.User.UserId
    let unseen =
        chatMessages
        |> List.filter (fun (chatMessage, _, status) ->
            let (userId, _) = chatMessage.Sender
            if excludeSelf && userId = authUserId then false
            else
                match status with
                | MessageReceived ordinal ->
                    match latestChatSeen with
                    | Some(currentKey, _) when currentKey <> key -> true
                    | Some(_, Some highestOrdinal) when ordinal > highestOrdinal -> true
                    | _ -> false
                | MessageExpired -> false)
    let unseenTagged = unseen |> List.filter (fun (chatMessage, _, _) -> chatMessage.TaggedUsers |> List.contains authUserId)
    unseen.Length, unseenTagged.Length

let private updateCounts isCurrentPage unseen unseenTagged readyState =
    let unseenCount, unseenTaggedCount =
        if not isCurrentPage || hasActivity then readyState.UnseenCount + unseen, readyState.UnseenTaggedCount + unseenTagged
        else readyState.UnseenCount, readyState.UnseenTaggedCount
    let cmd =
        if isCurrentPage && (unseenCount <> readyState.UnseenCount || unseenTaggedCount <> readyState.UnseenTaggedCount) then UpdatePageTitle |> Cmd.ofMsg
        else Cmd.none
    { readyState with UnseenCount = unseenCount ; UnseenTaggedCount = unseenTaggedCount }, cmd

let private updateLatestChatSeen isCurrentPage (key:Guid) (chatMessages:ChatMessageData list) readyState =
    if isCurrentPage then
        let latestChatSeen =
            if chatMessages.Length > 0 then
                let highestOrdinal =
                    chatMessages
                    |> List.choose (fun (_, _, status) -> match status with | MessageReceived ordinal -> Some ordinal | MessageExpired -> None)
                    |> List.max
                Some(key, Some highestOrdinal)
            else Some(key, None)
        let currentLatestChatSeen = readyState.LatestChatSeen
        match latestChatSeen, currentLatestChatSeen with
        | Some latestChatSeen, None -> { readyState with LatestChatSeen = Some latestChatSeen }, writeLatestChatSeenCmd latestChatSeen
        | Some(key, highestOrdinal), Some(currentKey, _) when key <> currentKey ->
            { readyState with LatestChatSeen = Some(key, highestOrdinal) }, writeLatestChatSeenCmd (key, highestOrdinal)
        | Some(key, highestOrdinal), Some(_, currentHighestOrdinal) when highestOrdinal > currentHighestOrdinal ->
            { readyState with LatestChatSeen = Some(key, highestOrdinal) }, writeLatestChatSeenCmd (key, highestOrdinal)
        | _ -> readyState, Cmd.none
    else readyState, Cmd.none

let private handleRemoteChatInput authUser remoteChatInput (pageState, readyState) =
    match remoteChatInput with
    | ChatMessageReceived(chatMessage, ordinal, count, key, chatMessagesRvn) ->
        let chatMessageData = chatMessageData chatMessage 0.<second> (MessageReceived ordinal)
        match readyState.ChatMessagesData |> addChatMessage chatMessageData count chatMessagesRvn with
        | Ok chatMessagesData ->
            let unseen, unseenTagged = [ chatMessageData ] |> unseenCounts authUser true readyState.LatestChatSeen key
            let readyState, updatePageTitleCmd = readyState |> updateCounts pageState.IsCurrentPage unseen unseenTagged
            let readyState, writeLatestChatSeenCmd = readyState |> updateLatestChatSeen pageState.IsCurrentPage key [ chatMessageData ]
            let toastCmd = ifDebug (sprintf "%A received (%i available) -> ChatMessageData now %A" chatMessage.ChatMessageId count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = chatMessagesData }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd; toastCmd ]
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | ChatMessagesExpired(chatMessageIds, count, chatMessagesRvn) ->
        match readyState.ChatMessagesData |> expire chatMessageIds count chatMessagesRvn with
        | Ok chatMessagesData ->
            // Note: No need to worry about unseen counts (&c.) as expired messages are not relevant for these.
            let readyState = { readyState with ChatMessagesData = chatMessagesData }
            Ready(pageState, readyState), ifDebug (sprintf "%A expired (%i available) -> ChatMessageData now %A" chatMessageIds count chatMessagesRvn |> infoToastCmd) Cmd.none
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error

let private handleLatestChatSeenInput connectionId authUser latestChatSeenInput state =
    match latestChatSeenInput, state with
    | ReadLatestChatSeenResult(Some(Ok latestChatSeen)), ReadingLatestChatSeen pageState ->
        let readyState, cmd = readyState (Some latestChatSeen) connectionId authUser
        Ready(pageState, readyState ), cmd
    | ReadLatestChatSeenResult None, ReadingLatestChatSeen pageState ->
        let readyState, cmd = readyState None connectionId authUser
        Ready(pageState, readyState ), cmd
    | ReadLatestChatSeenResult(Some(Error error)), ReadingLatestChatSeen _ ->
        let cmds = Cmd.batch [
            addDebugErrorCmd (sprintf "ReadLatestChatSeenResult error -> %s" error)
            LatestChatSeenInput(ReadLatestChatSeenResult None) |> Cmd.ofMsg ]
        state, cmds
    | ReadLatestChatSeenExn exn, ReadingLatestChatSeen _ -> state, LatestChatSeenInput(ReadLatestChatSeenResult(Some(Error exn.Message))) |> Cmd.ofMsg
    | WriteLatestChatSeenOk _, _ -> state, Cmd.none
    | WriteLatestChatSeenExn exn, _ -> state, addDebugErrorCmd (sprintf "WriteLatestChatSeenExn -> %s" exn.Message)
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState latestChatSeenInput state)

let private handleUpdateIsCurrentPage isCurrentPage state =
    match state with
    | ReadingLatestChatSeen pageState -> ReadingLatestChatSeen { pageState with IsCurrentPage = isCurrentPage }, Cmd.none
    | Ready(pageState, readyState) ->
        let pageState = { pageState with IsCurrentPage = isCurrentPage }
        if isCurrentPage then
            let updatePageTitleCmd = UpdatePageTitle |> Cmd.ofMsg
            let readyState, writeLatestChatSeenCmd =
                match readyState.LatestChatSeen, readyState.ChatMessagesData with
                | Some(key, _), Received((chatMessages, _), _) -> readyState |> updateLatestChatSeen isCurrentPage key chatMessages
                | _ -> readyState, Cmd.none
            let readyState = { readyState with UnseenCount = 0 ; UnseenTaggedCount = 0 }
            Ready(pageState, readyState), Cmd.batch [ writeLatestChatSeenCmd ; updatePageTitleCmd ]
        else Ready(pageState, readyState), Cmd.none

let private handleGetChatMessagesApiInput authUser getChatMessagesApiInput (pageState, readyState) =
    match readyState.ChatMessagesData with
    | Pending ->
        match getChatMessagesApiInput with
        | GetChatMessagesResult(Ok(chatMessages, count, key, chatMessagesRvn)) ->
            let chatMessages = chatMessages |> List.map (fun (chatMessage, ordinal, sinceSent) -> chatMessageData chatMessage sinceSent (MessageReceived ordinal))
            let unseen, unseenTagged = chatMessages |> unseenCounts authUser false readyState.LatestChatSeen key
            let readyState, updatePageTitleCmd = readyState |> updateCounts pageState.IsCurrentPage unseen unseenTagged
            let readyState, writeLatestChatSeenCmd = readyState |> updateLatestChatSeen pageState.IsCurrentPage key chatMessages
            let toastCmd = ifDebug (sprintf "Got %i chat message/s (%i available) -> ChatMessagesData %A" chatMessages.Length count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
        | GetChatMessagesResult(Error error) ->
            Ready(pageState, { readyState with ChatMessagesData = Failed error }), ifDebug (sprintf "GetChatMessagesResult error -> %s" error |> errorToastCmd) Cmd.none
        | GetChatMessagesExn exn -> Ready(pageState, readyState), GetChatMessagesApiInput(GetChatMessagesResult(Error exn.Message)) |> Cmd.ofMsg
    | _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when ChatMessagesData is not Pending (%A)" getChatMessagesApiInput readyState)

let private handleMoreChatMessagesApiInput authUser moreChatMessagesApiInput (pageState, readyState) =
    match readyState.MoreChatMessagesApiStatus with
    | Some ApiPending ->
        match moreChatMessagesApiInput with
        | MoreChatMessagesResult(Ok(chatMessages, key, chatMessagesRvn)) ->
            let chatMessages = chatMessages |> List.map (fun (chatMessage, ordinal, sinceSent) -> chatMessageData chatMessage sinceSent (MessageReceived ordinal))
            match readyState.ChatMessagesData |> addChatMessages chatMessages chatMessagesRvn with
            | Ok chatMessagesData ->
                // Note: Updating unseen counts (&c.) should be superfluous.
                let unseen, unseenTagged = chatMessages |> unseenCounts authUser false readyState.LatestChatSeen key
                let readyState, updatePageTitleCmd = readyState |> updateCounts pageState.IsCurrentPage unseen unseenTagged
                let readyState, writeLatestChatSeenCmd = readyState |> updateLatestChatSeen pageState.IsCurrentPage key chatMessages
                let toastCmd = ifDebug (sprintf "Got %i more chat message/s -> ChatMessagesData %A" chatMessages.Length chatMessagesRvn |> infoToastCmd) Cmd.none
                let readyState = { readyState with MoreChatMessagesApiStatus = None ; ChatMessagesData = chatMessagesData }
                Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
            | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
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

let initialize isCurrentPage (_:AuthUser) = ReadingLatestChatSeen { IsCurrentPage = isCurrentPage }, readLatestChatSeenCmd

let transition connectionId authUser (usersData:RemoteData<UserData list, string>) input state : State * Cmd<Input> =
    match input, state with
    // Note: AddMessage | UpdatePageTitle will have been handled by Program.State.transition.
    | RemoteChatInput remoteChatInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleRemoteChatInput authUser remoteChatInput
    | LatestChatSeenInput latestChatSeenInput, _ -> state |> handleLatestChatSeenInput connectionId authUser latestChatSeenInput
    | UpdateIsCurrentPage isCurrentPage, _ -> state |> handleUpdateIsCurrentPage isCurrentPage
    | ActivityWhenCurrentPage, ReadingLatestChatSeen _ -> state, Cmd.none
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
        | _ -> Ready(pageState, { readyState with NewChatMessage = newChatMessage ; NewChatMessageChanged = true }), Cmd.none
    | SendChatMessage, Ready(pageState, readyState) ->
        match readyState.SendChatMessageApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected SendChatMessage when SendChatMessageApiStatus is Pending"
        | _ ->
            let newChatMessage = Markdown readyState.NewChatMessage
            let newChatMessage, taggedUsers =
                match usersData with
                | Received(users, _) -> users |> processTags newChatMessage
                | _ -> newChatMessage, [] // should never happen
            let chatMessage = {
                ChatMessageId = ChatMessageId.Create()
                Sender = authUser.User.UserId, authUser.User.UserName
                Payload = newChatMessage
                TaggedUsers = taggedUsers }
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
            Ready(pageState, { readyState with MoreChatMessagesApiStatus = Some ApiPending }), cmd
    | GetChatMessagesApiInput getChatMessagesApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleGetChatMessagesApiInput authUser getChatMessagesApiInput
    | MoreChatMessagesApiInput moreChatMessagesApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleMoreChatMessagesApiInput authUser moreChatMessagesApiInput
    | SendChatMessageApiInput sendChatMessageApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleSendChatMessageApiInput sendChatMessageApiInput
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)
