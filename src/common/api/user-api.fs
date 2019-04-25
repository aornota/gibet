module Aornota.Gibet.Common.Api.UserApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

let [<Literal>] NOT_ALLOWED = "Not allowed"

type UserApi = { // TODO-NMB: More?...
    signIn : ConnectionId * UserName * Password -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    autoSignIn : ConnectionId * Jwt -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    signOut : ConnectionId * Jwt -> AsyncResult<unit, string>
    changePassword : ConnectionId * Jwt * Password * Rvn -> AsyncResult<unit, string>
    getUsers : ConnectionId * Jwt -> AsyncResult<(User * bool) list * Rvn, string>
    createUser : ConnectionId * Jwt * UserName * Password * UserType -> AsyncResult<unit, string>
    resetPassword : ConnectionId * Jwt * UserId * Password * Rvn -> AsyncResult<unit, string>
    changeUserType : ConnectionId * Jwt * UserId * UserType * Rvn -> AsyncResult<unit, string> }
