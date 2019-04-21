module Aornota.Gibet.Server.Api.UserApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Api.IUserApi
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.ResilientMailbox
open Aornota.Gibet.Common.Revision
// TODO-NMB...open Aornota.Gibet.Server.Jwt
open Aornota.Gibet.Server.Repo.IUserRepo

open System.Collections.Generic

open FsToolkit.ErrorHandling

open Serilog

(* TODO-NMB...Enforces unique UserName.
   TODO-NMB...Enforces length (&c.) restrictions on UserName and Password.
   Does not enforce unique UserId (but IUserRepo does).
   Does not enforce actual/expected Rvn/s (but IUserRepo does). *)

type private Input =
    | SignIn of UserName * Password * AsyncReplyChannelResult<AuthUser, string>
    | AutoSignIn of Jwt * AsyncReplyChannelResult<AuthUser, string>
    | SignOut of Jwt * AsyncReplyChannelResult<unit, string>
    | GetUsers of Jwt * AsyncReplyChannelResult<User list, string>
    | CreateUser of Jwt * UserName * Password * UserType * AsyncReplyChannelResult<unit, string>
    | ChangePassword of Jwt * Password * Rvn * AsyncReplyChannelResult<unit, string>
    | ResetPassword of Jwt * UserId * Password * Rvn * AsyncReplyChannelResult<unit, string>
    | ChangeUserType of Jwt * UserId * UserType * Rvn * AsyncReplyChannelResult<unit, string>

type private UserDict = Dictionary<UserId, User>

type UserApi(userRepo:IUserRepo, logger:ILogger) =
    let agent = ResilientMailbox<_>.Start(fun inbox ->
        let rec loop (userDict:UserDict) = async {
            match! inbox.Receive() with
            | SignIn(userName, password, reply) -> // TODO-NMB: Handle Jwt properly...
                let! userId = (userName, password) |> userRepo.SignIn
                let result = result {
                    let! userId = userId
                    let! authUser =
                        if userId |> userDict.ContainsKey then
                            let user = userDict.[userId]
                            let jwt = "Fake Jwt! Sad!" |> Jwt // TEMP-NMB (see also GetUsers(...) above)...
                            { User = user ; Jwt = jwt } |> Ok
                        else INVALID_CREDENTIALS |> Error
                    return authUser }
                result |> reply.Reply
                return! userDict |> loop
            | AutoSignIn(Jwt jwt, reply) -> // TODO-NMB...
                return! userDict |> loop
            | SignOut(Jwt jwt, reply) -> // TODO-NMB...
                return! userDict |> loop
            | GetUsers(Jwt jwt, reply) -> // TODO-NMB: Verify jwt properly...
                let result =
                    if jwt = "Fake Jwt! Sad!" then // TEMP-NMB (see also SignIn(...) above)...
                        userDict.Values |> List.ofSeq |> Ok
                    else NOT_ALLOWED |> Error
                match result with
                | Ok users ->  logger.Debug("Got {count} User/s", users.Length)
                | Error error -> logger.Warning("Unable to get Users: {error}", error)
                result |> reply.Reply
                return! userDict |> loop
            | CreateUser(Jwt jwt, userName, password, userType, reply) -> // TODO-NMB...
                return! userDict |> loop
            | ChangePassword(Jwt jwt, password, rvn, reply) -> // TODO-NMB...
                return! userDict |> loop
            | ResetPassword(Jwt jwt, userId, password, rvn, reply) -> // TODO-NMB...
                return! userDict|> loop
            | ChangeUserType(Jwt jwt, userId, userType, rvn, reply) -> // TODO-NMB...
                return! userDict |> loop }
        logger.Information("Starting UserApi agent...")
        let userDict = UserDict()
        match userRepo.GetUsers() |> Async.RunSynchronously with
        | Ok users -> users |> List.iter (fun user -> (user.UserId, user) |> userDict.Add)
        | Error _ -> logger.Warning("No Users in IUserRepo")
        userDict |> loop)
    do agent.OnError.Add (fun exn -> logger.Error("Unexpected error: {message}", exn.Message))
    member __.SignIn(userName, password) = (fun reply -> (userName, password, reply) |> SignIn) |> agent.PostAndAsyncReply
    member __.AutoSignIn(jwt) = (fun reply -> (jwt, reply) |> AutoSignIn) |> agent.PostAndAsyncReply
    member __.SignOut(jwt) = (fun reply -> (jwt, reply) |> SignOut) |> agent.PostAndAsyncReply
    member __.GetUsers(jwt) = (fun reply -> (jwt, reply) |> GetUsers) |> agent.PostAndAsyncReply
    member __.CreateUser(jwt, userName, password, userType) = (fun reply -> (jwt, userName, password, userType, reply) |> CreateUser) |> agent.PostAndAsyncReply
    member __.ChangePassword(jwt, password, rvn) = (fun reply -> (jwt, password, rvn, reply) |> ChangePassword) |> agent.PostAndAsyncReply
    member __.ResetPassword(jwt, userId, password, rvn) = (fun reply -> (jwt, userId, password, rvn, reply) |> ResetPassword) |> agent.PostAndAsyncReply
    member __.ChangeUserType(jwt, userId, userType, rvn) = (fun reply -> (jwt, userId, userType, rvn, reply) |> ChangeUserType) |> agent.PostAndAsyncReply

let userApiReader = reader {
    let! userApi = resolve<UserApi>()
    return {
        signIn = userApi.SignIn
        autoSignIn = userApi.AutoSignIn
        signOut = userApi.SignOut
        getUsers = userApi.GetUsers
        createUser = userApi.CreateUser
        changePassword = userApi.ChangePassword
        resetPassword = userApi.ResetPassword
        changeUserType = userApi.ChangeUserType } }
