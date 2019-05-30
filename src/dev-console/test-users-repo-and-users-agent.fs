module Aornota.Gibet.DevConsole.TestUsersRepoAndUsersAgent

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Agents.UsersAgent
open Aornota.Gibet.Server.Authenticator
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Repo.IUsersRepo
open Aornota.Gibet.Server.SourcedLogger

open System

open Microsoft.Extensions.Configuration

open FsToolkit.ErrorHandling

let [<Literal>] private SOURCE = "TestUsersRepoAndUsersAgent"

let testUsersRepoAndUsersAgent (configuration:IConfiguration) logger = asyncResult { // cf. Aornota.Gibet.Server.InitialUsers
    let sourcedLogger = logger |> sourcedLogger SOURCE
    sourcedLogger.Information("Testing UsersRepo and UsersAgent...")
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
    (* Note: Create UsersAgent *after* initial Users have been added to [fake] Users repo - else will not be able to sign in as these initial Users (because UsersAgent populates its
       "cache" from Users repo when created). *)
    let fakeHub = {
        new IHub<HubState, RemoteServerInput, RemoteUiInput> with
            member __.GetModels() = []
            member __.BroadcastClient _ = ()
            member __.BroadcastServer _ = ()
            member __.SendClientIf _ _ = ()
            member __.SendServerIf _ _ = () }
    let usersAgent = UsersAgent(fakeUsersRepo, fakeHub, Authenticator(configuration, logger), logger)
    let connectionId = ConnectionId.Create()
    let defaultPassword = Password "gibet"
    let! authUser, _ = usersAgent.SignIn(connectionId, yves, defaultPassword)
    let jwt = authUser.Jwt
    let! _ = usersAgent.GetUsers(connectionId, jwt)
    let yvesRvn = authUser.User.Rvn
    let! _ = usersAgent.ChangePassword(jwt, Password "brigge", yvesRvn)
    let yvesRvn = incrementRvn yvesRvn
    let! _ = usersAgent.ChangeImageUrl(jwt, Some(ImageUrl "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRZrSarOxyHO_FbyMvr8uJTeoSqgGiAIr3m9EhXqy_i28kBRV8S") (* Rainer Maria Rilke *), yvesRvn)
    let! annId, _ = usersAgent.CreateUser(jwt, UserName "ann ewity", defaultPassword, Pleb)
    let! _ = usersAgent.ChangeUserType(jwt, annId, Administrator, initialRvn)
    let! henriettaId, _ = usersAgent.CreateUser(jwt, UserName "henrietta viennetta", defaultPassword, Pleb)
    let! _ = usersAgent.ResetPassword(jwt, henriettaId, Password "pancetta", initialRvn)
    let! _ = usersAgent.CreateUser(jwt, UserName "mike hatstead", defaultPassword, Pleb)
    let! _ = usersAgent.SignOut(connectionId, jwt)
    let! _ = usersAgent.AutoSignIn(connectionId, jwt)
    let! _ = usersAgent.CreateUser(jwt, UserName "mike oxard", defaultPassword, PersonaNonGrata)
    let! _ = usersAgent.SignOut(connectionId, jwt)
    sourcedLogger.Information("...UsersRepo and UsersAgent tested")
    return () }
