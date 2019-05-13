module Aornota.Gibet.Server.Api.UserApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Api.UserApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Common.InvalidCredentials
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Jwt
open Aornota.Gibet.Server.Repo.IUserRepo

open System.Collections.Generic

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
    | ChangeImageUrl of Jwt * ImageUrl option * Rvn * AsyncReplyChannelResult<UserName * ImageChangeType option, string>
    | GetUsers of ConnectionId * Jwt * AsyncReplyChannelResult<(User * bool) list * Rvn, string>
    | CreateUser of Jwt * UserName * Password * UserType * AsyncReplyChannelResult<UserName, string>
    | ResetPassword of Jwt * UserId * Password * Rvn * AsyncReplyChannelResult<UserName, string>
    | ChangeUserType of Jwt * UserId * UserType * Rvn * AsyncReplyChannelResult<UserName, string>

type private UserDict = Dictionary<UserId, User>

let [<Literal>] private SOURCE = "Api.UserApiAgent"

let private addUser user (userDict:UserDict) =
    let userId = user.UserId
    if userDict.ContainsKey userId then Error(ifDebug (sprintf "%s.addUser -> Unable to add %A" SOURCE userId) UNEXPECTED_ERROR)
    else
        userDict.Add(userId, user)
        Ok()
let private updateUser user (userDict:UserDict) =
    let userId = user.UserId
    if userDict.ContainsKey userId then
        userDict.[userId] <- user
        Ok()
    else Error(ifDebug (sprintf "%s.updateUser -> Unable to update %A" SOURCE userId) UNEXPECTED_ERROR)
let private findUserId userId (userDict:UserDict) =
    if userDict.ContainsKey userId then Ok userDict.[userId]
    else Error(ifDebug (sprintf "%s.findUserId -> Unable to find %A" SOURCE userId) UNEXPECTED_ERROR)

let private validatePassword password = match validatePassword false password with | Some error -> Error error | None -> Ok()

type UserApiAgent(userRepo:IUserRepo, hub:IHub<HubState, RemoteServerInput, RemoteUiInput>, logger:ILogger) =
    let logger = logger |> sourcedLogger SOURCE
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop(userDict:UserDict, agentRvn:Rvn) = async {
            let! input = inbox.Receive ()
            (* TEMP-NMB...
            do! ifDebugSleepAsync 250 1000 *)
            match input with
            | SignIn(connectionId, userName, password, reply) ->
                let! repoResult = userRepo.SignIn(userName, password)
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake SignIn error -> %A" userName) else Ok()
                    let! (userId, mustChangePasswordReason) = repoResult
                    let! user = userDict |> findUserId userId
                    let! _ =
                        if not (canSignIn user.UserType) then Error(ifDebug (sprintf "%s.SignIn -> canSignIn returned false for %A" SOURCE user.UserType) NOT_ALLOWED)
                        else Ok()
                    let! jwt = toJwt user.UserId user.UserType
                    hub.SendServerIf (sameConnection connectionId) (SignedIn user.UserId)
                    return { User = user ; Jwt = jwt }, mustChangePasswordReason }
                match result with
                | Ok(authUser, mustChangePasswordReason) ->
                    logger.Debug("Signed in as {userName} ({mustChangePasswordReason})", authUser.User.UserName, mustChangePasswordReason)
                | Error error -> logger.Warning("Unable to sign in as {userName} -> {error}", userName, error)
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
                                match userDict |> findUserId userId with
                                | Ok user ->
                                    if userType <> user.UserType then Error(ifDebug (sprintf "%s.AutoSignIn -> Jwt %A differs from %A" SOURCE userType user.UserType) INVALID_CREDENTIALS)
                                    else if not (canSignIn userType) then Error(ifDebug (sprintf "%s.AutoSignIn -> canSignIn returned false for %A" SOURCE userType) NOT_ALLOWED)
                                    else // note: since tokens can expire, only create for "explicit" sign in (i.e. do *not* recreate for auto-sign in)
                                        hub.SendServerIf (sameConnection connectionId) (SignedIn user.UserId)
                                        Ok({ User = user ; Jwt = jwt }, mustChangePasswordReason)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                match result with
                | Ok(authUser, mustChangePasswordReason) ->
                    logger.Debug("Automatically signed in as {userName} ({mustChangePasswordReason})", authUser.User.UserName, mustChangePasswordReason)
                | Error error -> logger.Warning("Unable to automatically sign in -> {error}", error)
                reply.Reply result
                return! loop (userDict, agentRvn)
            | SignOut(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake SignOut error -> %A" jwt) else Ok()
                    let! userId, _ = fromJwt jwt // all authenticated Users allowed to SignOut (so no need to check UserType)
                    hub.SendServerIf (sameConnection connectionId) SignedOut
                    return userId }
                match result with
                | Ok userId ->  logger.Debug("Signed out {userId}", userId)
                | Error error -> logger.Warning("Unable to sign out -> {error}", error)
                reply.Reply (result |> Result.map ignore)
                return! loop (userDict, agentRvn)
            | ChangePassword(jwt, password, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake ChangePassword error -> %A" jwt) else Ok()
                    let! userId, userType = fromJwt jwt
                    let! _ =
                        if canChangePassword userId (userId, userType) then Ok()
                        else Error(ifDebug (sprintf "%s.ChangePassword -> canChangePassword for %A (%A) returned false" SOURCE userId userType) NOT_ALLOWED)
                    let! _ = validatePassword password
                    return userId}
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok userId ->
                        let! repoResult = userRepo.ChangePassword(userId, password, rvn)
                        return
                            match repoResult with
                            | Ok user ->
                                match userDict |> updateUser user with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserUpdated(user, agentRvn, PasswordChanged))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
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
            | ChangeImageUrl(jwt, imageUrl, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake ChangeImageUrl error -> %A" jwt) else Ok()
                    let! userId, userType = fromJwt jwt
                    let! _ =
                        if canChangeImageUrl userId (userId, userType) then Ok()
                        else Error(ifDebug (sprintf "%s.ChangeImageUrl -> canChangeImageUrl for %A (%A) returned false" SOURCE userId userType) NOT_ALLOWED)
                    return userId}
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok userId ->
                        let! repoResult = userRepo.ChangeImageUrl(userId, imageUrl, rvn)
                        return
                            match repoResult with
                            | Ok(user, imageChangeType) ->
                                match userDict |> updateUser user with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserUpdated(user, agentRvn, UserUpdateType.ImageChanged imageChangeType))
                                    Ok((user.UserName, imageChangeType), agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match result with
                    | Ok((userName, imageChangeType), agentRvn) ->
                        logger.Debug("Image URL {changeType} for {userName} (UserApiAgent now {rvn})", changeType imageChangeType, userName, agentRvn)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to change image URL (UserApiAgent {rvn} unchanged) -> {error}", agentRvn, error)
                        agentRvn
                reply.Reply (result |> Result.map fst)
                return! loop (userDict, agentRvn)
            | GetUsers(connectionId, jwt, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake GetUsers error -> %A" jwt) else Ok()
                    let! _, userType = fromJwt jwt
                    let! _ =
                        if canGetUsers userType then Ok()
                        else Error(ifDebug (sprintf "%s.GetUsers -> canGetUsers returned false for %A" SOURCE userType) NOT_ALLOWED)
                    let users = userDict.Values |> List.ofSeq |> List.map (fun user -> user, hub.GetModels() |> signedIn user.UserId)
                    hub.SendServerIf (sameConnection connectionId) HasUsers
                    return users, agentRvn }
                match result with
                | Ok (users, agentRvn) -> logger.Debug("Got {count} user/s (UserApiAgent {agentRvn})", users.Length, agentRvn)
                | Error error -> logger.Warning("Unable to get users -> {error}", error)
                reply.Reply result
                return! loop (userDict, agentRvn)
            | CreateUser(jwt, userName, password, userType, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake CreateUser error -> %A" jwt) else Ok()
                    let! _, byUserType = fromJwt jwt
                    let! _ =
                        if canCreateUser userType byUserType then Ok()
                        else Error(ifDebug (sprintf "%s.CreateUser -> canCreateUser for %A returned false for %A" SOURCE userType byUserType) NOT_ALLOWED)
                    let userNames = userDict.Values |> List.ofSeq |> List.map (fun user -> user.UserName)
                    let! _ = match validateUserName false userName userNames with | Some error -> Error error | None -> Ok()
                    let! _ = validatePassword password
                    return ()}
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok _ ->
                        let! repoResult = userRepo.CreateUser(None, userName, password, userType, None)
                        return
                            match repoResult with
                            | Ok user ->
                                match userDict |> addUser user with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserAdded(user, agentRvn))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match result with
                    | Ok(userName, agentRvn) ->
                        logger.Debug("Created user {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to create user {userName} (UserApiAgent {rvn} unchanged) -> {error}", userName, agentRvn, error)
                        agentRvn
                reply.Reply (result |> Result.map fst)
                return! loop (userDict, agentRvn)
            | ResetPassword(jwt, userId, password, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake ResetPassword error -> %A" jwt) else Ok()
                    let! byUserId, byUserType = fromJwt jwt
                    let! byUser = userDict |> findUserId byUserId
                    let! user = userDict |> findUserId userId
                    let! _ =
                        if canResetPassword (userId, user.UserType) (byUserId, byUserType) then Ok()
                        else Error(ifDebug (sprintf "%s.ResetPassword -> canResetPassword for %A (%A) returned false for %A (%A)" SOURCE userId user.UserType byUserId byUserType) NOT_ALLOWED)
                    let! _ = validatePassword password
                    return byUser}
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok byUser ->
                        let! repoResult = userRepo.ResetPassword(byUser.UserId, userId, password, rvn)
                        return
                            match repoResult with
                            | Ok user ->
                                match userDict |> updateUser user with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendServerIf (sameUser userId) (ForceChangePassword byUser.UserName)
                                    hub.SendClientIf hasUsers (UserUpdated(user, agentRvn, UserUpdateType.PasswordReset))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match result with
                    | Ok(userName, agentRvn) ->
                        logger.Debug("Password reset for {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to reset password for {userId} (UserApiAgent {rvn} unchanged) -> {error}", userId, agentRvn, error)
                        agentRvn
                reply.Reply (result |> Result.map fst)
                return! loop (userDict, agentRvn)
            | ChangeUserType(jwt, userId, userType, rvn, reply) ->
                let result = result {
                    let! _ = if debugFakeError() then Error(sprintf "Fake ChangeUserType error -> %A" jwt) else Ok()
                    let! byUserId, byUserType = fromJwt jwt
                    let! byUser = userDict |> findUserId byUserId
                    let! user = userDict |> findUserId userId
                    let! _ =
                        if canChangeUserType (userId, user.UserType) (byUserId, byUserType) then Ok()
                        else Error(ifDebug (sprintf "%s.ChangeUserType -> canChangeUserType for %A (%A) returned false for %A (%A)" SOURCE userId user.UserType byUserId byUserType) NOT_ALLOWED)
                    return byUser}
                let! result = async { // TODO-NMB: Make this less horrible (i.e. rethink how to mix Async<_> and Result<_>)?!...
                    match result with
                    | Ok byUser ->
                        let! repoResult = userRepo.ChangeUserType(userId, userType, rvn)
                        return
                            match repoResult with
                            | Ok user ->
                                match userDict |> updateUser user with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendServerIf (sameUser userId) (ForceSignOut(UserTypeChanged byUser.UserName))
                                    hub.SendClientIf (differentUserHasUsers userId) (UserUpdated(user, agentRvn, UserUpdateType.UserTypeChanged))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match result with
                    | Ok(userName, agentRvn) ->
                        logger.Debug("User type changed for {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        logger.Warning("Unable to change user type for {userId} (UserApiAgent {rvn} unchanged) -> {error}", userId, agentRvn, error)
                        agentRvn
                reply.Reply (result |> Result.map fst)
                return! loop (userDict, agentRvn) }
        logger.Information("Starting...")
        let userDict = UserDict()
        match userRepo.GetUsers() |> Async.RunSynchronously with
        | Ok users ->
            if users.Length > 0 then
                logger.Information("{count} users in IUserRepo", users.Length)
                users |> List.iter (fun user -> userDict.Add(user.UserId, user))
            else logger.Warning("No users in IUserRepo")
        | Error error -> logger.Warning("Unable to get users from IUserRepo -> {error}", error)
        loop (userDict, initialRvn))
    do agent.Error.Add (fun exn -> logger.Error("Unexpected error -> {errorMessage}", exn.Message))
    member __.SignIn(connectionId, userName, password) = agent.PostAndAsyncReply(fun reply -> SignIn(connectionId, userName, password, reply))
    member __.AutoSignIn(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> AutoSignIn(connectionId, jwt, reply))
    member __.SignOut(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> SignOut(connectionId, jwt, reply))
    member __.ChangePassword(jwt, password, rvn) = agent.PostAndAsyncReply(fun reply -> ChangePassword(jwt, password, rvn, reply))
    member __.ChangeImageUrl(jwt, imageUrl, rvn) = agent.PostAndAsyncReply(fun reply -> ChangeImageUrl(jwt, imageUrl, rvn, reply))
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
        changeImageUrl = userApi.ChangeImageUrl
        getUsers = userApi.GetUsers
        createUser = userApi.CreateUser
        resetPassword = userApi.ResetPassword
        changeUserType = userApi.ChangeUserType } }
