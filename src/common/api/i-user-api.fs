module Aornota.Gibet.Common.Api.IUserApi

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

// TODO-NMB: Will need some sort of ConnectionId / AffinityId? Store in AuthUser.Jwt?...
// TODO-NMB: Does FsToolkit.ErrorHandling provide alternative to Async<Result<_>>?...

type IUserApi = {
    signIn : UserName * Password -> Async<Result<AuthUser, string>>
    signOut : Jwt -> Async<Result<unit, string>>
    getUsers : Jwt -> Async<Result<User list, string>>
    createUser : Jwt * UserName * Password * UserType -> Async<Result<unit, string>>
    changePassword : Jwt * Password * Rvn -> Async<Result<unit, string>>
    resetPassword : Jwt * UserId * Password * Rvn -> Async<Result<unit, string>>
    changeUserType : Jwt * UserId * UserType * Rvn -> Async<Result<unit, string>>
    // TODO-NMB: More...
}
