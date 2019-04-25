module Aornota.Gibet.Server.Repo.UserTestData

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Repo.IUserRepo

open System

open FsToolkit.ErrorHandling

open Serilog

let create (logger:ILogger) (userRepo:IUserRepo) = asyncResult {
    logger.Information("Creating User test data...")
    let initialPassword = Password "password"
    let nephId, neph, nephPassword = UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "neph", Password "neph"
    let rosieId, rosie = UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName "rosie"
    let hughId, hugh, hughPassword = UserId(Guid("00000000-0000-0002-0000-000000000000")), UserName "hugh", Password "hugh"
    let willId, will = UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "will"
    let satan = UserName "satan"
    let! nephUser = userRepo.CreateUser(Some nephId, neph, initialPassword, BenevolentDictatorForLife)
    let! _ = userRepo.CreateUser(Some rosieId, rosie, initialPassword, Administrator)
    let! hughUser = userRepo.CreateUser(Some hughId, hugh, initialPassword, Pleb)
    let! _ = userRepo.CreateUser(Some willId, will, initialPassword, Pleb)
    let! _ = userRepo.CreateUser(None, satan, initialPassword, PersonaNonGrata)
    let! _ = userRepo.SignIn(neph, initialPassword)
    // "Invalid credentials" error...let! _ = userRepo.SignIn(rosie, Password "drowssap")
    // "Invalid credentials" error...let! _ = userRepo.SignIn(UserName "hguh", initialPassword)
    // "Invalid credentials" error...let! _ = userRepo.SignIn(satan, initialPassword)
    let! _ = userRepo.ChangePassword(nephUser.UserId, nephPassword, nephUser.Rvn)
    let! _ = userRepo.SignIn(neph, nephPassword)
    let! hughUser = userRepo.ChangeUserType(hughUser.UserId, Administrator, hughUser.Rvn)
    let! _ = userRepo.ChangePassword(hughUser.UserId, hughPassword, hughUser.Rvn)
    let! _ = userRepo.SignIn(hugh, hughPassword)
    logger.Information("...User test data created")
    return () }
