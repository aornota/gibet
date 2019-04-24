module Aornota.Gibet.Server.Api.UserApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Api.UserApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Jwt
open Aornota.Gibet.Server.Repo.IUserRepo

open System.Collections.Generic

open Elmish.Bridge

open FsToolkit.ErrorHandling

open Serilog

(* Enforces permissions (i.e. based on UserType).
   Enforces unique UserName.
   Enforces length (&c.) restrictions on UserName and Password.
   Prevents SignIn/AutoSignIn if PersonaNonGrata.
   Does not enforce unique UserId (but IUserRepo does).
   Does not enforce actual/expected Rvn/s (but IUserRepo does). *)

type private Input =
    | SignIn of ConnectionId * UserName * Password * AsyncReplyChannelResult<AuthUser * MustChangePasswordReason option, string>
    | AutoSignIn of ConnectionId * Jwt * AsyncReplyChannelResult<AuthUser * MustChangePasswordReason option, string>
    | SignOut of ConnectionId * Jwt * AsyncReplyChannelResult<unit, string>
    | ChangePassword of ConnectionId * Jwt * Password * Rvn * AsyncReplyChannelResult<unit, string>
    | GetUsers of ConnectionId * Jwt * AsyncReplyChannelResult<(User * bool) list * Rvn, string>
    | CreateUser of ConnectionId * Jwt * UserName * Password * UserType * AsyncReplyChannelResult<unit, string>
    | ResetPassword of ConnectionId * Jwt * UserId * Password * Rvn * AsyncReplyChannelResult<unit, string>
    | ChangeUserType of ConnectionId * Jwt * UserId * UserType * Rvn * AsyncReplyChannelResult<unit, string>

type private UserDict = Dictionary<UserId, User>

type UserApiAgent(userRepo:IUserRepo, hub:ServerHub<HubState, ServerInput, RemoteUiInput>, logger:ILogger) =
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop(userDict:UserDict, agentRvn:Rvn) = async {
            let! input = inbox.Receive ()
            (* TEMP-NMB...
            do! ifDebugSleepAsync 100 500 *)
            match input with
            | SignIn(connectionId, userName, password, reply) ->
                let! repoResult = (userName, password) |> userRepo.SignIn
                let result = result {
                    let! _ = if debugFakeError () then sprintf "Fake SignIn error: %A" userName |> Error else () |> Ok
                    let! (userId, mustChangePasswordReason) = repoResult
                    let! user =
                        if userId |> userDict.ContainsKey then userDict.[userId] |> Ok
                        else INVALID_CREDENTIALS |> Error
                    let! _ =
                        if user.UserType = PersonaNonGrata then INVALID_CREDENTIALS |> Error
                        else () |> Ok
                    let! jwt = (user.UserId, user.UserType) |> toJwt
                    user.UserId |> SignedIn |> hub.SendServerIf (connectionId |> sameConnection)
                    return { User = user ; Jwt = jwt }, mustChangePasswordReason }
                result |> reply.Reply
                return! (userDict, agentRvn) |> loop
            | AutoSignIn(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError () then sprintf "Fake AutoSignIn error: %A" jwt |> Error else () |> Ok
                    return! jwt |> fromJwt }
                let! result = async { // TODO-NMB: Make this less horrible?!...
                    match result with
                    | Ok(userId, userType) ->
                        let! repoResult = userId |> userRepo.AutoSignIn
                        return
                            match repoResult with
                            | Ok(userId, mustChangePasswordReason) ->
                                if userId |> userDict.ContainsKey then
                                    let user = userDict.[userId]
                                    if userType <> user.UserType || user.UserType = PersonaNonGrata then INVALID_CREDENTIALS |> Error
                                    else
                                        match (user.UserId, user.UserType) |> toJwt with
                                        | Ok jwt ->
                                            user.UserId |> SignedIn |> hub.SendServerIf (connectionId |> sameConnection)
                                            ({ User = user ; Jwt = jwt }, mustChangePasswordReason) |> Ok
                                        | Error error -> error |> Error
                                else INVALID_CREDENTIALS |> Error
                            | Error error -> error |> Error
                    | Error error -> return error |> Error }
                result |> reply.Reply
                return! (userDict, agentRvn) |> loop
            | SignOut(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError () then sprintf "Fake SignOut error: %A" jwt |> Error else () |> Ok
                    let! (userId, _) = jwt |> fromJwt // all authenticated Users allowed to SignOut (so no need to check UserType)
                    SignedOut |> hub.SendServerIf (connectionId |> sameConnection)
                    return () }
                result |> reply.Reply
                return! (userDict, agentRvn) |> loop
            | ChangePassword(connectionId, jwt, password, rvn, reply) ->
                (* TODO-NMB:
                    - debugFakeError...
                    - validate jwt... // all authenticated Users allowed to ChangePassword (so no need to check UserType)
                    - validate password...
                    - userRepo.ChangePassword...
                    - if successful:
                        - update userDict...
                        - increment agentRvn...
                        - (user, agentRvn) |> UserUpdated |> hub.SendClientIf (() |> hasUsers)...
                        - log result?... *)
                return! (userDict, agentRvn) |> loop
            | GetUsers(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError () then sprintf "Fake GetUsers error: %A" jwt |> Error else () |> Ok
                    let! _ = jwt |> fromJwt // all authenticated Users allowed to GetUsers (so no need to check UserType)
                    let users = userDict.Values |> List.ofSeq |> List.map (fun user -> user, hub.GetModels() |> signedIn user.UserId)
                    HasUsers |> hub.SendServerIf (connectionId |> sameConnection)
                    return users, agentRvn }
                match result with
                | Ok (users, rvn) ->  logger.Debug("Got {count} User/s: {rvn}", users.Length, rvn)
                | Error error -> logger.Warning("Unable to get Users: {error}", error)
                result |> reply.Reply
                return! (userDict, agentRvn) |> loop
            | CreateUser(connectionId, jwt, userName, password, userType, reply) ->
                (* TODO-NMB:
                    - debugFakeError...
                    - validate jwt...
                    - validate canCreateUser...
                    - validate userName...
                    - validate password...
                    - userRepo.CreateUser...
                    - if succesful:
                        - update userDict...
                        - increment agentRvn...
                        - (user, agentRvn) |> UserAdded |> hub.SendClientIf (() |> hasUsers)...
                        - log result?... *)
                return! (userDict, agentRvn) |> loop
            | ResetPassword(connectionId, jwt, userId, password, rvn, reply) ->
                (* TODO-NMB:
                    - debugFakeError...
                    - validate jwt...
                    - validate canResetPassword...
                    - validate password...
                    - userRepo.ResetPassword...
                    - if succesful:
                        - update userDict...
                        - increment agentRvn...
                        - PasswordReset |> Some |> ForceSignOut |> hub.SendServerIf (userId |> sameUser)...
                        - (user, agentRvn) |> UserUpdated |> hub.SendClientIf (userId |> differentUserHasUsers)...
                        - log result?... *)
                return! (userDict, agentRvn)|> loop
            | ChangeUserType(connectionId, jwt, userId, userType, rvn, reply) ->
                (* TODO-NMB:
                    - debugFakeError...
                    - validate jwt...
                    - validate canChangeUserType...
                    - userRepo.ChangeUserType...
                    - if succesful:
                        - update userDict...
                        - increment agentRvn...
                        - UserTypeChanged |> Some |> ForceSignOut |> hub.SendServerIf (userId |> sameUser)...
                        - (user, agentRvn) |> UserUpdated |> hub.SendClientIf (userId |> differentUserHasUsers)...
                        - log result?... *)
                return! (userDict, agentRvn) |> loop }
        logger.Information("Starting UserApi agent...")
        let userDict = UserDict()
        match userRepo.GetUsers() |> Async.RunSynchronously with
        | Ok users ->
            if users.Length > 0 then users |> List.iter (fun user -> (user.UserId, user) |> userDict.Add)
            else logger.Warning("No Users in IUserRepo")
        | Error error -> logger.Warning("Unable to get Users: {error}", error)
        (userDict, initialRvn) |> loop)
    do agent.Error.Add (fun exn -> logger.Error("Unexpected error: {message}", exn.Message))
    member __.SignIn(connection, userName, password) = (fun reply -> (connection, userName, password, reply) |> SignIn) |> agent.PostAndAsyncReply
    member __.AutoSignIn(connection, jwt) = (fun reply -> (connection, jwt, reply) |> AutoSignIn) |> agent.PostAndAsyncReply
    member __.SignOut(connection, jwt) = (fun reply -> (connection, jwt, reply) |> SignOut) |> agent.PostAndAsyncReply
    member __.ChangePassword(connection, jwt, password, rvn) = (fun reply -> (connection, jwt, password, rvn, reply) |> ChangePassword) |> agent.PostAndAsyncReply
    member __.GetUsers(connection, jwt) = (fun reply -> (connection, jwt, reply) |> GetUsers) |> agent.PostAndAsyncReply
    member __.CreateUser(connection, jwt, userName, password, userType) = (fun reply -> (connection, jwt, userName, password, userType, reply) |> CreateUser) |> agent.PostAndAsyncReply
    member __.ResetPassword(connection, jwt, userId, password, rvn) = (fun reply -> (connection, jwt, userId, password, rvn, reply) |> ResetPassword) |> agent.PostAndAsyncReply
    member __.ChangeUserType(connection, jwt, userId, userType, rvn) = (fun reply -> (connection, jwt, userId, userType, rvn, reply) |> ChangeUserType) |> agent.PostAndAsyncReply

let userApiReader = reader {
    let! userApi = resolve<UserApiAgent>()
    return {
        signIn = userApi.SignIn
        autoSignIn = userApi.AutoSignIn
        signOut = userApi.SignOut
        changePassword = userApi.ChangePassword
        getUsers = userApi.GetUsers
        createUser = userApi.CreateUser
        resetPassword = userApi.ResetPassword
        changeUserType = userApi.ChangeUserType } }
