module Aornota.Gibet.Server.InitialUsers

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Agents.UsersAgent
open Aornota.Gibet.Server.Repo.IUsersRepo
open Aornota.Gibet.Server.SourcedLogger

open System

open FsToolkit.ErrorHandling

let [<Literal>] private SOURCE = "Server.InitialUsers"

let createInitialUsers hub authenticator logger = asyncResult {
    let sourcedLogger = logger |> sourcedLogger SOURCE
    sourcedLogger.Information("Creating initial User/s...")
    let yvesId, yves = UserId(Guid("00000000-0000-0000-0000-000000000000")), UserName "yves strop"
    let yvesDto = {
        User = {
            UserId = yvesId
            Rvn = initialRvn
            UserName = yves
            UserType = BenevolentDictatorForLife
            ImageUrl = Some(ImageUrl "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRZrSarOxyHO_FbyMvr8uJTeoSqgGiAIr3m9EhXqy_i28kBRV8S") (* Rainer Maria Rilke *) }
        Salt = Salt "uI53XJb5XYgu5Hq3GdFVIjD26qwuIqKE5R0sWZWNI9Y="
        Hash = Hash "xrXXaftJJ6Rrt716SJPIoCLqqaCgMnOg/Z1wHK7Nhx7weKwZyHEldXh0xS5Xa5aN+xFbWHxeb1uu50ztzBmsiQ=="
        MustChangePasswordReason = None }
    let annId, ann = UserId(Guid("00000000-0000-0000-0000-000000000001")), UserName EXAMPLE_USER_NAME__AE
    let annDto = {
        User = {
            UserId = annId
            Rvn = initialRvn
            UserName = ann
            UserType = Administrator
            ImageUrl = Some(ImageUrl "https://upload.wikimedia.org/wikipedia/en/thumb/3/3e/Nan_Shepherd.jpg/220px-Nan_Shepherd.jpg") (* Nan Shepherd *) }
        Salt = Salt "r5v1Z/6+Xuuf7I6F52jq8AzQKtqCCOAIvuHsKo8sQ6I="
        Hash = Hash "5RIgX5gpXtDZvhBzFW3TcEaAv5cn/u5pn4CrR1eUlCznQiTwwb8eYsTqusg39+8k9Ub2Pt9qyfK5YCiUqAVcYQ=="
        MustChangePasswordReason = None }
    sourcedLogger.Information("Creating fake IUsersRepo containing {yves} ({yvesId}) and {ann} ({annId})...", yves, yvesId, ann, annId)
    let fakeUsersRepo = {
        new IUsersRepo with
            member __.GetUsers() = async { return Ok([ yvesDto ; annDto ]) }
            member __.AddUser _ = async { return Ok() }
            member __.UpdateUser _ = async { return Ok() } }
    (* Note: Create UsersAgent *after* initial Users have been added to [fake] Users repo - else will not be able to sign in as these initial Users (because UsersAgent populates its
       "cache" from Users repo when created). *)
    let usersAgent = UsersAgent(fakeUsersRepo, hub, authenticator, logger)
    let connectionId = ConnectionId.Create()
    let defaultPassword = Password "gibet"
    sourcedLogger.Information("Signing in as {ann} via UsersAgent...", ann)
    let! authUser, _ = usersAgent.SignIn(connectionId, ann, Password EXAMPLE_PASSWORD__AE)
    let jwt = authUser.Jwt
    let henrietta, mikeH, mikeO = UserName "henrietta viennetta", UserName "mike hatstead", UserName "mike oxard"
    sourcedLogger.Information("Creating additional User/s via UsersAgent...")
    let! _ = usersAgent.CreateUser(jwt, henrietta, defaultPassword, Pleb)
    let! _ = usersAgent.CreateUser(jwt, mikeH, defaultPassword, Pleb)
    let! _ = usersAgent.CreateUser(jwt, mikeO, defaultPassword, Pleb)
    let! _ = usersAgent.SignOut(connectionId, jwt)
    sourcedLogger.Information("Signing in as other User/s (to change Password and/or ImageUrl)...")
    let! authUser, _ = usersAgent.SignIn(connectionId, henrietta, defaultPassword)
    let jwt = authUser.Jwt
    let! _ = usersAgent.ChangeImageUrl(jwt, Some(ImageUrl "https://i.pinimg.com/originals/d3/e0/ce/d3e0ceb34eeadb2563cf7783bead4e85.jpg") (* Ursula Le Guin *), initialRvn)
    let! _ = usersAgent.SignOut(connectionId, jwt)
    let! authUser, _ = usersAgent.SignIn(connectionId, mikeH, defaultPassword)
    let jwt = authUser.Jwt
    let! _ = usersAgent.ChangePassword(jwt, Password "mikeH", initialRvn)
    let! _ = usersAgent.ChangeImageUrl(jwt, Some(ImageUrl "https://cdn-ed.versobooks.com/images/000000/455/John-Berger-37fedb298baa7ac93877ab8b7169366c.jpg") (* John Berger *), incrementRvn initialRvn)
    let! _ = usersAgent.SignOut(connectionId, jwt)
    let! authUser, _ = usersAgent.SignIn(connectionId, mikeO, defaultPassword)
    let jwt = authUser.Jwt
    let! _ = usersAgent.ChangeImageUrl(jwt, Some(ImageUrl "https://mises-media.s3.amazonaws.com/styles/social_media_1200_x_1200/s3/static-page/img/zamyatin.jpg?itok=J9ilfOm8") (* Yevgeny Zamyatin *), initialRvn)
    let! _ = usersAgent.SignOut(connectionId, jwt)
    sourcedLogger.Information("...initial User/s created")
    return fakeUsersRepo, usersAgent }
