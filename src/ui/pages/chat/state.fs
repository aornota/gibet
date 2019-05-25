module Aornota.Gibet.Ui.Pages.Chat.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.LocalStorage
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Pages.Chat.ChatApi
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Shared

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
        EditChatMessageModalState = None
        DeleteChatMessageModalState = None
        ChatMessagesData = chatMessagesData }
    state, getChatMessagesCmd

let private chatMessageData chatMessage sinceSent status : ChatMessageData = chatMessage, DateTimeOffset.UtcNow.AddSeconds(float -sinceSent), status

let private chatMessageExists chatMessageId (chatMessages:ChatMessageData list) = match chatMessages |> tryFindChatMessage chatMessageId with | Some _ -> true | None -> false

let private addChatMessage (chatMessageData:ChatMessageData) chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    match chatMessagesData with
    | Received((chatMessages, _), currentChatMessagesRvn) ->
        match validateNextRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "addChatMessage: %s" error)
        | None ->
            let chatMessage, _, _ = chatMessageData
            if chatMessages |> chatMessageExists chatMessage.ChatMessageId then Error(sprintf "addChatMessage: %A already exists" chatMessage.ChatMessageId)
            else Ok(chatMessageData :: chatMessages)
    | _ -> Error "addChatMessage: not Received"
let private addChatMessages (chatMessages:ChatMessageData list) chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    let rec add current (chatMessages:ChatMessageData list) =
        match chatMessages with
        | data :: tail ->
            let chatMessage, _, _ = data
            if current |> chatMessageExists chatMessage.ChatMessageId then Error(sprintf "addChatMessages: %A already exists" chatMessage.ChatMessageId)
            else add (data :: current) tail
        | _ -> Ok current
    match chatMessagesData with
    | Received((current, count), currentChatMessagesRvn) ->
        match validateSameRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "addChatMessages: %s" error)
        | None ->
            match add current chatMessages with
            | Ok chatMessages -> Ok chatMessages
            | Error error -> Error error
    | _ -> Error "addChatMessages: not Received"
let private editChatMessage (chatMessage:ChatMessage) chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) =
    match chatMessagesData with
    | Received((chatMessages, _), currentChatMessagesRvn) ->
        match validateNextRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "editChatMessage: %s" error)
        | None ->
            let chatMessageId = chatMessage.ChatMessageId
            match chatMessages |> tryFindChatMessage chatMessageId with
            | Some _ ->
                let chatMessages =
                    chatMessages
                    |> List.map (fun (currentChatMessage, timestamp, status) ->
                        if currentChatMessage.ChatMessageId = chatMessageId then chatMessage, timestamp, status
                        else currentChatMessage, timestamp, status)
                Ok chatMessages
            | None -> Error(sprintf "editChatMessage: %A already exists" chatMessageId)
    | _ -> Error "editChatMessage: not Received"
let private deleteChatMessage chatMessageId chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) = // note: silently ignore unknown chatMessageId
    match chatMessagesData with
    | Received((chatMessages, _), currentChatMessagesRvn) ->
        match validateNextRvn currentChatMessagesRvn chatMessagesRvn with
        | Some error -> Error(sprintf "deleteChatMessage: %s" error)
        | None ->
            let chatMessages =
                chatMessages
                |> List.choose (fun (chatMessage, timestamp, status) ->
                    if chatMessage.ChatMessageId = chatMessageId then None
                    else Some(chatMessage, timestamp, status))
            Ok chatMessages
    | _ -> Error "deleteChatMessage: not Received"
let private expireChatMessages chatMessageIds chatMessagesRvn (chatMessagesData:RemoteData<ChatMessageData list * int, string>) = // note: silently ignore unknown chatMessageIds
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
            Ok chatMessages
    | _ -> Error "expireChatMessages: not Received"

let private removeChatMessages chatMessageIds (chatMessagesData:RemoteData<ChatMessageData list * int, string>) = // note: silently ignore unknown chatMessageIds
    match chatMessagesData with
    | Received((chatMessages, count), chatMessagesRvn) ->
        let chatMessages =
            chatMessages
            |> List.choose (fun (chatMessage, timestamp, status) ->
                if chatMessageIds |> List.contains chatMessage.ChatMessageId then None
                else Some(chatMessage, timestamp, status))
        Ok(Received((chatMessages, count), chatMessagesRvn))
    | _ -> Error "removeChatMessages: not Received"

let private updateUnseenCounts authUser (key:Guid) (chatMessages:ChatMessageData list) isCurrentPage readyState =
    let unseen =
        chatMessages
        |> List.filter (fun (_, _, status) ->
            match status with
            | MessageReceived ordinal ->
                match readyState.LatestChatSeen with
                | Some(currentKey, _) when currentKey <> key -> true
                | Some(_, Some highestOrdinal) when ordinal > highestOrdinal -> true
                | Some(_, None) -> true
                | None -> true
                | _ -> false
            | MessageExpired -> false)
    let unseenTagged = unseen |> List.filter (fun (chatMessage, _, _) -> chatMessage.TaggedUsers |> List.contains authUser.User.UserId)
    let unseenCount, unseenTaggedCount =
        if not isCurrentPage || hasActivity then unseen.Length, unseenTagged.Length
        else readyState.UnseenCount, readyState.UnseenTaggedCount
    let cmd =
        if isCurrentPage && (unseenCount <> readyState.UnseenCount || unseenTaggedCount <> readyState.UnseenTaggedCount) then UpdatePageTitle |> Cmd.ofMsg
        else Cmd.none
    { readyState with UnseenCount = unseenCount ; UnseenTaggedCount = unseenTaggedCount }, cmd
let private initializeLatestChatSeen (key:Guid) readyState =
    match readyState.LatestChatSeen with
    | Some(currentKey, _) when currentKey = key -> readyState, Cmd.none
    | _ ->
        let latestChatSeen = key, None
        { readyState with LatestChatSeen = Some latestChatSeen }, writeLatestChatSeenCmd latestChatSeen
let private updateLatestChatSeen readyState =
    match readyState.LatestChatSeen, readyState.ChatMessagesData with
    | Some(key, _), Received((chatMessages, _), _) ->
        let latestChatSeen =
            let ordinals = chatMessages |> List.choose (fun (_, _, status) -> match status with | MessageReceived ordinal -> Some ordinal | MessageExpired -> None)
            if ordinals.Length > 0 then Some(key, Some(ordinals |> List.max))
            else Some(key, None)
        let currentLatestChatSeen = readyState.LatestChatSeen
        match latestChatSeen, currentLatestChatSeen with
        | Some latestChatSeen, None ->
            { readyState with LatestChatSeen = Some latestChatSeen }, writeLatestChatSeenCmd latestChatSeen
        | Some(key, highestOrdinal), Some(currentKey, _) when key <> currentKey ->
            { readyState with LatestChatSeen = Some(key, highestOrdinal) }, writeLatestChatSeenCmd (key, highestOrdinal)
        | Some(key, highestOrdinal), Some(_, currentHighestOrdinal) when highestOrdinal > currentHighestOrdinal ->
            { readyState with LatestChatSeen = Some(key, highestOrdinal) }, writeLatestChatSeenCmd (key, highestOrdinal)
        | _ -> readyState, Cmd.none
    | _ -> readyState, Cmd.none

let private editChatMessageModalState chatMessageId rvn (Markdown newChatMessage) = {
    ForChatMessage = chatMessageId, rvn
    NewChatMessageKey = Guid.NewGuid()
    NewChatMessage = newChatMessage
    NewChatMessageChanged = false
    EditChatMessageApiStatus = None }

let private deleteChatMessageModalState chatMessageId rvn = {
    ForChatMessage = chatMessageId, rvn
    DeleteChatMessageApiStatus = None }

let private handleRemoteChatInput connectionId authUser remoteChatInput (pageState, readyState) =
    match remoteChatInput with
    | ChatMessageReceived(fromConnectionId, chatMessage, ordinal, count, key, chatMessagesRvn) ->
        let chatMessageData = chatMessageData chatMessage 0.<second> (MessageReceived ordinal)
        match readyState.ChatMessagesData |> addChatMessage chatMessageData chatMessagesRvn with
        | Ok chatMessages ->
            let readyState, updatePageTitleCmd, writeLatestChatSeenCmd =
                if fromConnectionId = connectionId then readyState, Cmd.none, Cmd.none
                else
                    let readyState, updatePageTitleCmd = readyState |> updateUnseenCounts authUser key chatMessages pageState.IsCurrentPage
                    // Note: Call to initializeLatestChatSeen should be superfluous.
                    let readyState, writeLatestChatSeenCmd = readyState |> initializeLatestChatSeen key
                    readyState, updatePageTitleCmd, writeLatestChatSeenCmd
            let toastCmd = ifDebug (sprintf "%A received (%i available) -> ChatMessageData now %A" chatMessage.ChatMessageId count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | ChatMessageEdited(chatMessage, count, key, chatMessagesRvn) ->
        match readyState.ChatMessagesData |> editChatMessage chatMessage chatMessagesRvn with
        | Ok chatMessages ->
            let readyState, updatePageTitleCmd = readyState |> updateUnseenCounts authUser key chatMessages pageState.IsCurrentPage
            // Note: Call to initializeLatestChatSeen should be superfluous.
            let readyState, writeLatestChatSeenCmd = readyState |> initializeLatestChatSeen key
            let toastCmd = ifDebug (sprintf "%A edited (%i available) -> ChatMessageData now %A" chatMessage.ChatMessageId count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | ChatMessageDeleted(chatMessageId, count, key, chatMessagesRvn) ->
        match readyState.ChatMessagesData |> deleteChatMessage chatMessageId chatMessagesRvn with
        | Ok chatMessages ->
            let readyState, updatePageTitleCmd = readyState |> updateUnseenCounts authUser key chatMessages pageState.IsCurrentPage
            // Note: Call to initializeLatestChatSeen should be superfluous.
            let readyState, writeLatestChatSeenCmd = readyState |> initializeLatestChatSeen key
            let toastCmd = ifDebug (sprintf "%A deleted (%i available) -> ChatMessageData now %A" chatMessageId count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | ChatMessagesExpired(chatMessageIds, count, key, chatMessagesRvn) ->
        match readyState.ChatMessagesData |> expireChatMessages chatMessageIds chatMessagesRvn with
        | Ok chatMessages ->
            let readyState, updatePageTitleCmd = readyState |> updateUnseenCounts authUser key chatMessages pageState.IsCurrentPage
            // Note: Call to initializeLatestChatSeen should be superfluous.
            let readyState, writeLatestChatSeenCmd = readyState |> initializeLatestChatSeen key
            let toastCmd = ifDebug (sprintf "%A expired (%i available) -> ChatMessageData now %A" chatMessageIds count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
            Ready(pageState, readyState),  Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error

let private handleLatestChatSeenInput connectionId authUser latestChatSeenInput state =
    match latestChatSeenInput, state with
    | ReadLatestChatSeenResult(Some(Ok latestChatSeen)), ReadingLatestChatSeen pageState ->
        let readyState, cmd = readyState (Some latestChatSeen) connectionId authUser
        Ready(pageState, readyState), cmd
    | ReadLatestChatSeenResult None, ReadingLatestChatSeen pageState ->
        let readyState, cmd = readyState None connectionId authUser
        Ready(pageState, readyState), cmd
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
            let readyState, writeLatestChatSeenCmd = readyState |> updateLatestChatSeen
            let readyState = { readyState with UnseenCount = 0 ; UnseenTaggedCount = 0 }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ]
        else Ready(pageState, readyState), Cmd.none

let private handleGetChatMessagesApiInput authUser getChatMessagesApiInput (pageState, readyState) =
    match readyState.ChatMessagesData with
    | Pending ->
        match getChatMessagesApiInput with
        | GetChatMessagesResult(Ok(chatMessages, count, key, chatMessagesRvn)) ->
            let chatMessages = chatMessages |> List.map (fun (chatMessage, ordinal, sinceSent) -> chatMessageData chatMessage sinceSent (MessageReceived ordinal))
            let readyState, updatePageTitleCmd = readyState |> updateUnseenCounts authUser key chatMessages pageState.IsCurrentPage
            let readyState, writeLatestChatSeenCmd = readyState |> initializeLatestChatSeen key
            let toastCmd = ifDebug (sprintf "Got %i chat message/s (%i available) -> ChatMessagesData %A" chatMessages.Length count chatMessagesRvn |> infoToastCmd) Cmd.none
            let readyState = { readyState with ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
            Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
        | GetChatMessagesResult(Error error) ->
            Ready(pageState, { readyState with ChatMessagesData = Failed error }), ifDebug (sprintf "GetChatMessagesResult error -> %s" error |> errorToastCmd) Cmd.none
        | GetChatMessagesExn exn -> Ready(pageState, readyState), GetChatMessagesApiInput(GetChatMessagesResult(Error exn.Message)) |> Cmd.ofMsg
    | _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when ChatMessagesData is not Pending (%A)" getChatMessagesApiInput readyState)

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

let private handleMoreChatMessagesApiInput authUser moreChatMessagesApiInput (pageState, readyState) =
    match readyState.MoreChatMessagesApiStatus with
    | Some ApiPending ->
        match moreChatMessagesApiInput with
        | MoreChatMessagesResult(Ok(chatMessages, count, key, chatMessagesRvn)) ->
            let chatMessages = chatMessages |> List.map (fun (chatMessage, ordinal, sinceSent) -> chatMessageData chatMessage sinceSent (MessageReceived ordinal))
            let chatMessagesCount = chatMessages.Length
            match readyState.ChatMessagesData |> addChatMessages chatMessages chatMessagesRvn with
            | Ok chatMessages ->
                // Note: Callw to updateUnseenCounts and initializeLatestChatSeen should be superfluous.
                let readyState, updatePageTitleCmd = readyState |> updateUnseenCounts authUser key chatMessages pageState.IsCurrentPage
                let readyState, writeLatestChatSeenCmd = readyState |> initializeLatestChatSeen key
                let toastCmd = ifDebug (sprintf "Got %i more chat message/s -> ChatMessagesData %A" chatMessagesCount chatMessagesRvn |> infoToastCmd) Cmd.none
                let readyState = { readyState with MoreChatMessagesApiStatus = None ; ChatMessagesData = Received((chatMessages, count), chatMessagesRvn) }
                Ready(pageState, readyState), Cmd.batch [ updatePageTitleCmd ; writeLatestChatSeenCmd ; toastCmd ]
            | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
        | MoreChatMessagesResult(Error error) ->
            let cmd = ifDebug (sprintf "MoreChatMessagesResult error -> %s" error |> errorToastCmd) Cmd.none
            Ready(pageState, { readyState with MoreChatMessagesApiStatus = Some(ApiFailed error) }), cmd
        | MoreChatMessagesExn exn -> Ready(pageState, readyState), MoreChatMessagesApiInput(MoreChatMessagesResult(Error exn.Message)) |> Cmd.ofMsg
    | _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when MoreChatMessagesApiStatus is not Pending (%A)" moreChatMessagesApiInput readyState)

let private handleEditChatMessageModalInput authUser usersData editChatMessageModalInput (pageState, readyState) =
    match readyState.EditChatMessageModalState with
    | Some editChatMessageModalState ->
        match editChatMessageModalInput, editChatMessageModalState.EditChatMessageApiStatus with
        | _, Some ApiPending ->
            let cmd = shouldNeverHappenCmd (sprintf "Unexpected %A when EditChatMessageModalState.EditChatMessageApiStatus is Pending (%A)" editChatMessageModalInput readyState)
            Ready(pageState, readyState), cmd
        | EditChatMessageModalInput.NewChatMessageChanged newChatMessage, _ ->
            let editChatMessageModalState = { editChatMessageModalState with NewChatMessage = newChatMessage ; NewChatMessageChanged = true }
            let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
            Ready(pageState, readyState), Cmd.none
        | EditChatMessage, _ ->
            match readyState.ChatMessagesData with
            | Received((chatMessages, _), _) ->
                let chatMessageId, _ = editChatMessageModalState.ForChatMessage
                match chatMessages |> tryFindChatMessage chatMessageId with
                | Some(chatMessage, _, _) ->
                    let userId, _ = chatMessage.Sender
                    let payload = Markdown editChatMessageModalState.NewChatMessage
                    let processedPayload, taggedUsers =
                        match usersData with
                        | Received(users, _) -> users |> processTags payload
                        | _ -> payload, [] // should never happen
                    let cmd =
                        Cmd.OfAsync.either chatApi.editChatMessage (authUser.Jwt, chatMessageId, userId, payload, processedPayload, taggedUsers, chatMessage.Rvn) EditChatMessageResult
                            EditChatMessageExn |> Cmd.map EditChatMessageApiInput
                    let editChatMessageModalState = { editChatMessageModalState with EditChatMessageApiStatus = Some ApiPending }
                    let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
                    Ready(pageState, readyState), cmd
                | None ->
                    let cmd = shouldNeverHappenCmd (sprintf "Unexpected EditChatMessage when %A not found in ChatMessagesData (%A)" chatMessageId readyState)
                    let editChatMessageModalState = { editChatMessageModalState with EditChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                    let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
                    Ready(pageState, readyState), cmd
            | _ ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected EditChatMessage when ChatMessagesData not Received (%A)" readyState)
                let editChatMessageModalState = { editChatMessageModalState with EditChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
                Ready(pageState, readyState), cmd
        | CloseEditChatMessageModal, _ ->
            let readyState = { readyState with EditChatMessageModalState = None }
            Ready(pageState, readyState), Cmd.none
    | None -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when EditChatMessageModalState is None (%A)" editChatMessageModalInput readyState)
let private handleEditChatMessageApiInput editChatMessageApiInput (pageState, readyState) =
    match readyState.EditChatMessageModalState with
    | Some editChatMessageModalState ->
        match editChatMessageModalState.EditChatMessageApiStatus with
        | Some ApiPending ->
            match editChatMessageApiInput with
            | EditChatMessageResult(Ok _) ->
                let readyState = { readyState with EditChatMessageModalState = None }
                Ready(pageState, readyState), "Chat message edited" |> successToastCmd
            | EditChatMessageResult(Error error) ->
                let editChatMessageModalState = { editChatMessageModalState with EditChatMessageApiStatus = Some(ApiFailed error) }
                let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
                Ready(pageState, readyState), Cmd.none // no need for toast (since error will be displayed on EditChatMessageModal)
            | EditChatMessageExn exn -> Ready(pageState, readyState), EditChatMessageApiInput(EditChatMessageResult(Error exn.Message)) |> Cmd.ofMsg
        | _ ->
            let cmd = shouldNeverHappenCmd (sprintf "Unexpected %A when EditChatMessageModalState.EditChatMessageApiStatus is not Pending (%A)" editChatMessageApiInput readyState)
            Ready(pageState, readyState), cmd
    | None -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when EditChatMessageModalState is None (%A)" editChatMessageApiInput readyState)

let private handleDeleteChatMessageModalInput authUser deleteChatMessageModalInput (pageState, readyState) =
    match readyState.DeleteChatMessageModalState with
    | Some deleteChatMessageModalState ->
        match deleteChatMessageModalInput, deleteChatMessageModalState.DeleteChatMessageApiStatus with
        | _, Some ApiPending ->
            let cmd = shouldNeverHappenCmd (sprintf "Unexpected %A when DeleteChatMessageModalState.DeleteChatMessageApiStatus is Pending (%A)" deleteChatMessageModalInput readyState)
            Ready(pageState, readyState), cmd
        | DeleteChatMessage, _ ->
            match readyState.ChatMessagesData with
            | Received((chatMessages, _), _) ->
                let chatMessageId, _ = deleteChatMessageModalState.ForChatMessage
                match chatMessages |> tryFindChatMessage chatMessageId with
                | Some(chatMessage, _, status) ->
                    let userId, _ = chatMessage.Sender
                    let cmd =
                        Cmd.OfAsync.either chatApi.deleteChatMessage (authUser.Jwt, chatMessageId, userId, expired status, chatMessage.Rvn) DeleteChatMessageResult DeleteChatMessageExn
                        |> Cmd.map DeleteChatMessageApiInput
                    let deleteChatMessageModalState = { deleteChatMessageModalState with DeleteChatMessageApiStatus = Some ApiPending }
                    let readyState = { readyState with DeleteChatMessageModalState = Some deleteChatMessageModalState }
                    Ready(pageState, readyState), cmd
                | None ->
                    let cmd = shouldNeverHappenCmd (sprintf "Unexpected DeleteChatMessage when %A not found in ChatMessagesData (%A)" chatMessageId readyState)
                    let deleteChatMessageModalState = { deleteChatMessageModalState with DeleteChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                    let readyState = { readyState with DeleteChatMessageModalState = Some deleteChatMessageModalState }
                    Ready(pageState, readyState), cmd
            | _ ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected DeleteChatMessage when ChatMessagesData not Received (%A)" readyState)
                let deleteChatMessageModalState = { deleteChatMessageModalState with DeleteChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                let readyState = { readyState with DeleteChatMessageModalState = Some deleteChatMessageModalState }
                Ready(pageState, readyState), cmd
        | CloseDeleteChatMessageModal, _ ->
            let readyState = { readyState with DeleteChatMessageModalState = None }
            Ready(pageState, readyState), Cmd.none
    | None -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when DeleteChatMessageModalState is None (%A)" deleteChatMessageModalInput readyState)
let private handleDeleteChatMessageApiInput deleteChatMessageApiInput (pageState, readyState) =
    match readyState.DeleteChatMessageModalState with
    | Some deleteChatMessageModalState ->
        match deleteChatMessageModalState.DeleteChatMessageApiStatus with
        | Some ApiPending ->
            match deleteChatMessageApiInput with
            | DeleteChatMessageResult(Ok _) ->
                let readyState = { readyState with DeleteChatMessageModalState = None }
                Ready(pageState, readyState), "Chat message deleted" |> successToastCmd
            | DeleteChatMessageResult(Error error) ->
                let deleteChatMessageModalState = { deleteChatMessageModalState with DeleteChatMessageApiStatus = Some(ApiFailed error) }
                let readyState = { readyState with DeleteChatMessageModalState = Some deleteChatMessageModalState }
                Ready(pageState, readyState), Cmd.none // no need for toast (since error will be displayed on DeleteChatMessageModal)
            | DeleteChatMessageExn exn -> Ready(pageState, readyState), DeleteChatMessageApiInput(DeleteChatMessageResult(Error exn.Message)) |> Cmd.ofMsg
        | _ ->
            let cmd = shouldNeverHappenCmd (sprintf "Unexpected %A when DeleteChatMessageModalState.DeleteChatMessageApiStatus is not Pending (%A)" deleteChatMessageApiInput readyState)
            Ready(pageState, readyState), cmd
    | None -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected %A when DeleteChatMessageModalState is None (%A)" deleteChatMessageApiInput readyState)

let initialize isCurrentPage (_:AuthUser) = ReadingLatestChatSeen { IsCurrentPage = isCurrentPage }, readLatestChatSeenCmd

let transition connectionId authUser usersData input state =
    match input, state with
    // Note: AddMessage | UpdatePageTitle will have been handled by Program.State.transition.
    | RemoteChatInput remoteChatInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleRemoteChatInput connectionId authUser remoteChatInput
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
    | GetChatMessagesApiInput getChatMessagesApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleGetChatMessagesApiInput authUser getChatMessagesApiInput
    | NewChatMessageChanged newChatMessage, Ready(pageState, readyState) ->
        match readyState.SendChatMessageApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected NewChatMessageChanged when SendChatMessageApiStatus is Pending"
        | _ -> Ready(pageState, { readyState with NewChatMessage = newChatMessage ; NewChatMessageChanged = true }), Cmd.none
    | SendChatMessage, Ready(pageState, readyState) ->
        match readyState.SendChatMessageApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected SendChatMessage when SendChatMessageApiStatus is Pending"
        | _ ->
            let payload = Markdown readyState.NewChatMessage
            let processedPayload, taggedUsers =
                match usersData with
                | Received(users, _) -> users |> processTags payload
                | _ -> payload, [] // should never happen
            let cmd =
                Cmd.OfAsync.either chatApi.sendChatMessage (connectionId, authUser.Jwt, authUser.User.UserId, authUser.User.UserName, payload, processedPayload, taggedUsers)
                    SendChatMessageResult SendChatMessageExn |> Cmd.map SendChatMessageApiInput
            Ready(pageState, { readyState with SendChatMessageApiStatus = Some ApiPending }), cmd
    | SendChatMessageApiInput sendChatMessageApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleSendChatMessageApiInput sendChatMessageApiInput
    | RemoveExpiredChatMessages chatMessageIds, Ready(pageState, readyState) ->
        match readyState.ChatMessagesData |> removeChatMessages chatMessageIds with
        | Ok chatMessagesData -> Ready(pageState, { readyState with ChatMessagesData = chatMessagesData }), Cmd.none
        | Error error -> Ready(pageState, readyState), shouldNeverHappenCmd error
    | MoreChatMessages belowOrdinal, Ready(pageState, readyState) ->
        match readyState.MoreChatMessagesApiStatus with
        | Some ApiPending -> Ready(pageState, readyState), shouldNeverHappenCmd "Unexpected MoreChatMessages when MoreChatMessagesApiStatus is Pending"
        | _ ->
            let cmd =
                Cmd.OfAsync.either chatApi.moreChatMessages (authUser.Jwt, belowOrdinal, queryBatchSize) MoreChatMessagesResult MoreChatMessagesExn |> Cmd.map MoreChatMessagesApiInput
            Ready(pageState, { readyState with MoreChatMessagesApiStatus = Some ApiPending }), cmd
    | MoreChatMessagesApiInput moreChatMessagesApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleMoreChatMessagesApiInput authUser moreChatMessagesApiInput
    | ShowEditChatMessageModal chatMessageId, Ready(pageState, readyState) ->
        match readyState.EditChatMessageModalState with
        | Some _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected ShowEditChatMessageModal when EditChatMessageModalState is not None (%A)" readyState)
        | None ->
            match readyState.ChatMessagesData with
            | Received((chatMessages, _), _) ->
                match chatMessages |> tryFindChatMessage chatMessageId with
                | Some(chatMessage, _, _) ->
                    let readyState = { readyState with EditChatMessageModalState = Some(editChatMessageModalState chatMessageId chatMessage.Rvn chatMessage.Payload) }
                    Ready(pageState, readyState), Cmd.none
                | None ->
                    let cmd = shouldNeverHappenCmd (sprintf "Unexpected ShowEditChatMessageModal when %A not found in ChatMessagesData (%A)" chatMessageId readyState)
                    let editChatMessageModalState = editChatMessageModalState chatMessageId initialRvn (Markdown UNEXPECTED_ERROR)
                    let editChatMessageModalState = { editChatMessageModalState with EditChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                    let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
                    Ready(pageState, readyState), cmd
            | _ ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected ShowEditChatMessageModal when ChatMessagesData not Received (%A)" readyState)
                let editChatMessageModalState = editChatMessageModalState chatMessageId initialRvn (Markdown UNEXPECTED_ERROR)
                let editChatMessageModalState = { editChatMessageModalState with EditChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                let readyState = { readyState with EditChatMessageModalState = Some editChatMessageModalState }
                Ready(pageState, readyState), cmd
    | EditChatMessageModalInput editChatMessageModalInput, Ready(pageState, readyState) ->
        (pageState, readyState) |> handleEditChatMessageModalInput authUser usersData editChatMessageModalInput
    | EditChatMessageApiInput editChatMessageApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleEditChatMessageApiInput editChatMessageApiInput
    | ShowDeleteChatMessageModal chatMessageId, Ready(pageState, readyState) ->
        match readyState.DeleteChatMessageModalState with
        | Some _ -> Ready(pageState, readyState), shouldNeverHappenCmd (sprintf "Unexpected ShowDeleteChatMessageModal when DeleteChatMessageModalState is not None (%A)" readyState)
        | None ->
            match readyState.ChatMessagesData with
            | Received((chatMessages, _), _) ->
                match chatMessages |> tryFindChatMessage chatMessageId with
                | Some(chatMessage, _, _) ->
                    let readyState = { readyState with DeleteChatMessageModalState = Some(deleteChatMessageModalState chatMessageId chatMessage.Rvn) }
                    Ready(pageState, readyState), Cmd.none
                | None ->
                    let cmd = shouldNeverHappenCmd (sprintf "Unexpected ShowDeleteChatMessageModal when %A not found in ChatMessagesData (%A)" chatMessageId readyState)
                    let deleteChatMessageModalState = deleteChatMessageModalState chatMessageId initialRvn
                    let deleteChatMessageModalState = { deleteChatMessageModalState with DeleteChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                    let readyState = { readyState with DeleteChatMessageModalState = Some deleteChatMessageModalState }
                    Ready(pageState, readyState), cmd
            | _ ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected ShowDeleteChatMessageModal when ChatMessagesData not Received (%A)" readyState)
                let deleteChatMessageModalState = deleteChatMessageModalState chatMessageId initialRvn
                let deleteChatMessageModalState = { deleteChatMessageModalState with DeleteChatMessageApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                let readyState = { readyState with DeleteChatMessageModalState = Some deleteChatMessageModalState }
                Ready(pageState, readyState), cmd
    | DeleteChatMessageModalInput deleteChatMessageModalInput, Ready(pageState, readyState) ->
        (pageState, readyState) |> handleDeleteChatMessageModalInput authUser deleteChatMessageModalInput
    | DeleteChatMessageApiInput deleteChatMessageApiInput, Ready(pageState, readyState) -> (pageState, readyState) |> handleDeleteChatMessageApiInput deleteChatMessageApiInput
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)
