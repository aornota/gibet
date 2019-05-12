module Aornota.Gibet.Server.Repo.InMemoryUserRepoAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Server.Common.InvalidCredentials
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo.IUserRepo

open System
open System.Collections.Generic

open FsToolkit.ErrorHandling

open Serilog

(* Enforces unique UserId.
   Enforces actual/expected Rvn/s.
   Does not enforce unique UserName (but cannot sign in with non-unique UserName).
   Does not enforce any length (&c.) restrictions on UserName or Password - but trims UserName and Password and ensures ImageUrl None if (Some whitespace-only).
   Does not prevent SignIn/AutoSignIn if PersonaNonGrata. *)

type private Input =
    | SignIn of UserName * Password * AsyncReplyChannelResult<UserId * MustChangePasswordReason option, string>
    | AutoSignIn of UserId * AsyncReplyChannelResult<UserId * MustChangePasswordReason option, string>
    | ChangePassword of UserId * Password * Rvn * AsyncReplyChannelResult<User, string>
    | ChangeImageUrl of UserId * ImageUrl option * Rvn * AsyncReplyChannelResult<User * ImageChangeType option, string>
    | GetUsers of AsyncReplyChannelResult<User list, string>
    | CreateUser of UserId option * UserName * Password * UserType * ImageUrl option * AsyncReplyChannelResult<User, string>
    | ResetPassword of byUserId : UserId * UserId * Password * Rvn * AsyncReplyChannelResult<User, string>
    | ChangeUserType of UserId * UserType * Rvn * AsyncReplyChannelResult<User, string>

type private InMemoryUser = {
    User : User
    Salt : Salt
    Hash : Hash
    MustChangePasswordReason : MustChangePasswordReason option }

type private ImUserDict = Dictionary<UserId, InMemoryUser>

let [<Literal>] private SOURCE = "Repo.InMemoryUserRepoAgent"

let private canoicalizeImageUrl imageUrl =
    match imageUrl with
    | Some(ImageUrl imageUrl) ->
        if String.IsNullOrWhiteSpace imageUrl then None else Some(ImageUrl imageUrl)
    | None -> None

let private addUser userId userName password userType imageUrl (imUserDict:ImUserDict) =
    let userId = userId |> Option.defaultValue (UserId.Create())
    let result = result {
        let! _ =
            if imUserDict.ContainsKey userId then Error(ifDebug (sprintf "%s.addUser -> %A already exists" SOURCE userId) UNEXPECTED_ERROR)
            else Ok()
        let user = {
            UserId = userId
            Rvn = initialRvn
            UserName = userName
            UserType = userType
            ImageUrl = imageUrl }
        let salt = salt()
        let imUser = {
            User = user
            Salt = salt
            Hash = hash password salt
            MustChangePasswordReason = Some FirstSignIn }
        imUserDict.Add(userId, imUser)
        return imUser.User }
    result
let private updateUser imUser (imUserDict:ImUserDict) =
    let userId = imUser.User.UserId
    if imUserDict.ContainsKey userId then
        imUserDict.[userId] <- imUser
        Ok()
    else Error(ifDebug (sprintf "%s.updateUser -> Unable to update %A" SOURCE userId) UNEXPECTED_ERROR)
let private findUserId userId (imUserDict:ImUserDict) =
    if imUserDict.ContainsKey userId then Ok imUserDict.[userId]
    else Error(ifDebug (sprintf "%s.findUserId -> Unable to find %A" SOURCE userId) UNEXPECTED_ERROR)
let private findUserName userName error (imUserDict:ImUserDict) =
    match imUserDict.Values |> List.ofSeq |> List.filter (fun imUser -> imUser.User.UserName = userName) with
    | [ imUser ] -> Ok imUser
    | _ :: _ | [] -> Error error

type InMemoryUserRepoAgent(logger:ILogger) =
    let logger = logger |> sourcedLogger SOURCE
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop (imUserDict:ImUserDict) = async {
            match! inbox.Receive() with
            | SignIn(userName, password, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserName userName (ifDebug (sprintf "%s.SignIn -> %A not found" SOURCE userName) INVALID_CREDENTIALS)
                    let! _ =
                        if imUser.Hash = hash password imUser.Salt then Ok()
                        else Error(ifDebug (sprintf "%s.SignIn -> Invalid password for %A" SOURCE userName) INVALID_CREDENTIALS)
                    return (imUser.User.UserId, imUser.MustChangePasswordReason) }
                match result with
                | Ok _ -> logger.Debug("Able to sign in as {userName}", userName)
                | Error error -> logger.Warning("Unable to sign in as {userName} -> {error}", userName, error)
                reply.Reply result
                return! loop imUserDict
            | AutoSignIn(userId, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    return (imUser.User.UserId, imUser.MustChangePasswordReason) }
                match result with
                | Ok _ -> logger.Debug("Able to automatically sign in as {userId}", userId)
                | Error error -> logger.Warning("Unable to automatically sign in as {userId} -> {error}", userId, error)
                reply.Reply result
                return! loop imUserDict
            | ChangePassword(userId, Password password, rvn, reply) ->
                let password = Password(password.Trim())
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = validateRvn imUser.User.Rvn rvn |> errorIfSome ()
                    let! _ = if hash password imUser.Salt = imUser.Hash then Error "New password must not be the same as existing password" else Ok()
                    let user = { imUser.User with Rvn = incrementRvn rvn }
                    let salt = salt()
                    let imUser = { imUser with User = user ; Salt = salt ; Hash = hash password salt ; MustChangePasswordReason = None }
                    let! _ = imUserDict |> updateUser imUser
                    return user }
                match result with
                | Ok user -> logger.Debug("Password changed for {user}", user)
                | Error error -> logger.Warning("Unable to change password for {userId} -> {error}", userId, error)
                reply.Reply result
                return! loop imUserDict
            | ChangeImageUrl(userId, imageUrl, rvn, reply) ->
                let imageUrl = canoicalizeImageUrl imageUrl
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = validateRvn imUser.User.Rvn rvn |> errorIfSome ()
                    let imageChangeType =
                        match imUser.User.ImageUrl, imageUrl with
                        | None, Some _ -> Some ImageChosen
                        | Some currentImageUrl, Some imageUrl when currentImageUrl <> imageUrl -> Some ImageChanged
                        | Some _, None -> Some ImageRemoved
                        | _ -> None // should never happen
                    let user = { imUser.User with Rvn = incrementRvn rvn ; ImageUrl = imageUrl }
                    let imUser = { imUser with User = user }
                    let! _ = imUserDict |> updateUser imUser
                    return user, imageChangeType }
                match result with
                | Ok(user, imageChangeType) -> logger.Debug("Image URL {changeType} for {user}", changeType imageChangeType, user)
                | Error error -> logger.Warning("Unable to change image URL for {userId} -> {error}", userId, error)
                reply.Reply result
                return! loop imUserDict
            | GetUsers reply ->
                let result : Result<User list, string> = Ok(imUserDict.Values |> List.ofSeq |> List.map (fun imUser -> imUser.User))
                match result with
                | Ok users -> logger.Debug("Got {count} user/s", users.Length)
                | Error error -> logger.Warning("Unable to get users -> {error}", error)
                reply.Reply result
                return! loop imUserDict
            | CreateUser(userId, UserName userName, Password password, userType, imageUrl, reply) ->
                let userName, password, imageUrl = UserName(userName.Trim()), Password(password.Trim()), canoicalizeImageUrl imageUrl
                let result = imUserDict |> addUser userId userName password userType imageUrl
                match result with
                | Ok user -> logger.Debug("Created {user}", user)
                | Error error -> logger.Warning("Unable to create {userName} -> {error}", userName, error)
                reply.Reply result
                return! loop imUserDict
            | ResetPassword(byUserId, userId, Password password, rvn, reply) ->
                let password = Password(password.Trim())
                let result = result {
                    let! byImUser = imUserDict |> findUserId byUserId
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = validateRvn imUser.User.Rvn rvn |> errorIfSome ()
                    let! _ = if hash password imUser.Salt = imUser.Hash then Error "New password must not be the same as existing password" else Ok()
                    let user = { imUser.User with Rvn = incrementRvn rvn }
                    let salt = salt()
                    let mustChangePasswordReason =
                        match imUser.MustChangePasswordReason with
                        | Some FirstSignIn -> Some FirstSignIn
                        | _ -> Some(MustChangePasswordReason.PasswordReset byImUser.User.UserName)
                    let imUser = { imUser with User = user ; Salt = salt ; Hash = hash password salt ; MustChangePasswordReason = mustChangePasswordReason }
                    let! _ = imUserDict |> updateUser imUser
                    return user }
                match result with
                | Ok user -> logger.Debug("Password reset for {user}", user)
                | Error error -> logger.Warning("Unable to reset password for {userId} -> {error}", userId, error)
                reply.Reply result
                return! loop imUserDict
            | ChangeUserType(userId, userType, rvn, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = validateRvn imUser.User.Rvn rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = incrementRvn rvn ; UserType = userType }
                    let imUser = { imUser with User = user }
                    let! _ = imUserDict |> updateUser imUser
                    return user }
                match result with
                | Ok user -> logger.Debug("User type changed for {user}", user)
                | Error error -> logger.Warning("Unable to change user type for {userId} -> {error}", userId, error)
                reply.Reply result
                return! loop imUserDict }
        logger.Information("Starting...")
        loop (ImUserDict()))
    do agent.Error.Add (fun exn -> logger.Error("Unexpected error -> {errorMessage}", exn.Message))
    interface IUserRepo with
        member __.SignIn(userName, password) = agent.PostAndAsyncReply(fun reply -> SignIn(userName, password, reply))
        member __.AutoSignIn(userId) = agent.PostAndAsyncReply(fun reply -> AutoSignIn(userId, reply))
        member __.ChangePassword(userId, password, rvn) = agent.PostAndAsyncReply(fun reply -> ChangePassword(userId, password, rvn, reply))
        member __.ChangeImageUrl(userId, imageUrl, rvn) = agent.PostAndAsyncReply(fun reply -> ChangeImageUrl(userId, imageUrl, rvn, reply))
        member __.GetUsers() = agent.PostAndAsyncReply(GetUsers)
        member __.CreateUser(userId, userName, password, userType, imageUrl) = agent.PostAndAsyncReply(fun reply -> CreateUser(userId, userName, password, userType, imageUrl, reply))
        member __.ResetPassword(byUserId, userId, password, rvn) = agent.PostAndAsyncReply(fun reply -> ResetPassword(byUserId, userId, password, rvn, reply))
        member __.ChangeUserType(userId, userType, rvn) = agent.PostAndAsyncReply(fun reply -> ChangeUserType(userId, userType, rvn, reply))
