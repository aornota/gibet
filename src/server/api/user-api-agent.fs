module Aornota.Gibet.Server.Api.UserApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Api.UserApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
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
    | ChangePassword of Jwt * Password * Rvn * AsyncReplyChannelResult<UserName, string>
    | GetUsers of ConnectionId * Jwt * AsyncReplyChannelResult<(User * bool) list * Rvn, string>
    | CreateUser of Jwt * UserName * Password * UserType * AsyncReplyChannelResult<UserName, string>
    | ResetPassword of Jwt * UserId * Password * Rvn * AsyncReplyChannelResult<UserName, string>
    | ChangeUserType of Jwt * UserId * UserType * Rvn * AsyncReplyChannelResult<UserName, string>

type private UserDict = Dictionary<UserId, User>

type UserApiAgent(userRepo:IUserRepo, hub:ServerHub<HubState, ServerInput, RemoteUiInput>, logger:ILogger) =
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop(userDict:UserDict, agentRvn:Rvn) = async {
            let! input = inbox.Receive ()
            (* TEMP-NMB... *)
            do! ifDebugSleepAsync 250 1000
            match input with
            | SignIn(connectionId, userName, password, reply) ->
                let! repoResult = userRepo.SignIn(userName, password)
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake SignIn error -> %A" userName) else Ok()
                    let! (userId, mustChangePasswordReason) = repoResult
                    let! user =
                        if userDict.ContainsKey userId then Ok userDict.[userId]
                        else Error(ifDebug (sprintf "UserApiAgent.SignIn -> userDict does not contain %A" userId) UNEXPECTED_ERROR)
                    let! _ =
                        if user.UserType = PersonaNonGrata then Error(ifDebug (sprintf "UserApiAgent.SignIn -> %A is %A" userName PersonaNonGrata) INVALID_CREDENTIALS)
                        else Ok()
                    let! jwt = toJwt user.UserId user.UserType
                    hub.SendServerIf (sameConnection connectionId) (SignedIn user.UserId)
                    return { User = user ; Jwt = jwt }, mustChangePasswordReason }
                reply.Reply result
                return! loop (userDict, agentRvn)
            | AutoSignIn(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake AutoSignIn error -> %A" jwt) else Ok()
                    return! fromJwt jwt }
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok(userId, userType) ->
                        let! repoResult = userRepo.AutoSignIn userId
                        return
                            match repoResult with
                            | Ok(userId, mustChangePasswordReason) ->
                                if userDict.ContainsKey userId then
                                    let user = userDict.[userId]
                                    if userType <> user.UserType then Error(ifDebug (sprintf "UserApiAgent.AutoSignIn -> Jwt %A differs from %A" userType user.UserType) INVALID_CREDENTIALS)
                                    else if userType = PersonaNonGrata then Error(ifDebug (sprintf "UserApiAgent.AutoSignIn -> %A is %A" user.UserName PersonaNonGrata) INVALID_CREDENTIALS)
                                    else
                                        match toJwt user.UserId user.UserType with
                                        | Ok jwt ->
                                            hub.SendServerIf (sameConnection connectionId) (SignedIn user.UserId)
                                            Ok({ User = user ; Jwt = jwt }, mustChangePasswordReason)
                                        | Error error -> Error error
                                else Error(ifDebug (sprintf "UserApiAgent.AutoSignIn -> userDict does not contain %A" userId) UNEXPECTED_ERROR)
                            | Error error -> Error error
                    | Error error -> return Error error }
                reply.Reply result
                return! loop (userDict, agentRvn)
            | SignOut(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake SignOut error -> %A" jwt) else Ok()
                    let! _ = fromJwt jwt // all authenticated Users allowed to SignOut (so no need to check UserType)
                    hub.SendServerIf (sameConnection connectionId) SignedOut
                    return() }
                reply.Reply result
                return! loop (userDict, agentRvn)
            | ChangePassword(jwt, password, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake ChangePassword error -> %A" jwt) else Ok()
                    let! userId, userType = fromJwt jwt
                    let! _ =
                        if canChangePassword userId (userId, userType) then Ok()
                        else Error(ifDebug (sprintf "UserApiAgent.ChangePassword -> canChangePassword returned false for %A (%A)" userId userType) UNEXPECTED_ERROR)
                    let! _ = match validatePassword false password with | Some error -> Error error | None -> Ok()
                    return userId}
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok userId ->
                        let! repoResult = userRepo.ChangePassword(userId, password, rvn)
                        return
                            match repoResult with
                            | Ok user ->
                                if userDict.ContainsKey userId then
                                    userDict.[userId] <- user
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserUpdated(user,agentRvn))
                                    Ok(user.UserName, agentRvn)
                                else Error(ifDebug (sprintf "UserApiAgent.ChangePassword -> userDict does not contain %A" userId) UNEXPECTED_ERROR)
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match result with
                    | Ok(userName, agentRvn) ->
                        logger.Debug("Password changed for {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to change password (UserApiAgent {rvn} unchanged) -> {error}", agentRvn, error)
                        agentRvn
                reply.Reply (result |> Result.map fst)
                return! loop (userDict, agentRvn)
            | GetUsers(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake GetUsers error -> %A" jwt) else Ok()
                    let! _ = fromJwt jwt // all authenticated Users allowed to GetUsers (so no need to check UserType)
                    let users = userDict.Values |> List.ofSeq |> List.map (fun user -> user, hub.GetModels() |> signedIn user.UserId)
                    hub.SendServerIf (sameConnection connectionId) HasUsers
                    return users, agentRvn }
                match result with
                | Ok (users, agentRvn) ->  logger.Debug("Got {count} User/s (UserApiAgent {agentRvn})", users.Length, agentRvn)
                | Error error -> logger.Warning("Unable to get Users -> {error}", error)
                reply.Reply result
                return! loop (userDict, agentRvn)
            | CreateUser(jwt, userName, password, userType, reply) -> (* TODO-NMB...
                - debugFakeError...
                - validate jwt: INVALID_CREDENTIALS (ifDebug &c.)...
                - validate canCreateUser: NOT_ALLOWED (ifDebug &c.)...
                - validate userName...
                - validate password...
                - userRepo.CreateUser...
                - if succesful:
                    - update userDict...
                    - increment agentRvn (see ChangePassword)...
                    - hub.SendClientIf hasUsers (UserAdded(user, agentRvn))...
                    - log result?... *)
                return! loop (userDict, agentRvn)
            | ResetPassword(jwt, userId, password, rvn, reply) -> (* TODO-NMB...
                - debugFakeError...
                - validate jwt: INVALID_CREDENTIALS (ifDebug &c.)...
                - validate canResetPassword: NOT_ALLOWED (ifDebug &c.)...
                - validate password...
                - userRepo.ResetPassword...
                - if succesful:
                    - update userDict...
                    - increment agentRvn (see ChangePassword)...
                    - hub.SendServerIf (sameUser userId) (ForceSignOut(Some PasswordReset))...
                    - hub.SendClientIf (differentUserHasUsers userId) (UserUpdated(user, agentRvn))...
                    - log result?... *)
                return! loop (userDict, agentRvn)
            | ChangeUserType(jwt, userId, userType, rvn, reply) -> (* TODO-NMB:
                - debugFakeError...
                - validate jwt: INVALID_CREDENTIALS (ifDebug &c.)...
                - validate canChangeUserType: NOT_ALLOWED (ifDebug &c.)...
                - userRepo.ChangeUserType...
                - if succesful:
                    - update userDict...
                    - increment agentRvn (see ChangePassword)...
                    - hub.SendServerIf (sameUser userId) (ForceSignOut(Some UserTypeChanged))...
                    - hub.SendClientIf (differentUserHasUsers userId) (UserUpdated(user, agentRvn))...
                    - log result?... *)
                return! loop (userDict, agentRvn) }
        logger.Information("Starting UserApiAgent...")
        let userDict = UserDict()
        match userRepo.GetUsers() |> Async.RunSynchronously with
        | Ok users ->
            if users.Length > 0 then users |> List.iter (fun user -> userDict.Add(user.UserId, user))
            else logger.Warning("No Users in IUserRepo")
        | Error error -> logger.Warning("Unable to get Users -> {error}", error)
        loop (userDict, initialRvn))
    do agent.Error.Add (fun exn -> logger.Error("Unexpected UserApiAgent error -> {message}", exn.Message))
    member __.SignIn(connectionId, userName, password) = agent.PostAndAsyncReply(fun reply -> SignIn(connectionId, userName, password, reply))
    member __.AutoSignIn(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> AutoSignIn(connectionId, jwt, reply))
    member __.SignOut(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> SignOut(connectionId, jwt, reply))
    member __.ChangePassword(jwt, password, rvn) = agent.PostAndAsyncReply(fun reply -> ChangePassword(jwt, password, rvn, reply))
    member __.GetUsers(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> GetUsers(connectionId, jwt, reply))
    member __.CreateUser(jwt, userName, password, userType) = agent.PostAndAsyncReply(fun reply -> CreateUser(jwt, userName, password, userType, reply))
    member __.ResetPassword(jwt, userId, password, rvn) = agent.PostAndAsyncReply(fun reply -> ResetPassword(jwt, userId, password, rvn, reply))
    member __.ChangeUserType(jwt, userId, userType, rvn) = agent.PostAndAsyncReply(fun reply -> ChangeUserType( jwt, userId, userType, rvn, reply))

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
