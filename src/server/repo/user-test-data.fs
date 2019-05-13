module Aornota.Gibet.Server.Repo.UserTestData

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo.IUserRepo

open System

open FsToolkit.ErrorHandling

open Serilog

let private logger = Log.Logger |> sourcedLogger "Repo.UserTestData"

let create (userRepo:IUserRepo) = asyncResult {
    logger.Information("Creating user test data...")
    // Note: Some user names and passwords would be invalid for IUserApi - but are okay for IUserRepo
    let defaultPassword = Password "password"
    let yvesId, yves, yvesPassword = UserId(Guid("00000000-0001-0000-0000-000000000000")), UserName "yves strop", Password "yves"
    let yvesImageUrl = ImageUrl "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcRZrSarOxyHO_FbyMvr8uJTeoSqgGiAIr3m9EhXqy_i28kBRV8S" // Rainer Maria Rilke
    let annId, ann, annPassword = UserId(Guid("00000000-0000-0001-0000-000000000000")), UserName EXAMPLE_ADMIN_USER_NAME, Password EXAMPLE_ADMIN_PASSWORD
    let annImageUrl = ImageUrl "https://upload.wikimedia.org/wikipedia/en/thumb/3/3e/Nan_Shepherd.jpg/220px-Nan_Shepherd.jpg" // Nan Shepherd
    let henriettaId, henrietta = UserId(Guid("00000000-0000-0000-0001-000000000000")), UserName "henrietta viennetta"
    let henriettaImageUrl = ImageUrl "https://i.pinimg.com/originals/d3/e0/ce/d3e0ceb34eeadb2563cf7783bead4e85.jpg" // Ursula Le Guin
    let mike1Id, mike1, mike1Password = UserId(Guid("00000000-0000-0000-0002-000000000000")), UserName "mike hatstead", Password "mike"
    let mike1ImageUrl = ImageUrl "https://cdn-ed.versobooks.com/images/000000/455/John-Berger-37fedb298baa7ac93877ab8b7169366c.jpg" // John Berger
    let mike2Id, mike2 = UserId(Guid("00000000-0000-0000-0000-000000000001")), UserName "mike oxard"
    let mike2ImageUrl = ImageUrl "https://mises-media.s3.amazonaws.com/styles/social_media_1200_x_1200/s3/static-page/img/zamyatin.jpg?itok=J9ilfOm8" // Yevgeny Zamyatin
    let! yvesUser = userRepo.CreateUser(Some yvesId, yves, defaultPassword, BenevolentDictatorForLife, Some yvesImageUrl)
    let! _ = userRepo.ChangePassword(yvesUser.UserId, yvesPassword, yvesUser.Rvn)
    let! annUser = userRepo.CreateUser(Some annId, ann, defaultPassword, Administrator, Some annImageUrl)
    let! _ = userRepo.ChangePassword(annUser.UserId, annPassword, annUser.Rvn)
    let! _ = userRepo.CreateUser(Some henriettaId, henrietta, defaultPassword, Pleb, Some henriettaImageUrl)
    let! mike1User = userRepo.CreateUser(Some mike1Id, mike1, defaultPassword, Pleb, Some mike1ImageUrl)
    let! _ = userRepo.ChangePassword(mike1User.UserId, mike1Password, mike1User.Rvn)
    let! _ = userRepo.CreateUser(Some mike2Id, mike2, defaultPassword, PersonaNonGrata, Some mike2ImageUrl)
    logger.Information("...user test data created")
    return () }
