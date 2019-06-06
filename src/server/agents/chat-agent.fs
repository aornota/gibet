module Aornota.Gibet.Server.Agents.ChatAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Api.ChatApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Chat
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Jwt
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.Rvn
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Server.Authenticator
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.SourcedLogger

open System
open System.Collections.Generic

open FsToolkit.ErrorHandling

type private Input =
    | GetChatMessages of ConnectionId * Jwt * int option * AsyncReplyChannelResult<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    | MoreChatMessages of Jwt * int * int option * AsyncReplyChannelResult<(ChatMessage * int * float<second>) list * int * Guid * Rvn, string>
    | SendChatMessage of ConnectionId * Jwt * UserId * UserName * Markdown * Markdown * UserId list * AsyncReplyChannelResult<unit, string>
    | EditChatMessage of Jwt * ChatMessageId * UserId * Markdown * Markdown * UserId list * Rvn * AsyncReplyChannelResult<unit, string>
    | DeleteChatMessage of Jwt * ChatMessageId * UserId * bool * Rvn * AsyncReplyChannelResult<unit, string>
    | Housekeeping

type private ChatMessageDict = Dictionary<ChatMessageId, ChatMessage * int * DateTimeOffset>

let [<Literal>] private SOURCE = "Agents.ChatAgent"

let [<Literal>] private HOUSEKEEPING_INTERVAL = 1.<minute>

let private key = Guid.NewGuid()

let private findChatMessage chatMessageId (chatMessageDict:ChatMessageDict) =
    if chatMessageDict.ContainsKey chatMessageId then Ok chatMessageDict.[chatMessageId]
    else Error(ifDebug (sprintf "Unable to find %A" chatMessageId) UNEXPECTED_ERROR)
let private addChatMessage chatMessage ordinal (chatMessageDict:ChatMessageDict) =
    let chatMessageId = chatMessage.ChatMessageId
    if chatMessageDict.ContainsKey chatMessageId then Error(ifDebug (sprintf "%A already exists" chatMessageId) UNEXPECTED_ERROR)
    else
        chatMessageDict.Add(chatMessageId, (chatMessage, ordinal, DateTimeOffset.UtcNow))
        Ok()
let private updateChatMessage chatMessage (chatMessageDict:ChatMessageDict) =
    let chatMessageId = chatMessage.ChatMessageId
    result {
        let! _, ordinal, timestamp = chatMessageDict |> findChatMessage chatMessageId
        chatMessageDict.[chatMessageId] <- (chatMessage, ordinal, timestamp)
        return () }
let private deleteChatMessage chatMessageId expired (chatMessageDict:ChatMessageDict) =
    if chatMessageDict.ContainsKey chatMessageId && expired then Error(ifDebug (sprintf "Unable to delete %A (expired but found)" chatMessageId) UNEXPECTED_ERROR)
    else if not (chatMessageDict.ContainsKey chatMessageId) && not expired then Error(ifDebug (sprintf "Unable to delete %A (not expired but not found)" chatMessageId) UNEXPECTED_ERROR)
    else
        if not expired then chatMessageDict.Remove chatMessageId |> ignore
        Ok()

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

type ChatAgent(hub:IHub<HubState, RemoteServerInput, RemoteUiInput>, authenticator:Authenticator, logger) =
    let sourcedLogger, logger = logger |> sourcedLogger SOURCE, logger
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop(chatMessageDict:ChatMessageDict, lastOrdinal, agentRvn) = async {
            let! input = inbox.Receive()
            (* TEMP-NMB...
            do! ifDebugSleepAsync 250 1_000 *)
            match input with
            | GetChatMessages(connectionId, jwt, batchSize, reply) ->
                let chatMessagesPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake GetChatMessages error" else Ok()
                    let! _, userType = authenticator.FromJwt(jwt)
                    let! _ = if canGetChatMessages userType then Ok() else Error(ifDebug (sprintf "canGetChatMessages returned false for %A" userType) NOT_ALLOWED)
                    let chatMessages = chatMessageDict |> chatMessages None batchSize
                    hub.SendServerIf (sameConnection connectionId) HasChatMessages
                    return chatMessages, chatMessageDict.Count, key, agentRvn }
                match chatMessagesPlusResult with
                | Ok(chatMessages, count, _, _) -> sourcedLogger.Debug("Got {length} ChatMessage/s out of {count} (ChatAgent {agentRvn})", chatMessages.Length, count, agentRvn)
                | Error error -> sourcedLogger.Warning("Unable to get ChatMessages -> {error}", error)
                reply.Reply chatMessagesPlusResult
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | MoreChatMessages(jwt, minOrdinal, batchSize, reply) ->
                let chatMessagesPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake MoreChatMessages error" else Ok()
                    let! _, userType = authenticator.FromJwt(jwt)
                    let! _ = if canGetChatMessages userType then Ok() else Error(ifDebug (sprintf "canGetChatMessages returned false for %A" userType) NOT_ALLOWED)
                    let chatMessages = chatMessageDict |> chatMessages (Some minOrdinal) batchSize
                    return chatMessages, chatMessageDict.Count, key, agentRvn }
                match chatMessagesPlusResult with
                | Ok(chatMessages, count, _, _) -> sourcedLogger.Debug("Got {length} more ChatMessage/s out of {count} (ChatAgent {agentRvn})", chatMessages.Length, count, agentRvn)
                | Error error -> sourcedLogger.Warning("Unable to get more ChatMessages -> {error}", error)
                reply.Reply chatMessagesPlusResult
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | SendChatMessage(connectionId, jwt, userId, userName, payload, processedPayload, taggedUsers, reply) ->
                let chatMessageIdPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake SendChatMessage error" else Ok()
                    let! _, userType = authenticator.FromJwt(jwt)
                    let! _ = if canSendChatMessage userType then Ok() else Error(ifDebug (sprintf "canSendChatMessage returned false for %A" userType) NOT_ALLOWED)
                    let lastOrdinal = lastOrdinal + 1
                    let chatMessage = {
                        ChatMessageId = ChatMessageId.Create()
                        Rvn = initialRvn
                        Sender = userId, userName
                        Payload = payload
                        ProcessedPayload = processedPayload
                        TaggedUsers = taggedUsers
                        Edited = false }
                    let! _ = chatMessageDict |> addChatMessage chatMessage lastOrdinal
                    let agentRvn = incrementRvn agentRvn
                    hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessageReceived(connectionId, chatMessage, lastOrdinal, chatMessageDict.Count, key, agentRvn)))
                    return chatMessage.ChatMessageId, (lastOrdinal, agentRvn) }
                let lastOrdinal, agentRvn =
                    match chatMessageIdPlusResult with
                    | Ok(chatMessageId, (lastOrdinal, agentRvn)) ->
                        sourcedLogger.Debug("Sent {chatMessageId} with ordinal {lastOrdinal} (ChatAgent now {rvn})", chatMessageId, lastOrdinal, agentRvn)
                        lastOrdinal, agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to send ChatMessage (ChatAgent {rvn} unchanged) -> {error}", agentRvn, error)
                        lastOrdinal, agentRvn
                reply.Reply(chatMessageIdPlusResult |> ignoreResult)
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | EditChatMessage(jwt, chatMessageId, userId, payload, processedPayload, taggedUsers, rvn, reply) ->
                let chatMessageIdPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake EditChatMessage error" else Ok()
                    let! byUserId, userType = authenticator.FromJwt(jwt)
                    let! _ =
                        if canEditChatMessage userId (byUserId, userType) then Ok()
                        else Error(ifDebug (sprintf "canEditChatMessage from %A returned false for %A (%A)" userId byUserId userType) NOT_ALLOWED)
                    let! chatMessage, _, _ = chatMessageDict |> findChatMessage chatMessageId
                    let! _ = validateSameRvn chatMessage.Rvn rvn |> errorIfSome ()
                    let chatMessage = { chatMessage with Rvn = incrementRvn rvn ; Payload = payload ; ProcessedPayload = processedPayload ; TaggedUsers = taggedUsers ; Edited = true }
                    let! _ = chatMessageDict |> updateChatMessage chatMessage
                    let agentRvn = incrementRvn agentRvn
                    hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessageEdited(chatMessage, chatMessageDict.Count, key, agentRvn)))
                    return chatMessage.ChatMessageId, agentRvn }
                let agentRvn =
                    match chatMessageIdPlusResult with
                    | Ok(chatMessageId, agentRvn) ->
                        sourcedLogger.Debug("Edited {chatMessageId} (ChatAgent now {rvn})", chatMessageId, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to edit {chatMessageId} (ChatAgent {rvn} unchanged) -> {error}", chatMessageId, agentRvn, error)
                        agentRvn
                reply.Reply(chatMessageIdPlusResult |> ignoreResult)
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | DeleteChatMessage(jwt, chatMessageId, userId, expired, rvn, reply) ->
                let chatMessageIdPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake DeleteChatMessage error" else Ok()
                    let! byUserId, userType = authenticator.FromJwt(jwt)
                    let! _ =
                        if canDeleteChatMessage userId (byUserId, userType) then Ok()
                        else Error(ifDebug (sprintf "canDeleteChatMessage from %A returned false for %A (%A)" userId byUserId userType) NOT_ALLOWED)
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
                    match chatMessageIdPlusResult with
                    | Ok(chatMessageId, agentRvn) ->
                        sourcedLogger.Debug("Deleted {chatMessageId} (ChatAgent now {rvn})", chatMessageId, lastOrdinal, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to delete {chatMessageId} (ChatAgent {rvn} unchanged) -> {error}", chatMessageId, agentRvn, error)
                        agentRvn
                reply.Reply(chatMessageIdPlusResult |> ignoreResult)
                return! loop (chatMessageDict, lastOrdinal, agentRvn)
            | Housekeeping ->
                sourcedLogger.Debug("Housekeeping!")
                let expired =
                    chatMessageDict.Values
                    |> List.ofSeq
                    |> List.filter (fun (_, _, timestamp) -> (DateTimeOffset.UtcNow - timestamp).TotalHours * 1.<hour> > chatMessageLifetime)
                    |> List.map (fun (chatMessage, _, _) -> chatMessage.ChatMessageId)
                expired |> List.iter (chatMessageDict.Remove >> ignore)
                let agentRvn = if expired.Length > 0 then incrementRvn agentRvn else agentRvn
                if expired.Length > 0 then hub.SendClientIf hasChatMessages (RemoteChatInput(ChatMessagesExpired(expired, chatMessageDict.Count, key, agentRvn)))
                if expired.Length > 0 then sourcedLogger.Debug("Removed {length} expired ChatMessage/s", expired.Length)
                else sourcedLogger.Debug("No ChatMessages have expired")
                return! loop (chatMessageDict, lastOrdinal, agentRvn) }
        sourcedLogger.Information("Starting [{key}]...", key)
        let chatMessageDict = ChatMessageDict()
        loop (chatMessageDict, 0, initialRvn))
    do agent.Error.Add (fun exn -> sourcedLogger.Error("Unexpected error -> {message}", exn.Message))
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
    let! chatAgent = resolve<ChatAgent>()
    return {
        getChatMessages = chatAgent.GetChatMessages
        moreChatMessages = chatAgent.MoreChatMessages
        sendChatMessage = chatAgent.SendChatMessage
        editChatMessage = chatAgent.EditChatMessage
        deleteChatMessage = chatAgent.DeleteChatMessage } }
