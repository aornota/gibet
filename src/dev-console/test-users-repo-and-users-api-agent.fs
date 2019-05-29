module Aornota.Gibet.DevConsole.TestUsersRepoAndUsersApiAgent

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Api.UsersApiAgent
open Aornota.Gibet.Server.Authenticator
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Repo.IUsersRepo
open Aornota.Gibet.Server.SourcedLogger

open System

open Microsoft.Extensions.Configuration

open FsToolkit.ErrorHandling

let [<Literal>] private SOURCE = "TestUsersRepoAndUsersApiAgent"

let testUsersRepoAndUsersApiAgent (configuration:IConfiguration) logger = asyncResult { // cf. Aornota.Gibet.Server.InitialUsers
    let sourcedLogger = logger |> sourcedLogger SOURCE
    sourcedLogger.Information("Testing UsersRepo and UsersApiAgent...")
    let yves = UserName "yves strop"
    let yvesDto = {
        User = {
            UserId = UserId(Guid("00000000-0000-0000-0000-000000000000"))
            Rvn = initialRvn
            UserName = yves
            UserType = BenevolentDictatorForLife
            ImageUrl = None }
        Salt = Salt "27ZLmf7pVqIum6UnDZr+Kve9xJGJsacauLY8OnANEAw="
        Hash = Hash "fDvhhO6pcJ/YhmoSkfmUJ111MfKzCLe0TNFBZi4R0b3CzYTBhgC0Z4LYalSGoM8Ae04xdNXgBjS5lGLhV7nt1w=="
        MustChangePasswordReason = Some FirstSignIn }
    let fakeUsersRepo = {
        new IUsersRepo with
            member __.GetUsers() = async { return Ok([ yvesDto ]) }
            member __.AddUser _ = async { return Ok() }
            member __.UpdateUser _ = async { return Ok() } }
    (* Note: Create UsersApiAgent *after* initial Users have been added to [fake] Users repo - else will not be able to sign in as these initial Users (because UsersApiAgent populates its
       "cache" from Users repo when created). *)
    let fakeHub = {
        new IHub<HubState, RemoteServerInput, RemoteUiInput> with
            member __.GetModels() = []
            member __.BroadcastClient _ = ()
            member __.BroadcastServer _ = ()
            member __.SendClientIf _ _ = ()
            member __.SendServerIf _ _ = () }
    let usersApiAgent = UsersApiAgent(fakeUsersRepo, fakeHub, Authenticator(configuration, logger), logger)
    let connectionId = ConnectionId.Create()
    let defaultPassword = Password "gibet"
    let! authUser, _ = usersApiAgent.SignIn(connectionId, yves, defaultPassword)
    let jwt = authUser.Jwt
    let! _ = usersApiAgent.GetUsers(connectionId, jwt)
    let yvesRvn = authUser.User.Rvn
    let! _ = usersApiAgent.ChangePassword(jwt, Password "brigge", yvesRvn)
    let yvesRvn = incrementRvn yvesRvn
    let! _ = usersApiAgent.ChangeImageUrl(jwt, Some(ImageUrl "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRZrSarOxyHO_FbyMvr8uJTeoSqgGiAIr3m9EhXqy_i28kBRV8S") (* Rainer Maria Rilke *), yvesRvn)
    let! annId, _ = usersApiAgent.CreateUser(jwt, UserName "ann ewity", defaultPassword, Pleb)
    let! _ = usersApiAgent.ChangeUserType(jwt, annId, Administrator, initialRvn)
    let! henriettaId, _ = usersApiAgent.CreateUser(jwt, UserName "henrietta viennetta", defaultPassword, Pleb)
    let! _ = usersApiAgent.ResetPassword(jwt, henriettaId, Password "pancetta", initialRvn)
    let! _ = usersApiAgent.CreateUser(jwt, UserName "mike hatstead", defaultPassword, Pleb)
    let! _ = usersApiAgent.SignOut(connectionId, jwt)
    let! _ = usersApiAgent.AutoSignIn(connectionId, jwt)
    let! _ = usersApiAgent.CreateUser(jwt, UserName "mike oxard", defaultPassword, PersonaNonGrata)
    let! _ = usersApiAgent.SignOut(connectionId, jwt)
    sourcedLogger.Information("...UsersRepo and UsersApiAgent tested")
    return () }
