module Aornota.Gibet.DevConsole.TestUserRepoAndApi

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo.InMemoryUserRepoAgent
open Aornota.Gibet.Server.Repo.IUserRepo
open Aornota.Gibet.Server.Api.UserApiAgent

open System

open FsToolkit.ErrorHandling

open Serilog

let [<Literal>] private LOG_SOURCE = "DevConsole.Users"

let private hub = { // note: fake implementation that literally does nothing!
    new IHub<HubState, RemoteServerInput, RemoteUiInput> with
        member __.GetModels() = []
        member __.BroadcastClient msg = ()
        member __.BroadcastServer msg = ()
        member __.SendClientIf predicate msg = ()
        member __.SendServerIf predicate msg = () }

let testUserRepoAndApi() = asyncResult { // cf. Aornota.Gibet.Server.Repo.UserTestData.create
    // #region UserIds &c.
    let initialPassword = Password "password" // note: invalid for IUserApi - but okay for IUserRepo
    let nephId, neph, nephPassword, nephImageUrl =
        UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "neph", Password "nephhpen", ImageUrl "https://aornota.github.io/djnarration/public/resources/djnarration-24x24.png"
    let rosieId, rosie = UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName "rosie"
    let hughId, hugh, hughPassword = UserId(Guid("00000000-0000-0002-0000-000000000000")), UserName "hugh", Password "hughhguh"
    let willId, will = UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "will"
    let satan, satanPassword = UserName "satan", Password "blzbub"
    // #endregion
    // #region IMemoryUserRepoAgent
    Log.Logger.Information(sourced "Testing user repository..." LOG_SOURCE)
    let userRepo = InMemoryUserRepoAgent(Log.Logger) :> IUserRepo
    let! nephUser = userRepo.CreateUser(Some nephId, neph, initialPassword, BenevolentDictatorForLife, Some nephImageUrl)
    let! _ = userRepo.CreateUser(Some rosieId, rosie, initialPassword, Administrator, None)
    let! hughUser = userRepo.CreateUser(Some hughId, hugh, initialPassword, Pleb, None)
    let! _ = userRepo.CreateUser(Some willId, will, initialPassword, Pleb, None)
    Log.Logger.Information(sourced "...user repository tested" LOG_SOURCE)
    // #endregion
    // #region UserApiAgent
    Log.Logger.Information(sourced "Testing user Api..." LOG_SOURCE)
    let userApi, connectionId = UserApiAgent(userRepo, hub, Log.Logger), ConnectionId.Create()
    let! authUser, _ = userApi.SignIn(connectionId, neph, initialPassword)
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
    Log.Logger.Information(sourced "...user Api tested" LOG_SOURCE)
    // #endregion
    return () }
