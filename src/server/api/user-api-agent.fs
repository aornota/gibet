module Aornota.Gibet.Server.Api.UserApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Api.UserApi
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
// TODO-NMB...open Aornota.Gibet.Server.Jwt
open Aornota.Gibet.Server.Repo.IUserRepo

open System.Collections.Generic

open Elmish.Bridge

open FsToolkit.ErrorHandling

open Serilog

(* TODO-NMB...Enforces unique UserName.
   TODO-NMB...Enforces length (&c.) restrictions on UserName and Password.
   Does not enforce unique UserId (but IUserRepo does).
   Does not enforce actual/expected Rvn/s (but IUserRepo does). *)

type private Input =
    | SignIn of Connection * UserName * Password * AsyncReplyChannelResult<AuthUser * MustChangePasswordReason option, string>
    | AutoSignIn of Connection * Jwt * AsyncReplyChannelResult<AuthUser * MustChangePasswordReason option, string>
    | SignOut of Connection * Jwt * AsyncReplyChannelResult<unit, string>
    | GetUsers of Connection * Jwt * AsyncReplyChannelResult<User list, string>
    | CreateUser of Connection * Jwt * UserName * Password * UserType * AsyncReplyChannelResult<unit, string>
    | ChangePassword of Connection * Jwt * Password * Rvn * AsyncReplyChannelResult<unit, string>
    | ResetPassword of Connection * Jwt * UserId * Password * Rvn * AsyncReplyChannelResult<unit, string>
    | ChangeUserType of Connection * Jwt * UserId * UserType * Rvn * AsyncReplyChannelResult<unit, string>

type private UserDict = Dictionary<UserId, User>

type UserApiAgent(userRepo:IUserRepo, hub:ServerHub<HubState, ServerInput, RemoteUiInput>, logger:ILogger) =
    let agent = MailboxProcessor<_>.Start(fun inbox ->
        let rec loop (userDict:UserDict) = async {
            let! input = inbox.Receive ()
            (* TEMP-NMB...
            do! ifDebugSleepAsync 100 500 *)
            match input with
            | SignIn(connection, userName, password, reply) -> // TODO-NMB: Handle Jwt properly...
                let! repoResult = (userName, password) |> userRepo.SignIn
                let result = result {
                    let! (userId, mustChangePasswordReason) = repoResult
                    let! tuple =
                        if debugFakeError () then sprintf "Fake SignIn error: %A" userName |> Error
                        else if userId |> userDict.ContainsKey then
                            let user = userDict.[userId]
                            let jwt = "Fake Jwt!" |> Jwt // TEMP-NMB (see also GetUsers(...) below)...
                            ({ User = user ; Jwt = jwt }, mustChangePasswordReason) |> Ok
                        else INVALID_CREDENTIALS |> Error
                    return tuple }
                result |> reply.Reply
                return! userDict |> loop
            | AutoSignIn(connection, Jwt jwt, reply) -> // TODO-NMB...
                return! userDict |> loop
            | SignOut(connection, Jwt jwt, reply) -> // TODO-NMB...
                return! userDict |> loop
            | GetUsers(connection, Jwt jwt, reply) -> // TODO-NMB: Verify jwt properly...
                let result =
                    if debugFakeError () then sprintf "Fake GetUsers error: %A" jwt |> Error
                    else if jwt = "Fake Jwt!" then // TEMP-NMB (see also SignIn(...) above)...
                        userDict.Values |> List.ofSeq |> Ok
                    else NOT_ALLOWED |> Error
                match result with
                | Ok users ->  logger.Debug("Got {count} User/s", users.Length)
                | Error error -> logger.Warning("Unable to get Users: {error}", error)
                result |> reply.Reply
                return! userDict |> loop
            | CreateUser(connection, Jwt jwt, userName, password, userType, reply) -> // TODO-NMB...
                return! userDict |> loop
            | ChangePassword(connection, Jwt jwt, password, rvn, reply) -> // TODO-NMB...
                return! userDict |> loop
            | ResetPassword(connection, Jwt jwt, userId, password, rvn, reply) -> // TODO-NMB...
                return! userDict|> loop
            | ChangeUserType(connection, Jwt jwt, userId, userType, rvn, reply) -> // TODO-NMB...
                return! userDict |> loop }
        logger.Information("Starting UserApi agent...")
        let userDict = UserDict()
        match userRepo.GetUsers() |> Async.RunSynchronously with
        | Ok users ->
            if users.Length > 0 then users |> List.iter (fun user -> (user.UserId, user) |> userDict.Add)
            else logger.Warning("No Users in IUserRepo")
        | Error error -> logger.Warning("Unable to get Users: {error}", error)
        userDict |> loop)
    do agent.Error.Add (fun exn -> logger.Error("Unexpected error: {message}", exn.Message))
    member __.SignIn(connection, userName, password) = (fun reply -> (connection, userName, password, reply) |> SignIn) |> agent.PostAndAsyncReply
    member __.AutoSignIn(connection, jwt) = (fun reply -> (connection, jwt, reply) |> AutoSignIn) |> agent.PostAndAsyncReply
    member __.SignOut(connection, jwt) = (fun reply -> (connection, jwt, reply) |> SignOut) |> agent.PostAndAsyncReply
    member __.GetUsers(connection, jwt) = (fun reply -> (connection, jwt, reply) |> GetUsers) |> agent.PostAndAsyncReply
    member __.CreateUser(connection, jwt, userName, password, userType) = (fun reply -> (connection, jwt, userName, password, userType, reply) |> CreateUser) |> agent.PostAndAsyncReply
    member __.ChangePassword(connection, jwt, password, rvn) = (fun reply -> (connection, jwt, password, rvn, reply) |> ChangePassword) |> agent.PostAndAsyncReply
    member __.ResetPassword(connection, jwt, userId, password, rvn) = (fun reply -> (connection, jwt, userId, password, rvn, reply) |> ResetPassword) |> agent.PostAndAsyncReply
    member __.ChangeUserType(connection, jwt, userId, userType, rvn) = (fun reply -> (connection, jwt, userId, userType, rvn, reply) |> ChangeUserType) |> agent.PostAndAsyncReply

let userApiReader = reader {
    let! userApi = resolve<UserApiAgent>()
    return {
        signIn = userApi.SignIn
        autoSignIn = userApi.AutoSignIn
        signOut = userApi.SignOut
        getUsers = userApi.GetUsers
        createUser = userApi.CreateUser
        changePassword = userApi.ChangePassword
        resetPassword = userApi.ResetPassword
        changeUserType = userApi.ChangeUserType } }
