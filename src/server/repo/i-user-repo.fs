module Aornota.Gibet.Server.Repo.IUserRepo

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

// TODO-NMB: Does FsToolkit.ErrorHandling provide alternative to Async<Result<_>>?...

type IUserRepo =
    abstract SignIn: UserName * Password -> Async<Result<UserId, string>>
    abstract GetUsers: unit -> Async<Result<User list, string>>
    abstract CreateUser: UserId option * UserName * Password * UserType -> Async<Result<User, string>>
    abstract ChangePassword: UserId * Password * Rvn -> Async<Result<User, string>>
    abstract ResetPassword: UserId * Password * Rvn -> Async<Result<User, string>>
    abstract ChangeUserType: UserId * UserType * Rvn -> Async<Result<User, string>>
    // TODO-NMB: More...
