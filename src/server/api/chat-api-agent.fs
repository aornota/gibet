module Aornota.Gibet.Server.Api.ChatApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Api.ChatApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Jwt

open System
open System.Collections.Generic

open FsToolkit.ErrorHandling

open Serilog

type private Input =
    | GetChatMessages of ConnectionId * Jwt * int option * AsyncReplyChannelResult<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    | MoreChatMessages of Jwt * int * int option * AsyncReplyChannelResult<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    | SendChatMessage of ConnectionId * Jwt * UserId * UserName * Markdown * Markdown * UserId list * AsyncReplyChannelResult<unit, string>
    | EditChatMessage of Jwt * ChatMessageId * UserId * Markdown * Markdown * UserId list * Rvn * AsyncReplyChannelResult<unit, string>
    | DeleteChatMessage of Jwt * ChatMessageId * UserId * bool * Rvn * AsyncReplyChannelResult<unit, string>
    | Housekeeping

type private ChatMessageDict = Dictionary<ChatMessageId, ChatMessage * int * DateTimeOffset>

let [<Literal>] private SOURCE = "Api.ChatApiAgent"

let [<Literal>] private HOUSEKEEPING_INTERVAL = 1.<minute>

let private key = Guid.NewGuid()

let private addChatMessage userId userName payload processedPayload taggedUsers ordinal (chatMessageDict:ChatMessageDict) =
    let chatMessageId = ChatMessageId.Create()
    if chatMessageDict.ContainsKey chatMessageId then Error(ifDebug (sprintf "%s.addChatMessage [%A] -> Unable to add %A" SOURCE key chatMessageId) UNEXPECTED_ERROR)
    else
        let chatMessage = {
            ChatMessageId = chatMessageId
            Rvn = initialRvn
            Sender = userId, userName
            Payload = payload
            ProcessedPayload = processedPayload
            TaggedUsers = taggedUsers
            Edited = false }
        chatMessageDict.Add(chatMessageId, (chatMessage, ordinal, DateTimeOffset.UtcNow))
        Ok chatMessage
let private updateChatMessage chatMessage ordinal timestamp (chatMessageDict:ChatMessageDict) =
    let chatMessageId = chatMessage.ChatMessageId
    if chatMessageDict.ContainsKey chatMessageId then
        chatMessageDict.[chatMessageId] <- (chatMessage, ordinal, timestamp)
        Ok()
    else Error(ifDebug (sprintf "%s.updateChatMessage -> Unable to update %A" SOURCE chatMessageId) UNEXPECTED_ERROR)
let private deleteChatMessage chatMessageId expired (chatMessageDict:ChatMessageDict) =
    if chatMessageDict.ContainsKey chatMessageId && expired then
        Error(ifDebug (sprintf "%s.deleteChatMessage [%A] -> Unable to delete %A (expired but found)" SOURCE key chatMessageId) UNEXPECTED_ERROR)
    else if not (chatMessageDict.ContainsKey chatMessageId) && not expired then
        Error(ifDebug (sprintf "%s.deleteChatMessage [%A] -> Unable to delete %A (not expired but not found)" SOURCE key chatMessageId) UNEXPECTED_ERROR)
    else
        if not expired then chatMessageDict.Remove chatMessageId |> ignore
        Ok()
let private findChatMessage chatMessageId (chatMessageDict:ChatMessageDict) =
    if chatMessageDict.ContainsKey chatMessageId then Ok chatMessageDict.[chatMessageId]
    else Error(ifDebug (sprintf "%s.findChatMessageId -> Unable to find %A" SOURCE chatMessageId) UNEXPECTED_ERROR)

let private chatMessages belowOrdinal batchSize (chatMessageDict:ChatMessageDict) =
    let chatMessages =
        chatMessageDict.Values
        |> List.ofSeq
        |> List.filter (fun (_, ordinal, _) -> match belowOrdinal with | Some belowOrdinal -> ordinal < belowOrdinal | None -> true)
        |> List.sortBy (fun (_, ordinal, _) -> -ordinal)
        |> List.map (fun (chatMessage, ordinal, timestamp) -> chatMessage, ordinal, (DateTimeOffset.UtcNow - timestamp).TotalSeconds * 1.<second>)
    match chatMessages.Length, batchSize with
    | length, Some batchSize when length > batchSize -> chatMessages |> List.take batchSize
    | _ -> chatMessages

type ChatApiAgent(hub:IHub<HubState, RemoteServerInput, RemoteUiInput>, logger:ILogger) =
    let logger = logger |> sourcedLogger SOURCE
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop(chatMessageDict:ChatMessageDict, lastOrdinal, agentRvn) = async {
            let! input = inbox.Receive ()
            (* TEMP-NMB...
            do! ifDebugSleepAsync 250 1000 *)
            match input with
            | GetChatMessages(connectionId, jwt, batchSize, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake GetChatMessages error [%A] -> %A" key jwt) else Ok()
                    let! _, userType = fromJwt jwt
                    let! _ =
                        if canGetChatMessages userType then Ok()
                        else Error(ifDebug (sprintf "%s.GetChatMessages [%A] -> canGetChatMessages returned false for %A" SOURCE key userType) NOT_ALLOWED)
                    let chatMessages = chatMessageDict |> chatMessages None batchSize
                    hub.SendServerIf (sameConnection connectionId) HasChatMessages
                    return chatMessages, chatMessageDict.Count, key, agentRvn }
                match result with
                | Ok (chatMessages, count, _, _) ->
                    logger.Debug("Got {length} chat message/s out of {count} (ChatApiAgent {agentRvn}) [{key}]", chatMessages.Length, count, agentRvn, key)
                | Error error -> logger.Warning("Unable to get chat messages [{key}] -> {error}", key, error)
                reply.Reply result
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | MoreChatMessages(jwt, minOrdinal, batchSize, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake MoreChatMessages error [%A] -> %A" key jwt) else Ok()
                    let! _, userType = fromJwt jwt
                    let! _ =
                        if canGetChatMessages userType then Ok()
                        else Error(ifDebug (sprintf "%s.MoreChatMessages [%A] -> canGetChatMessages returned false for %A" SOURCE key userType) NOT_ALLOWED)
                    let chatMessages = chatMessageDict |> chatMessages (Some minOrdinal) batchSize
                    return chatMessages, chatMessageDict.Count, key, agentRvn }
                match result with
                | Ok (chatMessages, count, _, _) ->
                    logger.Debug("Got {length} more chat message/s out of {count} (ChatApiAgent {agentRvn}) [{key}]", chatMessages.Length, count, agentRvn, key)
                | Error error -> logger.Warning("Unable to get more chat messages [{key}] -> {error}", key, error)
                reply.Reply result
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | SendChatMessage(connectionId, jwt, userId, userName, payload, processedPayload, taggedUsers, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake SendChatMessage error [%A] -> %A" key jwt) else Ok()
                    let! _, userType = fromJwt jwt
                    let! _ =
                        if canSendChatMessage userType then Ok()
                        else Error(ifDebug (sprintf "%s.SendChatMessage [%A] -> canSendChatMessage returned false for %A" SOURCE key userType) NOT_ALLOWED)
                    let lastOrdinal = lastOrdinal + 1
                    let! chatMessage = chatMessageDict |> addChatMessage userId userName payload processedPayload taggedUsers lastOrdinal
                    let agentRvn = incrementRvn agentRvn
                    hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessageReceived(connectionId, chatMessage, lastOrdinal, chatMessageDict.Count, key, agentRvn)))
                    return chatMessage.ChatMessageId, (lastOrdinal, agentRvn) }
                let lastOrdinal, agentRvn =
                    match result with
                    | Ok(chatMessageId, (lastOrdinal, agentRvn)) ->
                        logger.Debug("Sent chat message {chatMessageId} with ordinal {lastOrdinal} (ChatApiAgent now {rvn}) [{key}]", chatMessageId, lastOrdinal, agentRvn, key)
                        lastOrdinal, agentRvn
                    | Error error ->
                        logger.Warning("Unable to send chat message (ChatApiAgent {rvn} unchanged) [{key}] -> {error}", agentRvn, key, error)
                        lastOrdinal, agentRvn
                reply.Reply(result |> ignoreResult)
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | EditChatMessage(jwt, chatMessageId, userId, payload, processedPayload, taggedUsers, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake EditChatMessage error [%A] -> %A" key jwt) else Ok()
                    let! byUserId, userType = fromJwt jwt
                    let! _ =
                        if canEditChatMessage userId (byUserId, userType) then Ok()
                        else Error(ifDebug (sprintf "%s.EditChatMessage [%A] -> canEditChatMessage from %A returned false for %A (%A)" SOURCE key userId byUserId userType) NOT_ALLOWED)
                    let! chatMessage, ordinal, timestamp = chatMessageDict |> findChatMessage chatMessageId
                    let! _ = validateSameRvn chatMessage.Rvn rvn |> errorIfSome ()
                    let chatMessage = { chatMessage with Rvn = incrementRvn rvn ; Payload = payload ; ProcessedPayload = processedPayload ; TaggedUsers = taggedUsers ; Edited = true }
                    let! _ = chatMessageDict |> updateChatMessage chatMessage ordinal timestamp
                    let agentRvn = incrementRvn agentRvn
                    hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessageEdited(chatMessage, chatMessageDict.Count, key, agentRvn)))
                    return chatMessage.ChatMessageId, agentRvn }
                let agentRvn =
                    match result with
                    | Ok(chatMessageId, agentRvn) ->
                        logger.Debug("Edited chat message {chatMessageId} (ChatApiAgent now {rvn}) [{key}]", chatMessageId, agentRvn, key)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to edit chat message (ChatApiAgent {rvn} unchanged) [{key}] -> {error}", agentRvn, key, error)
                        agentRvn
                reply.Reply(result |> ignoreResult)
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | DeleteChatMessage(jwt, chatMessageId, userId, expired, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake DeleteChatMessage error [%A] -> %A" key jwt) else Ok()
                    let! byUserId, userType = fromJwt jwt
                    let! _ =
                        if canDeleteChatMessage userId (byUserId, userType) then Ok()
                        else Error(ifDebug (sprintf "%s.DeleteChatMessage [%A] -> canDeleteChatMessage from %A returned false for %A (%A)" SOURCE key userId byUserId userType) NOT_ALLOWED)
                    let! _ =
                        if not expired then
                            match chatMessageDict |> findChatMessage chatMessageId with
                            | Ok(chatMessage, _, __) -> validateSameRvn chatMessage.Rvn rvn |> errorIfSome ()
                            | Error error -> Error error
                        else Ok()
                    let! _ = chatMessageDict |> deleteChatMessage chatMessageId expired
                    let agentRvn = incrementRvn agentRvn
                    hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessageDeleted(chatMessageId, chatMessageDict.Count, key, agentRvn)))
                    return chatMessageId, agentRvn }
                let agentRvn =
                    match result with
                    | Ok(chatMessageId, agentRvn) ->
                        logger.Debug("Deleted chat message {chatMessageId} (ChatApiAgent now {rvn}) [{key}]", chatMessageId, lastOrdinal, agentRvn, key)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to delete chat message {chatMessageId} (ChatApiAgent {rvn} unchanged) [{key}] -> {error}", chatMessageId, agentRvn, key, error)
                        agentRvn
                reply.Reply(result |> ignoreResult)
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | Housekeeping ->
                logger.Debug("Housekeeping!")
                let expired =
                    chatMessageDict.Values
                    |> List.ofSeq
                    |> List.filter (fun (_, _, timestamp) -> (DateTimeOffset.UtcNow - timestamp).TotalHours * 1.<hour> > chatMessageLifetime)
                    |> List.map (fun (chatMessage, _, _) -> chatMessage.ChatMessageId)
                expired |> List.iter (chatMessageDict.Remove >> ignore)
                let agentRvn = if expired.Length > 0 then incrementRvn agentRvn else agentRvn
                if expired.Length > 0 then hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessagesExpired(expired, chatMessageDict.Count, key, agentRvn)))
                if expired.Length > 0 then logger.Debug("Removed {length} expired messages", expired.Length)
                else logger.Debug("No messages have expired")
                return! loop (chatMessageDict, lastOrdinal, agentRvn) }
        logger.Information("Starting [{key}]...", key)
        let chatMessageDict = ChatMessageDict()
        loop (chatMessageDict, 0, initialRvn))
    do agent.Error.Add (fun exn -> logger.Error("Unexpected error [{key}] -> {errorMessage}", key, exn.Message))
    let rec housekeeping () = async {
        do! Async.Sleep(int (minutesToMilliseconds HOUSEKEEPING_INTERVAL))
        agent.Post(Housekeeping)
        return! housekeeping () }
    do housekeeping () |> Async.Start
    member __.GetChatMessages(connectionId, jwt, batchSize) = agent.PostAndAsyncReply(fun reply -> GetChatMessages(connectionId, jwt, batchSize, reply))
    member __.MoreChatMessages(jwt, belowOrdinal, batchSize) = agent.PostAndAsyncReply(fun reply -> MoreChatMessages(jwt, belowOrdinal, batchSize, reply))
    member __.SendChatMessage(connectionId, jwt, userId, userName, payload, processedPayload, taggedUsers) =
        agent.PostAndAsyncReply(fun reply -> SendChatMessage(connectionId, jwt, userId, userName, payload, processedPayload, taggedUsers, reply))
    member __.EditChatMessage(jwt, chatMessageId, userId, payload, processedPayload, taggedUsers, rvn) =
        agent.PostAndAsyncReply(fun reply -> EditChatMessage(jwt, chatMessageId, userId, payload, processedPayload, taggedUsers, rvn, reply))
    member __.DeleteChatMessage(jwt, chatMessageId, userId, expired, rvn) = agent.PostAndAsyncReply(fun reply -> DeleteChatMessage(jwt, chatMessageId, userId, expired, rvn, reply))

let chatApiReader = reader {
    let! chatApi = resolve<ChatApiAgent>()
    return {
        getChatMessages = chatApi.GetChatMessages
        moreChatMessages = chatApi.MoreChatMessages
        sendChatMessage = chatApi.SendChatMessage
        editChatMessage = chatApi.EditChatMessage
        deleteChatMessage = chatApi.DeleteChatMessage } }
