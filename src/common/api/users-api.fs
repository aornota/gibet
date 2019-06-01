module Aornota.Gibet.Common.Api.UsersApi

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Jwt
open Aornota.Gibet.Common.Rvn

type UsersApi = {
    signIn : ConnectionId * UserName * Password -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    autoSignIn : ConnectionId * Jwt -> AsyncResult<AuthUser * MustChangePasswordReason option, string>
    signOut : ConnectionId * Jwt -> AsyncResult<unit, string>
    changePassword : Jwt * Password * Rvn -> AsyncResult<UserName, string>
    changeImageUrl : Jwt * ImageUrl option * Rvn -> AsyncResult<UserName * ImageChangeType option, string>
    getUsers : ConnectionId * Jwt -> AsyncResult<(User * bool) list * Rvn, string>
    createUser : Jwt * UserName * Password * UserType -> AsyncResult<UserId * UserName, string>
    resetPassword : Jwt * UserId * Password * Rvn -> AsyncResult<UserName, string>
    changeUserType : Jwt * UserId * UserType * Rvn -> AsyncResult<UserName, string> }

let canSignIn userType = match userType with | BenevolentDictatorForLife | Administrator | Pleb -> true | PersonaNonGrata -> false
let canChangePassword forUserId (userId:UserId, userType) = if forUserId <> userId then false else canSignIn userType
let canChangeImageUrl forUserId (userId:UserId, userType) = canChangePassword forUserId (userId, userType)
let canGetUsers userType = canSignIn userType
let canAdministerUsers userType = match userType with | BenevolentDictatorForLife | Administrator -> true | _ -> false
let canCreateUser forUserType userType =
    if not (canAdministerUsers userType) then false
    else match userType, forUserType with | BenevolentDictatorForLife, _ -> true | Administrator, Administrator | Administrator, Pleb -> true | _ -> false
let canResetPassword (forUserId, forUserType) (userId:UserId, userType) = if forUserId = userId then false else canCreateUser forUserType userType
let canChangeUserType (forUserId, forUserType) (userId:UserId, userType) = if forUserId = userId then false else canCreateUser forUserType userType
let canChangeUserTypeTo (forUserId, forUserType) newUserType (userId:UserId, userType) =
    if not (canChangeUserType (forUserId, forUserType) (userId, userType)) then false
    else if forUserType = newUserType then false
    else canCreateUser newUserType userType
