module Aornota.Gibet.Common.Api.IUserApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

let [<Literal>] NOT_ALLOWED = "Not allowed"

// TODO-NMB: Will need some sort of ConnectionId and/or AffinityId (a.k.a. SessionId)?...

type IUserApi = {
    signIn : UserName * Password -> AsyncResult<AuthUser, string>
    autoSignIn : Jwt -> AsyncResult<AuthUser, string>
    signOut : Jwt -> AsyncResult<unit, string>
    getUsers : Jwt -> AsyncResult<User list, string>
    createUser : Jwt * UserName * Password * UserType -> AsyncResult<unit, string>
    changePassword : Jwt * Password * Rvn -> AsyncResult<unit, string>
    resetPassword : Jwt * UserId * Password * Rvn -> AsyncResult<unit, string>
    changeUserType : Jwt * UserId * UserType * Rvn -> AsyncResult<unit, string> } // TODO-NMB: More?...
