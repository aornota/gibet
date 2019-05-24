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

let testUserRepoAndApi () = asyncResult { // cf. Aornota.Gibet.Server.Repo.UserTestData.create
    // #region UserIds &c.
    // Note: Some user names and passwords would be invalid for IUserApi - but are okay for IUserRepo
    let defaultPassword = Password "password"
    let yvesId, yves, yvesPassword = UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "yves strop", Password "brigge"
    let yvesImageUrl = ImageUrl "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRZrSarOxyHO_FbyMvr8uJTeoSqgGiAIr3m9EhXqy_i28kBRV8S" // Rainer Maria Rilke
    let annId, ann, annPassword = UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName EXAMPLE_ADMIN_USER_NAME, Password EXAMPLE_ADMIN_PASSWORD
    let annImageUrl = ImageUrl "https://upload.wikimedia.org/wikipedia/en/thumb/3/3e/Nan_Shepherd.jpg/220px-Nan_Shepherd.jpg" // Nan Shepherd
    let henriettaId, henrietta, henriettaPassword = UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "henrietta viennetta", Password "pancetta"
    let henriettaImageUrl = ImageUrl "https://i.pinimg.com/originals/d3/e0/ce/d3e0ceb34eeadb2563cf7783bead4e85.jpg" // Ursula Le Guin
    let mike1Id, mike1 = UserId(Guid("00000000-0000-0000-0002-000000000000")), UserName "mike hatstead"
    let mike1ImageUrl = ImageUrl "https://cdn-ed.versobooks.com/images/000000/455/John-Berger-37fedb298baa7ac93877ab8b7169366c.jpg" // John Berger
    let mike2, mike2Password = UserName "mike oxard", Password "priapic"
    let mike2ImageUrl = ImageUrl "https://mises-media.s3.amazonaws.com/styles/social_media_1200_x_1200/s3/static-page/img/zamyatin.jpg?itok=J9ilfOm8" // Yevgeny Zamyatin
    // #endregion
    // #region IMemoryUserRepoAgent
    logger.Information("Testing user repository...")
    let userRepo = InMemoryUserRepoAgent(Log.Logger) :> IUserRepo
    let! nephUser = userRepo.CreateUser(Some yvesId, yves, defaultPassword, BenevolentDictatorForLife, None)
    let! annUser = userRepo.CreateUser(Some annId, ann, annPassword, Pleb, Some annImageUrl)
    let! henriettaUser = userRepo.CreateUser(Some henriettaId, henrietta, defaultPassword, Pleb, Some henriettaImageUrl)
    let! _ = userRepo.CreateUser(Some mike1Id, mike1, defaultPassword, Pleb, Some mike1ImageUrl)
    logger.Information("...user repository tested")
    // #endregion
    // #region UserApiAgent
    logger.Information("Testing user Api...")
    let userApi, connectionId = UserApiAgent(userRepo, hub, Log.Logger), ConnectionId.Create()
    let! authUser, _ = userApi.SignIn(connectionId, yves, defaultPassword)
    let! _ = userApi.GetUsers(connectionId, authUser.Jwt)
    let yvesRvn = nephUser.Rvn
    let! _ = userApi.ChangePassword(authUser.Jwt, yvesPassword, yvesRvn)
    let yvesRvn = incrementRvn yvesRvn
    let! _ = userApi.ChangeImageUrl(authUser.Jwt, Some yvesImageUrl, yvesRvn)
    let yvesRvn = incrementRvn yvesRvn
    let annRvn = annUser.Rvn
    let! _ = userApi.ChangeUserType(authUser.Jwt, annId, Administrator, annRvn)
    let annRvn = incrementRvn annRvn
    let henriettaRvn = henriettaUser.Rvn
    let! _ = userApi.ResetPassword(authUser.Jwt, henriettaId, henriettaPassword, henriettaRvn)
    let henriettaRvn = incrementRvn henriettaRvn
    let! _ = userApi.SignOut(connectionId, authUser.Jwt)
    let! authUser, _ = userApi.AutoSignIn(connectionId, authUser.Jwt)
    let! _ = userApi.CreateUser(authUser.Jwt, mike2, mike2Password, PersonaNonGrata)
    logger.Information("...user Api tested")
    // #endregion
    return () }
