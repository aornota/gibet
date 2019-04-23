module Aornota.Gibet.Server.Api.UserApiAgent

open Aornota.Gibet.Common
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Api.Connection
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
open Microsoft.AspNetCore.Http.Features

(* TODO-NMB...Enforces unique UserName.
   TODO-NMB...Enforces length (&c.) restrictions on UserName and Password.
   Prevents SignIn/AutoSignIn if PersonaNonGrata.
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
            | SignIn(connection, userName, password, reply) -> // TODO-NMB: hub...
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
                    (* TODO-NMB: Interaction with hub, e.g. send UserSignedIn to *this* connection [to update HubState]...
                                 ...then bridge-state.transition sends RemoteUiInput to *other*-signed-in)... *)
                    return { User = user ; Jwt = jwt }, mustChangePasswordReason }
                result |> reply.Reply
                return! userDict |> loop
            | AutoSignIn(connection, jwt, reply) -> // TODO-NMB: hub...
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
                                            (* TODO-NMB: Interaction with hub, e.g. send UserSignedIn to *this* connection [to update HubState]...
                                                         ...then bridge-state.transition sends RemoteUiInput to *other*-signed-in)... *)
                                            ({ User = user ; Jwt = jwt }, mustChangePasswordReason) |> Ok
                                        | Error error -> error |> Error
                                else INVALID_CREDENTIALS |> Error
                            | Error error -> error |> Error
                    | Error error -> return error |> Error }
                result |> reply.Reply
                return! userDict |> loop
            | SignOut(connection, jwt, reply) -> // TODO-NMB: hub...
                let result = result {
                    let! _ = if debugFakeError () then sprintf "Fake SignOut error: %A" jwt |> Error else () |> Ok
                    let! _ = jwt |> fromJwt // all authenticated Users allowed to SignOut (so no need to check UserType)
                    // TODO-NMB: Interaction with hub, e.g. UserSignedOut (send to *this* connection [to update HubState], then transition sends RemoteUiInput/s)...
                    return () }
                result |> reply.Reply
                return! userDict |> loop
            | GetUsers(connection, jwt, reply) -> // TODO-NMB: hub...
                let result = result {
                    let! _ = if debugFakeError () then sprintf "Fake GetUsers error: %A" jwt |> Error else () |> Ok
                    let! _ = jwt |> fromJwt // all authenticated Users allowed to GetUsers (so no need to check UserType)
                    // TODO-NMB: Interaction with hub, e.g. GotUsers (send to *this* connection [to update HubState])...
                    return userDict.Values |> List.ofSeq }
                match result with
                | Ok users ->  logger.Debug("Got {count} User/s", users.Length)
                | Error error -> logger.Warning("Unable to get Users: {error}", error)
                result |> reply.Reply
                return! userDict |> loop
            | CreateUser(connection, jwt, userName, password, userType, reply) -> // TODO-NMB...
                return! userDict |> loop
            | ChangePassword(connection, jwt, password, rvn, reply) -> // TODO-NMB...
                return! userDict |> loop
            | ResetPassword(connection, jwt, userId, password, rvn, reply) -> // TODO-NMB...
                return! userDict|> loop
            | ChangeUserType(connection, jwt, userId, userType, rvn, reply) -> // TODO-NMB...
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
