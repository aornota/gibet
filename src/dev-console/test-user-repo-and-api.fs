module Aornota.Gibet.DevConsole.TestUserRepoAndApi

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Api.UserApiAgent
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo.InMemoryUserRepoAgent
open Aornota.Gibet.Server.Repo.IUserRepo

open System

open FsToolkit.ErrorHandling

open Serilog

let private logger = Log.Logger |> sourcedLogger "DevConsole.TestUserRepoAndApi"

let private hub = { // note: fake implementation that literally does nothing!
    new IHub<HubState, RemoteServerInput, RemoteUiInput> with
        member __.GetModels() = []
        member __.BroadcastClient msg = ()
        member __.BroadcastServer msg = ()
        member __.SendClientIf predicate msg = ()
        member __.SendServerIf predicate msg = () }

let testUserRepoAndApi() = asyncResult { // cf. Aornota.Gibet.Server.Repo.UserTestData.create
    // #region UserIds &c.
    // Note: Some user names and passwords would be invalid for IUserApi - but are okay for IUserRepo
    let defaultPassword = Password "password"
    let nephId, neph, nephPassword = UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "neph", Password "nephhpen"
    let nephImageUrl = ImageUrl "https://avatars0.githubusercontent.com/u/14148307?s=460&v=4"
    let rosieId, rosie = UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName "rosie"
    let hughId, hugh, hughPassword = UserId(Guid("00000000-0000-0002-0000-000000000000")), UserName "hugh", Password "hughhguh"
    let willId, will = UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "will"
    let satan, satanPassword = UserName "satan", Password "blzbub"
    // #endregion
    // #region IMemoryUserRepoAgent
    logger.Information("Testing user repository...")
    let userRepo = InMemoryUserRepoAgent(Log.Logger) :> IUserRepo
    let! nephUser = userRepo.CreateUser(Some nephId, neph, defaultPassword, BenevolentDictatorForLife, Some nephImageUrl)
    let! _ = userRepo.CreateUser(Some rosieId, rosie, defaultPassword, Administrator, None)
    let! hughUser = userRepo.CreateUser(Some hughId, hugh, defaultPassword, Pleb, None)
    let! _ = userRepo.CreateUser(Some willId, will, defaultPassword, Pleb, None)
    logger.Information("...user repository tested")
    // #endregion
    // #region UserApiAgent
    logger.Information("Testing user Api...")
    let userApi, connectionId = UserApiAgent(userRepo, hub, Log.Logger), ConnectionId.Create()
    let! authUser, _ = userApi.SignIn(connectionId, neph, defaultPassword)
    let! _ = userApi.GetUsers(connectionId, authUser.Jwt)
    let nephRvn = nephUser.Rvn
    let! _ = userApi.ChangePassword(authUser.Jwt, nephPassword, nephRvn)
    let nephRvn = incrementRvn nephRvn
    let! _ = userApi.ChangeImageUrl(authUser.Jwt, None, nephRvn)
    let nephRvn = incrementRvn nephRvn
    let hughRvn = hughUser.Rvn
    let! _ = userApi.ResetPassword(authUser.Jwt, hughId, hughPassword, hughRvn)
    let hughRvn = incrementRvn hughRvn
    let! _ = userApi.ChangeUserType(authUser.Jwt, hughId, Administrator, hughRvn)
    let hughRvn = incrementRvn hughRvn
    let! _ = userApi.SignOut(connectionId, authUser.Jwt)
    let! authUser, _ = userApi.AutoSignIn(connectionId, authUser.Jwt)
    let! _ = userApi.CreateUser(authUser.Jwt, satan, satanPassword, PersonaNonGrata, None)
    logger.Information("...user Api tested")
    // #endregion
    return () }
