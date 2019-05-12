module Aornota.Gibet.Common.Api.UserApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

let [<Literal>] NOT_ALLOWED = "\"I'm sorry Dave, I'm afraid I can't do that\""

type UserApi = {
    signIn : ConnectionId * UserName * Password -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    autoSignIn : ConnectionId * Jwt -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    signOut : ConnectionId * Jwt -> AsyncResult<unit, string>
    changePassword : Jwt * Password * Rvn -> AsyncResult<UserName, string>
    changeImageUrl : Jwt * ImageUrl option * Rvn -> AsyncResult<UserName * ImageChangeType option, string>
    getUsers : ConnectionId * Jwt -> AsyncResult<(User * bool) list * Rvn, string>
    createUser : Jwt * UserName * Password * UserType * ImageUrl option -> AsyncResult<UserName, string>
    resetPassword : Jwt * UserId * Password * Rvn -> AsyncResult<UserName, string>
    changeUserType : Jwt * UserId * UserType * Rvn -> AsyncResult<UserName, string> }
