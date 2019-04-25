module Aornota.Gibet.Server.Repo.InMemoryUserRepoAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Server.Repo.IUserRepo

open System.Collections.Generic

open FsToolkit.ErrorHandling

open Serilog

(* Enforces unique UserId.
   Enforces actual/expected Rvn/s.
   Does not enforce unique UserName (but cannot sign in with non-unique UserName).
   Does not enforce any length (&c.) restrictions on UserName or Password.
   Does not prevent SignIn/AutoSignIn if PersonaNonGrata. *)

type private Input =
    | SignIn of UserName * Password * AsyncReplyChannelResult<UserId * MustChangePasswordReason option, string>
    | AutoSignIn of UserId * AsyncReplyChannelResult<UserId * MustChangePasswordReason option, string>
    | GetUsers of AsyncReplyChannelResult<User list, string>
    | CreateUser of UserId option * UserName * Password * UserType * AsyncReplyChannelResult<User, string>
    | ChangePassword of UserId * Password * Rvn * AsyncReplyChannelResult<User, string>
    | ResetPassword of UserId * Password * Rvn * AsyncReplyChannelResult<User, string>
    | ChangeUserType of UserId * UserType * Rvn * AsyncReplyChannelResult<User, string>

type private InMemoryUser = {
    User : User
    Salt : Salt
    Hash : Hash
    MustChangePasswordReason : MustChangePasswordReason option }

type private ImUserDict = Dictionary<UserId, InMemoryUser>

let private addUser userId userName password userType (imUserDict:ImUserDict) =
    let userId = userId |> Option.defaultValue (UserId.Create())
    let result = result {
        let! _ =
            if imUserDict.ContainsKey userId then ifDebug (sprintf "InMemoryUserRepoAgent.addUser -> %A already exists" userId) UNEXPECTED_ERROR |> Error
            else () |> Ok
        let user = {
            UserId = userId
            Rvn = initialRvn
            UserName = userName
            UserType = userType }
        let salt = salt()
        let imUser = {
            User = user
            Salt = salt
            Hash = hash(password, salt)
            MustChangePasswordReason = FirstSignIn |> Some }
        (userId, imUser) |> imUserDict.Add
        return imUser.User }
    result

let private updateUser imUser (imUserDict:ImUserDict) =
    let userId = imUser.User.UserId
    if imUserDict.ContainsKey userId then
        imUserDict.[userId] <- imUser
        () |> Ok
    else ifDebug (sprintf "InMemoryUserRepoAgent.updateUser -> Unable to update %A" userId) UNEXPECTED_ERROR |> Error

let private findUserId userId (imUserDict:ImUserDict) =
    if imUserDict.ContainsKey userId then imUserDict.[userId] |> Ok
    else ifDebug (sprintf "InMemoryUserRepoAgent.findUserId -> Unable to find %A" userId) UNEXPECTED_ERROR |> Error

let private findUserName userName error (imUserDict:ImUserDict) =
    match imUserDict.Values |> List.ofSeq |> List.filter (fun imUser -> imUser.User.UserName = userName) with
    | [ imUser ] -> imUser |> Ok
    | _ :: _ | [] -> error |> Error

type InMemoryUserRepoAgent(logger:ILogger) =
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop (imUserDict:ImUserDict) = async {
            match! inbox.Receive() with
            | SignIn(userName, password, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserName userName (ifDebug (sprintf "InMemoryUserRepoAgent.SignIn -> %A not found" userName) INVALID_CREDENTIALS)
                    let! _ =
                        if imUser.Hash = hash(password, imUser.Salt) then () |> Ok
                        else ifDebug (sprintf "InMemoryUserRepoAgent.SignIn -> Invalid password for %A" userName) INVALID_CREDENTIALS |> Error
                    return (imUser.User.UserId, imUser.MustChangePasswordReason) }
                match result with
                | Ok _ -> logger.Debug("Able to sign in as {userName}", userName)
                | Error error -> logger.Warning("Unable to sign in as {userName} -> {error}", userName, error)
                result |> reply.Reply
                return! imUserDict |> loop
            | AutoSignIn(userId, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    return (imUser.User.UserId, imUser.MustChangePasswordReason) }
                match result with
                | Ok _ -> logger.Debug("Able to automatically sign in as {userId}", userId)
                | Error error -> logger.Warning("Unable to automatically sign in as {userId} -> {error}", userId, error)
                result |> reply.Reply
                return! imUserDict |> loop
            | ChangePassword(userId, password, rvn, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = rvn |> validateRvn imUser.User.Rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = rvn |> incrementRvn }
                    let salt = salt()
                    let imUser = { imUser with User = user ; Salt = salt ; Hash = (password, salt) |> hash ; MustChangePasswordReason = None }
                    let! _ = imUserDict |> updateUser imUser
                    return user }
                match result with
                | Ok user -> logger.Debug("Password changed for {user}", user)
                | Error error -> logger.Warning("Unable to change password for {userId} -> {error}", userId, error)
                result |> reply.Reply
                return! imUserDict |> loop
            | GetUsers reply ->
                let result = imUserDict.Values |> List.ofSeq |> List.map (fun imUser -> imUser.User) |> Ok
                result |> reply.Reply
                return! imUserDict |> loop
            | CreateUser(userId, userName, password, userType, reply) ->
                let result = imUserDict |> addUser userId userName password userType
                match result with
                | Ok user -> logger.Debug("Added {user}", user)
                | Error error -> logger.Warning("Unable to add {userName} -> {error}", userName, error)
                result |> reply.Reply
                return! imUserDict |> loop
            | ResetPassword(userId, password, rvn, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = rvn |> validateRvn imUser.User.Rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = rvn |> incrementRvn }
                    let salt = salt()
                    let imUser = { imUser with User = user ; Salt = salt ; Hash = (password, salt) |> hash ; MustChangePasswordReason = MustChangePasswordReason.PasswordReset |> Some }
                    let! _ = imUserDict |> updateUser imUser
                    return user }
                match result with
                | Ok user -> logger.Debug("Password reset for {user}", user)
                | Error error -> logger.Warning("Unable to reset password for {userId} -> {error}", userId, error)
                result |> reply.Reply
                return! imUserDict |> loop
            | ChangeUserType(userId, userType, rvn, reply) ->
                let result = result {
                    let! imUser = imUserDict |> findUserId userId
                    let! _ = rvn |> validateRvn imUser.User.Rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = rvn |> incrementRvn ; UserType = userType }
                    let imUser = { imUser with User = user }
                    let! _ = imUserDict |> updateUser imUser
                    return user }
                match result with
                | Ok user -> logger.Debug("User type changed for {user}", user)
                | Error error -> logger.Warning("Unable to change user type for {userId} -> {error}", userId, error)
                result |> reply.Reply
                return! imUserDict |> loop }
        logger.Information("Starting InMemoryUserRepoAgent...")
        ImUserDict() |> loop)
    do agent.Error.Add (fun exn -> logger.Error("Unexpected InMemoryUserRepoAgent error -> {message}", exn.Message))
    interface IUserRepo with
        member __.SignIn(userName, password) = (fun reply -> (userName, password, reply) |> SignIn) |> agent.PostAndAsyncReply
        member __.AutoSignIn(userId) = (fun reply -> (userId, reply) |> AutoSignIn) |> agent.PostAndAsyncReply
        member __.ChangePassword(userId, password, rvn) = (fun reply -> (userId, password, rvn, reply) |> ChangePassword) |> agent.PostAndAsyncReply
        member __.GetUsers() = (fun reply -> reply |> GetUsers) |> agent.PostAndAsyncReply
        member __.CreateUser(userId, userName, password, userType) = (fun reply -> (userId, userName, password, userType, reply) |> CreateUser) |> agent.PostAndAsyncReply
        member __.ResetPassword(userId, password, rvn) = (fun reply -> (userId, password, rvn, reply) |> ResetPassword) |> agent.PostAndAsyncReply
        member __.ChangeUserType(userId, userType, rvn) = (fun reply -> (userId, userType, rvn, reply) |> ChangeUserType) |> agent.PostAndAsyncReply
