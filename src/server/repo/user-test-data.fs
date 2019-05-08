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
    let nephId, neph, nephPassword, nephImageUrl =
        UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "neph", Password "neph", ImageUrl "djnarration-128x128.png"
    let rosieId, rosie, rosiePassword, rosieImageUrl =
        UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName "rosie", Password "rosie", ImageUrl "https://aornota.github.io/djnarration/public/resources/cmprssd-0100-128x128.png"
    let hughId, hugh = UserId(Guid("00000000-0000-0002-0000-000000000000")), UserName "hugh"
    let willId, will, willPassword, willImageUrl =
        UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "will", Password "will", ImageUrl "https://aornota.github.io/djnarration/public/resources/for-your-ears-only-part-iv-128x128.png"
    let satan, satanPassword = UserName "satan", Password "blzbub"
    let! nephUser = userRepo.CreateUser(Some nephId, neph, initialPassword, BenevolentDictatorForLife, Some nephImageUrl)
    let! _ = userRepo.ChangePassword(nephUser.UserId, nephPassword, nephUser.Rvn)
    let! rosieUser = userRepo.CreateUser(Some rosieId, rosie, initialPassword, Administrator, Some rosieImageUrl)
    let! _ = userRepo.ChangePassword(rosieUser.UserId, rosiePassword, rosieUser.Rvn)
    let! _ = userRepo.CreateUser(Some hughId, hugh, initialPassword, Administrator, None)
    let! willUser = userRepo.CreateUser(Some willId, will, initialPassword, Pleb, Some willImageUrl)
    let! _ = userRepo.ChangePassword(willUser.UserId, willPassword, willUser.Rvn)
    let! _ = userRepo.CreateUser(None, satan, satanPassword, PersonaNonGrata, None)
    logger.Information(sourced "...user test data created" LOG_SOURCE)
    return () }
