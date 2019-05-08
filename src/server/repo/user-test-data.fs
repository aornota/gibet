module Aornota.Gibet.Server.Repo.UserTestData

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo.IUserRepo

open System

open FsToolkit.ErrorHandling

open Serilog

let [<Literal>] private LOG_SOURCE = "UserTestData"

let create (logger:ILogger) (userRepo:IUserRepo) = asyncResult {
    logger.Information(sourced "Creating user test data..." LOG_SOURCE)
    let initialPassword = Password "password" // note: invalid for IUserApi - but okay for IUserRepo
    let nephId, neph, nephPassword = UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "neph", Password "neph"
    let rosieId, rosie = UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName "rosie"
    let hughId, hugh = UserId(Guid("00000000-0000-0002-0000-000000000000")), UserName "hugh"
    let willId, will = UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "will"
    let satan, satanPassword = UserName "satan", Password "blzbub"
    let! nephUser = userRepo.CreateUser(Some nephId, neph, initialPassword, BenevolentDictatorForLife)
    let! _ = userRepo.ChangePassword(nephUser.UserId, nephPassword, nephUser.Rvn)
    let! _ = userRepo.CreateUser(Some rosieId, rosie, initialPassword, Administrator)
    let! _ = userRepo.CreateUser(Some hughId, hugh, initialPassword, Administrator)
    let! _ = userRepo.CreateUser(Some willId, will, initialPassword, Pleb)
    let! _ = userRepo.CreateUser(None, satan, satanPassword, PersonaNonGrata)
    logger.Information(sourced "...user test data created" LOG_SOURCE)
    return () }
