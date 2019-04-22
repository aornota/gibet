module Aornota.Gibet.Common.Api.UserApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

let [<Literal>] NOT_ALLOWED = "Not allowed"

type UserApi = {
    signIn : Connection * UserName * Password -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    autoSignIn : Connection * Jwt -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    signOut : Connection * Jwt -> AsyncResult<unit, string>
    getUsers : Connection * Jwt -> AsyncResult<User list, string>
    createUser : Connection * Jwt * UserName * Password * UserType -> AsyncResult<unit, string>
    changePassword : Connection * Jwt * Password * Rvn -> AsyncResult<unit, string>
    resetPassword : Connection * Jwt * UserId * Password * Rvn -> AsyncResult<unit, string>
    changeUserType : Connection * Jwt * UserId * UserType * Rvn -> AsyncResult<unit, string> } // TODO-NMB: More?...
