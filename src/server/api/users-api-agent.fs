module Aornota.Gibet.Server.Api.UsersApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Api.UsersApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Jwt
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Server.Authenticator
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.InvalidCredentials
open Aornota.Gibet.Server.SourcedLogger
open Aornota.Gibet.Server.Repo.IUsersRepo

open System
open System.Collections.Generic
open System.Security.Cryptography
open System.Text

open FsToolkit.ErrorHandling

(* Notes:
     Enforces unique UserId.
     Enforces unique UserName.
     Enforces actual/expected Rvn/s.
     Enforces permissions (based on UserType).
     Enforces length (&c.) restrictions on UserName and Password - and canonicalizes ImageUrl, i.e. None if (Some null-or-whitespace).
     Prevents SignIn/AutoSignIn if PersonaNonGrata. *)

type private Input =
    | SignIn of ConnectionId * UserName * Password * AsyncReplyChannelResult<AuthUser * MustChangePasswordReason option, string>
    | AutoSignIn of ConnectionId * Jwt * AsyncReplyChannelResult<AuthUser * MustChangePasswordReason option, string>
    | SignOut of ConnectionId * Jwt * AsyncReplyChannelResult<unit, string>
    | ChangePassword of Jwt * Password * Rvn * AsyncReplyChannelResult<UserName, string>
    | ChangeImageUrl of Jwt * ImageUrl option * Rvn * AsyncReplyChannelResult<UserName * ImageChangeType option, string>
    | GetUsers of ConnectionId * Jwt * AsyncReplyChannelResult<(User * bool) list * Rvn, string>
    | CreateUser of Jwt * UserName * Password * UserType * AsyncReplyChannelResult<UserId * UserName, string>
    | ResetPassword of Jwt * UserId * Password * Rvn * AsyncReplyChannelResult<UserName, string>
    | ChangeUserType of Jwt * UserId * UserType * Rvn * AsyncReplyChannelResult<UserName, string>

type private UserDtoDict = Dictionary<UserId, UserDto>

let [<Literal>] private SOURCE = "Api.UsersApiAgent"

let private rng = RandomNumberGenerator.Create()
let private sha512 = SHA512.Create()

let private salt () =
    let bytes : byte[] = Array.zeroCreate 32
    rng.GetBytes(bytes)
    Salt(Convert.ToBase64String(bytes))
let private hash (Password password) (Salt salt) =
    let bytes = sha512.ComputeHash(Encoding.UTF8.GetBytes(sprintf "%s|%s" password salt))
    Hash(Convert.ToBase64String(bytes))

let private canoicalizeImageUrl imageUrl =
    match imageUrl with
    | Some(ImageUrl imageUrl) -> if String.IsNullOrWhiteSpace(imageUrl) then None else Some(ImageUrl imageUrl)
    | None -> None
let private validatePassword password userName = match validatePassword false password userName with | Some error -> Error error | None -> Ok()

let private findUserDto userId (userDtoDict:UserDtoDict) =
    if userDtoDict.ContainsKey userId then Ok userDtoDict.[userId]
    else Error(ifDebug (sprintf "Unable to find %A" userId) UNEXPECTED_ERROR)
let private addUserDto userDto (userDtoDict:UserDtoDict) =
    let userId = userDto.User.UserId
    if userDtoDict.ContainsKey userId then Error(ifDebug (sprintf "%A already exists" userId) UNEXPECTED_ERROR)
    else
        userDtoDict.Add(userId, userDto)
        Ok()
let private updateUserDto userDto (userDtoDict:UserDtoDict) =
    let userId = userDto.User.UserId
    result {
        let! _ = userDtoDict |> findUserDto userId
        userDtoDict.[userId] <- userDto
        return () }

type UsersApiAgent(usersRepo:IUsersRepo, hub:IHub<HubState, RemoteServerInput, RemoteUiInput>, authenticator:Authenticator, logger) =
    let sourcedLogger, logger = logger |> sourcedLogger SOURCE, ()
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop (userDtoDict:UserDtoDict, agentRvn) = async {
            let! input = inbox.Receive ()
            (* TEMP-NMB...
            do! ifDebugSleepAsync 250 1000 *)
            match input with
            | SignIn(connectionId, userName, password, reply) ->
                let authUserPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake SignIn error" else Ok()
                    let! userDto =
                        match userDtoDict.Values |> List.ofSeq |> List.filter (fun userDto -> userDto.User.UserName = userName) with
                        | [ userDto ] -> Ok userDto
                        | _ :: _ -> Error(ifDebug (sprintf "%A not unique" userName) INVALID_CREDENTIALS)
                        | [] -> Error(ifDebug (sprintf "%A not found" userName) INVALID_CREDENTIALS)
                    let! _ = if userDto.Hash = hash password userDto.Salt then Ok() else Error(ifDebug (sprintf "Invalid password for %A" userName) INVALID_CREDENTIALS)
                    let user = userDto.User
                    let userId, userType = user.UserId, user.UserType
                    let! _ = if canSignIn userType then Ok() else Error(ifDebug (sprintf "canSignIn returned false for %A" userType) INVALID_CREDENTIALS)
                    let! jwt = authenticator.ToJwt(userId, userType)
                    hub.SendServerIf (sameConnection connectionId) (SignedIn userId)
                    return { User = user ; Jwt = jwt }, userDto.MustChangePasswordReason }
                match authUserPlusResult with
                | Ok(authUser, mustChangePasswordReason) -> sourcedLogger.Debug("Signed in as {userName} ({mustChangePasswordReason})", authUser.User.UserName, mustChangePasswordReason)
                | Error error -> sourcedLogger.Warning("Unable to sign in as {userName} -> {error}", userName, error)
                reply.Reply authUserPlusResult
                return! loop (userDtoDict, agentRvn)
            | AutoSignIn(connectionId, jwt, reply) ->
                let authUserPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake AutoSignIn error" else Ok()
                    let! userId, jwtUserType = authenticator.FromJwt(jwt)
                    let! userDto = userDtoDict |> findUserDto userId
                    let user = userDto.User
                    let userId, userType = user.UserId, user.UserType
                    let! authUserPlus =
                        if userType <> jwtUserType then Error(ifDebug (sprintf "%A differs from token (%A)" userType jwtUserType) INVALID_CREDENTIALS)
                        else if canSignIn userType then // note: since tokens can expire, only create for SignIn, i.e. do not recreate for AutoSignIn
                            hub.SendServerIf (sameConnection connectionId) (SignedIn userId)
                            Ok({ User = user ; Jwt = jwt }, userDto.MustChangePasswordReason)
                        else Error(ifDebug (sprintf "canSignIn returned false for %A" userType) INVALID_CREDENTIALS)
                    return authUserPlus }
                match authUserPlusResult with
                | Ok(authUser, mustChangePasswordReason) ->
                    sourcedLogger.Debug("Automatically signed in as {userName} ({mustChangePasswordReason})", authUser.User.UserName, mustChangePasswordReason)
                | Error error -> sourcedLogger.Warning("Unable to automatically sign in -> {error}", error)
                reply.Reply authUserPlusResult
                return! loop (userDtoDict, agentRvn)
            | SignOut(connectionId, jwt, reply) ->
                let userIdPlus = result {
                    let! _ = if debugFakeError() then Error "Fake SignOut error" else Ok()
                    let! userId, _ = authenticator.FromJwt(jwt) // all authenticated Users allowed to SignOut (so no need to check UserType)
                    hub.SendServerIf (sameConnection connectionId) SignedOut
                    return userId }
                match userIdPlus with
                | Ok userId ->  sourcedLogger.Debug("Signed out {userId}", userId)
                | Error error -> sourcedLogger.Warning("Unable to sign out -> {error}", error)
                reply.Reply(userIdPlus |> Result.map ignore)
                return! loop (userDtoDict, agentRvn)
            | ChangePassword(jwt, password, rvn, reply) ->
                let currentUserDtoResult = result {
                    let! _ = if debugFakeError() then Error "Fake ChangePassword error" else Ok()
                    let! userId, userType = authenticator.FromJwt(jwt)
                    let! currentUserDto = userDtoDict |> findUserDto userId
                    let! _ = if canChangePassword userId (userId, userType) then Ok() else Error(ifDebug (sprintf "canChangePassword for %A (%A) returned false" userId userType) NOT_ALLOWED)
                    let currentUser = currentUserDto.User
                    let! _ = validatePassword password currentUser.UserName
                    let! _ = if hash password currentUserDto.Salt = currentUserDto.Hash then Error "New password must not be the same as existing password" else Ok()
                    let! _ = validateSameRvn currentUser.Rvn rvn |> errorIfSome ()
                    return currentUserDto }
                let! userNamePlusResult = async {
                    match currentUserDtoResult with
                    | Ok currentUserDto ->
                        let currentUser = currentUserDto.User
                        let user = { currentUser with Rvn = incrementRvn currentUser.Rvn }
                        let salt = salt ()
                        let userDto = { currentUserDto with User = user; Salt = salt ; Hash = hash password salt ; MustChangePasswordReason = None }
                        let! unitResult = usersRepo.UpdateUser(userDto)
                        return
                            match unitResult with
                            | Ok _ ->
                                match userDtoDict |> updateUserDto userDto with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserUpdated(user, PasswordChanged, agentRvn))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match userNamePlusResult with
                    | Ok(userName, agentRvn) ->
                        sourcedLogger.Debug("Password changed for {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to change Password (UserApiAgent {rvn} unchanged) -> {error}", agentRvn, error)
                        agentRvn
                reply.Reply(userNamePlusResult |> Result.map fst)
                return! loop (userDtoDict, agentRvn)
            | ChangeImageUrl(jwt, imageUrl, rvn, reply) ->
                let currentUserDtoResult = result {
                    let! _ = if debugFakeError() then Error "Fake ChangeImageUrl error" else Ok()
                    let! userId, userType = authenticator.FromJwt(jwt)
                    let! currentUserDto = userDtoDict |> findUserDto userId
                    let! _ = if canChangeImageUrl userId (userId, userType) then Ok() else Error(ifDebug (sprintf "canChangeImageUrl for %A (%A) returned false" userId userType) NOT_ALLOWED)
                    let! _ = validateSameRvn currentUserDto.User.Rvn rvn |> errorIfSome ()
                    return currentUserDto }
                let! userNamePlusResult = async {
                    match currentUserDtoResult with
                    | Ok currentUserDto ->
                        let currentUser = currentUserDto.User
                        let currentImageUrl, imageUrl = currentUser.ImageUrl, canoicalizeImageUrl imageUrl
                        let imageChangeType =
                            match currentImageUrl, imageUrl with
                            | None, Some _ -> Some ImageChosen
                            | Some currentImageUrl, Some imageUrl when currentImageUrl <> imageUrl -> Some ImageChanged
                            | Some _, None -> Some ImageRemoved
                            | _ -> None // should never happen
                        let user = { currentUser with Rvn = incrementRvn currentUser.Rvn ; ImageUrl = imageUrl }
                        let userDto = { currentUserDto with User = user }
                        let! unitResult = usersRepo.UpdateUser(userDto)
                        return
                            match unitResult with
                            | Ok _ ->
                                match userDtoDict |> updateUserDto userDto with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserUpdated(user, UserUpdateType.ImageChanged imageChangeType, agentRvn))
                                    Ok((user.UserName, imageChangeType), agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match userNamePlusResult with
                    | Ok((userName, imageChangeType), agentRvn) ->
                        sourcedLogger.Debug("ImageUrl {changeType} for {userName} (UserApiAgent now {rvn})", changeType imageChangeType, userName, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to change ImageUrl (UserApiAgent {rvn} unchanged) -> {error}", agentRvn, error)
                        agentRvn
                reply.Reply(userNamePlusResult |> Result.map fst)
                return! loop (userDtoDict, agentRvn)
            | GetUsers(connectionId, jwt, reply) ->
                let usersPlus = result {
                    let! _ = if debugFakeError() then Error "Fake GetUsers error" else Ok()
                    let! _, userType = authenticator.FromJwt(jwt)
                    let! _ = if canGetUsers userType then Ok() else Error(ifDebug (sprintf "canGetUsers returned false for %A" userType) NOT_ALLOWED)
                    let users = userDtoDict.Values |> List.ofSeq |> List.map (fun userDto -> userDto.User, hub.GetModels() |> signedIn userDto.User.UserId)
                    hub.SendServerIf (sameConnection connectionId) HasUsers
                    return users, agentRvn }
                match usersPlus with
                | Ok (users, agentRvn) -> sourcedLogger.Debug("Got {length} User/s (UserApiAgent {agentRvn})", users.Length, agentRvn)
                | Error error -> sourcedLogger.Warning("Unable to get Users -> {error}", error)
                reply.Reply usersPlus
                return! loop (userDtoDict, agentRvn)
            | CreateUser(jwt, UserName userName, Password password, userType, reply) ->
                let userDtoResult = result {
                    let! _ = if debugFakeError() then Error "Fake CreateUser error" else Ok()
                    let! _, byUserType = authenticator.FromJwt(jwt)
                    let! _ = if canCreateUser userType byUserType then Ok() else Error(ifDebug (sprintf "canCreateUser for %A returned false for %A" userType byUserType) NOT_ALLOWED)
                    let userName, password = UserName(userName.Trim()), Password(password.Trim())
                    let userNames = userDtoDict.Values |> List.ofSeq |> List.map (fun userDto -> userDto.User.UserName)
                    let! _ = match validateUserName false userName userNames with | Some error -> Error error | None -> Ok()
                    let! _ = validatePassword password userName
                    let salt = salt ()
                    return {
                        User = {
                            UserId = UserId.Create()
                            Rvn = initialRvn
                            UserName = userName
                            UserType = userType
                            ImageUrl = None }
                        Salt = salt
                        Hash = hash password salt
                        MustChangePasswordReason = Some FirstSignIn } }
                let! userIdPlusResult = async {
                    match userDtoResult with
                    | Ok userDto ->
                        let! unitResult = usersRepo.AddUser(userDto)
                        return
                            match unitResult with
                            | Ok _ ->
                                match userDtoDict |> addUserDto userDto with
                                | Ok _ ->
                                    let user = userDto.User
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendClientIf hasUsers (UserAdded(user, agentRvn))
                                    Ok((user.UserId, user.UserName), agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match userIdPlusResult with
                    | Ok((userId, userName), agentRvn) ->
                        sourcedLogger.Debug("Created User {userName} ({userId}) (UserApiAgent now {rvn})", userName, userId, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to create User {userName} (UserApiAgent {rvn} unchanged) -> {error}", userName, agentRvn, error)
                        agentRvn
                reply.Reply(userIdPlusResult |> Result.map fst)
                return! loop (userDtoDict, agentRvn)
            | ResetPassword(jwt, userId, password, rvn, reply) ->
                let currentUserDtoPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake ResetPassword error" else Ok()
                    let! byUserId, byUserType = authenticator.FromJwt(jwt)
                    let! byUserDto = userDtoDict |> findUserDto byUserId
                    let! currentUserDto = userDtoDict |> findUserDto userId
                    let currentUser = currentUserDto.User
                    let currentUserType = currentUser.UserType
                    let! _ =
                        if canResetPassword (userId, currentUserType) (byUserId, byUserType) then Ok()
                        else Error(ifDebug (sprintf "canResetPassword for %A (%A) returned false for %A (%A)" userId currentUserType byUserId byUserType) NOT_ALLOWED)
                    let! _ = validatePassword password currentUser.UserName
                    let! _ = if hash password currentUserDto.Salt = currentUserDto.Hash then Error "New password must not be the same as existing password" else Ok()
                    let! _ = validateSameRvn currentUser.Rvn rvn |> errorIfSome ()
                    return currentUserDto, byUserDto.User.UserName }
                let! userNamePlusResult = async {
                    match currentUserDtoPlusResult with
                    | Ok(currentUserDto, byUserName) ->
                        let currentUser = currentUserDto.User
                        let user = { currentUser with Rvn = incrementRvn currentUser.Rvn }
                        let salt = salt ()
                        let mustChangePasswordReason = match currentUserDto.MustChangePasswordReason with | Some FirstSignIn -> Some FirstSignIn | _ -> Some(PasswordReset byUserName)
                        let userDto = { currentUserDto with User = user; Salt = salt ; Hash = hash password salt ; MustChangePasswordReason = mustChangePasswordReason }
                        let! unitResult = usersRepo.UpdateUser(userDto)
                        return
                            match unitResult with
                            | Ok _ ->
                                match userDtoDict |> updateUserDto userDto with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendServerIf (sameUser userId) (ForceChangePassword byUserName)
                                    hub.SendClientIf hasUsers (UserUpdated(user, UserUpdateType.PasswordReset, agentRvn))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match userNamePlusResult with
                    | Ok(userName, agentRvn) ->
                        sourcedLogger.Debug("Password reset for {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to reset Password for {userId} (UserApiAgent {rvn} unchanged) -> {error}", userId, agentRvn, error)
                        agentRvn
                reply.Reply(userNamePlusResult |> Result.map fst)
                return! loop(userDtoDict, agentRvn)
            | ChangeUserType(jwt, userId, userType, rvn, reply) ->
                let currentUserDtoPlusResult = result {
                    let! _ = if debugFakeError() then Error "Fake ChangeUserType error" else Ok()
                    let! byUserId, byUserType = authenticator.FromJwt(jwt)
                    let! byUserDto = userDtoDict |> findUserDto byUserId
                    let! currentUserDto = userDtoDict |> findUserDto userId
                    let currentUser = currentUserDto.User
                    let currentUserType = currentUser.UserType
                    let! _ =
                        if canChangeUserType (userId, currentUserType) (byUserId, byUserType) then Ok()
                        else Error(ifDebug (sprintf "canChangeUserType for %A (%A) returned false for %A (%A)" userId currentUserType byUserId byUserType) NOT_ALLOWED)
                    let! _ = validateSameRvn currentUser.Rvn rvn |> errorIfSome ()
                    return currentUserDto, byUserDto.User.UserName }
                let! userNamePlusResult = async {
                    match currentUserDtoPlusResult with
                    | Ok(currentUserDto, byUserName) ->
                        let currentUser = currentUserDto.User
                        let user = { currentUser with Rvn = incrementRvn currentUser.Rvn ; UserType = userType }
                        let userDto = { currentUserDto with User = user }
                        let! unitResult = usersRepo.UpdateUser(userDto)
                        return
                            match unitResult with
                            | Ok _ ->
                                match userDtoDict |> updateUserDto userDto with
                                | Ok _ ->
                                    let agentRvn = incrementRvn agentRvn
                                    hub.SendServerIf (sameUser userId) (ForceSignOut(UserTypeChanged byUserName))
                                    hub.SendClientIf (differentUserHasUsers userId) (UserUpdated(user, UserUpdateType.UserTypeChanged, agentRvn))
                                    Ok(user.UserName, agentRvn)
                                | Error error -> Error error
                            | Error error -> Error error
                    | Error error -> return Error error }
                let agentRvn =
                    match userNamePlusResult with
                    | Ok(userName, agentRvn) ->
                        sourcedLogger.Debug("UserType changed for {userName} (UserApiAgent now {rvn})", userName, agentRvn)
                        agentRvn
                    | Error error ->
                        sourcedLogger.Warning("Unable to change UserType for {userId} (UserApiAgent {rvn} unchanged) -> {error}", userId, agentRvn, error)
                        agentRvn
                reply.Reply(userNamePlusResult |> Result.map fst)
                return! loop (userDtoDict, agentRvn) }
        sourcedLogger.Information("Starting...")
        let userDtoDict = UserDtoDict()
        let userRepoTypeName = usersRepo.GetType().Name
        match usersRepo.GetUsers() |> Async.RunSynchronously with
        | Ok userDtos ->
            if userDtos.Length > 0 then
                sourcedLogger.Information("{length} User/s in {userRepoTypeName}", userDtos.Length, userRepoTypeName)
                userDtos |> List.iter (fun userDto -> userDtoDict.Add(userDto.User.UserId, userDto))
            else sourcedLogger.Warning("No Users in {userRepoTypeName}", userRepoTypeName)
        | Error error -> sourcedLogger.Warning("Unable to get Users from {userRepoTypeName} -> {error}", userRepoTypeName, error)
        loop (userDtoDict, initialRvn))
    do agent.Error.Add (fun exn -> sourcedLogger.Error("Unexpected error -> {message}", exn.Message))
    member __.SignIn(connectionId, userName, password) = agent.PostAndAsyncReply(fun reply -> SignIn(connectionId, userName, password, reply))
    member __.AutoSignIn(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> AutoSignIn(connectionId, jwt, reply))
    member __.SignOut(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> SignOut(connectionId, jwt, reply))
    member __.ChangePassword(jwt, password, rvn) = agent.PostAndAsyncReply(fun reply -> ChangePassword(jwt, password, rvn, reply))
    member __.ChangeImageUrl(jwt, imageUrl, rvn) = agent.PostAndAsyncReply(fun reply -> ChangeImageUrl(jwt, imageUrl, rvn, reply))
    member __.GetUsers(connectionId, jwt) = agent.PostAndAsyncReply(fun reply -> GetUsers(connectionId, jwt, reply))
    member __.CreateUser(jwt, userName, password, userType) = agent.PostAndAsyncReply(fun reply -> CreateUser(jwt, userName, password, userType, reply))
    member __.ResetPassword(jwt, userId, password, rvn) = agent.PostAndAsyncReply(fun reply -> ResetPassword(jwt, userId, password, rvn, reply))
    member __.ChangeUserType(jwt, userId, userType, rvn) = agent.PostAndAsyncReply(fun reply -> ChangeUserType( jwt, userId, userType, rvn, reply))

let usersApiReader = reader {
    let! usersApi = resolve<UsersApiAgent>()
    return {
        signIn = usersApi.SignIn
        autoSignIn = usersApi.AutoSignIn
        signOut = usersApi.SignOut
        changePassword = usersApi.ChangePassword
        changeImageUrl = usersApi.ChangeImageUrl
        getUsers = usersApi.GetUsers
        createUser = usersApi.CreateUser
        resetPassword = usersApi.ResetPassword
        changeUserType = usersApi.ChangeUserType } }
