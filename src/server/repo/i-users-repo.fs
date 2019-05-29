module Aornota.Gibet.Server.Repo.IUsersRepo

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Domain.User

type Salt = | Salt of string
type Hash = | Hash of string

type UserDto = {
    User : User
    Salt : Salt
    Hash : Hash
    MustChangePasswordReason : MustChangePasswordReason option }

type IUsersRepo =
    abstract GetUsers: unit -> AsyncResult<UserDto list, string>
    abstract AddUser: UserDto -> AsyncResult<unit, string>
    abstract UpdateUser: UserDto -> AsyncResult<unit, string>
