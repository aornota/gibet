module Aornota.Gibet.Server.Repo.UserTestData

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Repo.IUserRepo

open System

open FsToolkit.ErrorHandling

open Serilog

let create (logger:ILogger) (userRepo:IUserRepo) = asyncResult {
    logger.Information("Creating user/s test data...")
    let initialPassword = "password" |> Password
    let nephId, neph, nephPassword = Guid "00000000-0001-0000-0000-000000000000" |> UserId, "neph" |> UserName, "neph" |> Password
    let rosieId, rosie = Guid "00000000-0000-0001-0000-000000000000" |> UserId, "rosie" |> UserName
    let hughId, hugh, hughPassword = Guid "00000000-0000-0002-0000-000000000000" |> UserId, "hugh" |> UserName, "hugh" |> Password
    let willId, will = Guid "00000000-0000-0000-0001-000000000000" |> UserId, "will" |> UserName
    let satan = "satan" |> UserName
    let! nephUser = userRepo.CreateUser(nephId |> Some, neph, initialPassword, BenevolentDictatorForLife)
    let! _ = userRepo.CreateUser(rosieId |> Some, rosie, initialPassword, Administrator)
    let! hughUser = userRepo.CreateUser(hughId |> Some, hugh, initialPassword, Pleb)
    let! _ = userRepo.CreateUser(willId |> Some, will, initialPassword, Pleb)
    let! _ = userRepo.CreateUser(None, satan, initialPassword, PersonaNonGrata)
    let! _ = userRepo.SignIn(neph, initialPassword)
    // "Invalid credentials" error...let! _ = userRepo.SignIn(rosie, "drowssap" |> Password)
    // "Invalid credentials" error...let! _ = userRepo.SignIn("hguh" |> UserName, initialPassword)
    // "Invalid credentials" error...let! _ = userRepo.SignIn(satan, initialPassword)
    let! _ = userRepo.ChangePassword(nephUser.UserId, nephPassword, nephUser.Rvn)
    let! _ = userRepo.SignIn(neph, nephPassword)
    let! hughUser = userRepo.ChangeUserType(hughUser.UserId, Administrator, hughUser.Rvn)
    let! _ = userRepo.ChangePassword(hughUser.UserId, hughPassword, hughUser.Rvn)
    let! _ = userRepo.SignIn(hugh, hughPassword)
    logger.Information("...user/s test data created")
    return () }
