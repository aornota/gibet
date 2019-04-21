module Aornota.Gibet.Server.Repo.InMemoryUserRepo

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.ResilientMailbox
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Repo.IUserRepo

open System.Collections.Generic

open FsToolkit.ErrorHandling

open Serilog

(* Enforces unique UserId.
   Enforces actual/expected Rnv/s.
   Does not enforce unique UserName (but cannot sign in with non-unique UserName).
   Does not enforce any length (&c.) restrictions on UserName or Password. *)

type private Input =
    | SignIn of UserName * Password * AsyncReplyChannel<Result<UserId, string>>
    | GetUsers of AsyncReplyChannel<Result<User list, string>>
    | CreateUser of UserId option * UserName * Password * UserType * AsyncReplyChannel<Result<User, string>>
    | ChangePassword of UserId * Password * Rvn * AsyncReplyChannel<Result<User, string>>
    | ResetPassword of UserId * Password * Rvn * AsyncReplyChannel<Result<User, string>>
    | ChangeUserType of UserId * UserType * Rvn * AsyncReplyChannel<Result<User, string>>

type private ImMemoryUser = {
    User : User
    Salt : Salt
    Hash : Hash }

type private UserDict = Dictionary<UserId, ImMemoryUser>

let [<Literal>] INVALID_CREDENTIALS = "Invalid credentials"

let private addUser (imUsers:UserDict) (logger:ILogger) (userId, userName, password, userType) =
    let userId = userId |> Option.defaultValue (UserId.Create())
    let result = result {
        let! _ =
            if imUsers.ContainsKey userId then sprintf "%A already exists" userId |> Error
            else () |> Ok
        let user = {
            UserId = userId
            Rvn = initialRvn
            UserName = userName
            UserType = userType
            MustChangePasswordReason = FirstSignIn |> Some
            LastActivity = None }
        let salt = salt()
        let imUser = { User = user ; Salt = salt ; Hash = hash(password, salt) }
        (userId, imUser) |> imUsers.Add
        return imUser.User }
    match result with
    | Ok user -> logger.Debug("Added {user}", user)
    | Error error -> logger.Warning("Unable to add {userName}: {error}", userName, error)
    result

let private updateUser (imUsers:UserDict) imUser =
    let userId = imUser.User.UserId
    if imUsers.ContainsKey userId then
        imUsers.[userId] <- imUser
        () |> Ok
    else sprintf "Unable to update %A" userId |> Error

let private findUserId (imUsers:UserDict) userId =
    if imUsers.ContainsKey userId then imUsers.[userId] |> Ok
    else sprintf "Unable to find %A" userId |> Error

let private findUserName (imUsers:UserDict) error userName =
    match imUsers.Values |> List.ofSeq |> List.filter (fun imUser -> imUser.User.UserName = userName) with
    | [ imUser ] -> imUser |> Ok
    | _ :: _ | [] -> error |> Error

type InMemoryUserRepo(logger:ILogger) =
    let agent = ResilientMailbox<_>.Start(fun inbox ->
        let rec loop (imUsers:UserDict)  = async {
            match! inbox.Receive() with
            | SignIn(userName, password, reply) ->
                let result = result {
                    let! imUser = userName |> findUserName imUsers INVALID_CREDENTIALS
                    let! _ =
                        if imUser.User.UserType = PersonaNonGrata then INVALID_CREDENTIALS |> Error
                        else () |> Ok
                    let! _ =
                        if imUser.Hash = hash(password, imUser.Salt) then () |> Ok
                        else INVALID_CREDENTIALS |> Error
                    return imUser.User.UserId }
                match result with
                | Ok _ -> logger.Debug("Able to sign in as {userName}", userName)
                | Error error -> logger.Warning("Unable to sign in as {userName}: {error}", userName, error)
                result |> reply.Reply
                return! imUsers |> loop
            | GetUsers reply ->
                let result = imUsers.Values |> List.ofSeq |> List.map (fun imUser -> imUser.User) |> Ok
                result |> reply.Reply
                return! imUsers |> loop
            | CreateUser(userId, userName, password, userType, reply) ->
                let result = (userId, userName, password, userType) |> addUser imUsers logger
                result |> reply.Reply
                return! imUsers |> loop
            | ChangePassword(userId, password, rvn, reply) ->
                let result = result {
                    let! imUser = userId |> findUserId imUsers
                    let! _ = rvn |> validateRvn imUser.User.Rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = rvn |> incrementRvn ; MustChangePasswordReason = None }
                    let salt = salt()
                    let imUser = { imUser with User = user ; Salt = salt ; Hash = (password, salt) |> hash }
                    let! _ = imUser |> updateUser imUsers
                    return user }
                match result with
                | Ok user -> logger.Debug("Password changed for {user}", user)
                | Error error -> logger.Warning("Unable to change password for {userId}: {error}", userId, error)
                result |> reply.Reply
                return! imUsers |> loop
            | ResetPassword(userId, password, rvn, reply) ->
                let result = result {
                    let! imUser = userId |> findUserId imUsers
                    let! _ = rvn |> validateRvn imUser.User.Rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = rvn |> incrementRvn ; MustChangePasswordReason = PasswordReset |> Some }
                    let salt = salt()
                    let imUser = { imUser with User = user ; Salt = salt ; Hash = (password, salt) |> hash }
                    let! _ = imUser |> updateUser imUsers
                    return user }
                match result with
                | Ok user -> logger.Debug("Password reset for {user}", user)
                | Error error -> logger.Warning("Unable to reset password for {userId}: {error}", userId, error)
                result |> reply.Reply
                return! imUsers |> loop
            | ChangeUserType(userId, userType, rvn, reply) ->
                let result = result {
                    let! imUser = userId |> findUserId imUsers
                    let! _ = rvn |> validateRvn imUser.User.Rvn |> errorIfSome ()
                    let user = { imUser.User with Rvn = rvn |> incrementRvn ; UserType = userType }
                    let imUser = { imUser with User = user }
                    let! _ = imUser |> updateUser imUsers
                    return user }
                match result with
                | Ok user -> logger.Debug("User type changed for {user}", user)
                | Error error -> logger.Warning("Unable to change user type for {userId}: {error}", userId, error)
                result |> reply.Reply
                return! imUsers |> loop }
        logger.Information("Starting InMemoryUserRepo agent...")
        UserDict() |> loop)
    do agent.OnError.Add (fun exn -> logger.Error("Unexpected error: {message}", exn.Message))
    interface IUserRepo with
        member __.SignIn(userName, password) = (fun reply -> (userName, password, reply) |> SignIn) |> agent.PostAndAsyncReply
        member __.GetUsers() = (fun reply -> reply |> GetUsers) |> agent.PostAndAsyncReply
        member __.CreateUser(userId, userName, password, userType) = (fun reply -> (userId, userName, password, userType, reply) |> CreateUser) |> agent.PostAndAsyncReply
        member __.ChangePassword(userId, password, rvn) = (fun reply -> (userId, password, rvn, reply) |> ChangePassword) |> agent.PostAndAsyncReply
        member __.ResetPassword(userId, password, rvn) = (fun reply -> (userId, password, rvn, reply) |> ResetPassword) |> agent.PostAndAsyncReply
        member __.ChangeUserType(userId, userType, rvn) = (fun reply -> (userId, userType, rvn, reply) |> ChangeUserType) |> agent.PostAndAsyncReply
