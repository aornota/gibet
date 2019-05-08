module Aornota.Gibet.Common.Domain.User

open Aornota.Gibet.Common.Revision

open System

type UserId = | UserId of Guid with static member Create() = UserId(Guid.NewGuid())

type UserName = | UserName of string
type Password = | Password of string
type ImageUrl = | ImageUrl of string

type UserType = | BenevolentDictatorForLife | Administrator | Pleb | PersonaNonGrata

type MustChangePasswordReason = | FirstSignIn | PasswordReset
type ForcedSignOutReason = | UserTypeChanged | PasswordReset

type User = {
    UserId : UserId
    Rvn : Rvn
    UserName : UserName
    UserType : UserType
    ImageUrl : ImageUrl option }

type Jwt = | Jwt of string

type AuthUser = {
    User : User
    Jwt : Jwt }

let validateUserName forSignIn (UserName userName) (userNames:UserName list) = // TODO-NMB: Limit to specific characters?...
    if String.IsNullOrWhiteSpace userName then Some "User name must not be blank"
    else if not forSignIn && userName.Trim().Length < 3 then Some "User name must be at least 3 characters"
    else if not forSignIn && userNames |> List.map (fun (UserName userName) -> userName.ToLower().Trim()) |> List.contains (userName.ToLower().Trim()) then Some "User name is not available"
    else None
let validatePassword forSignIn (Password password) = // TODO-NMB: Other restrictions?...
    if String.IsNullOrWhiteSpace password then Some "Password must not be blank"
    else if not forSignIn && password.Trim().Length < 6 then Some "Password must be at least 6 characters"
    else if not forSignIn && password.Trim().ToLower() = "password" then Some (sprintf "'%s' is not a valid password!" (password.Trim()))
    else None
let validateConfirmationPassword newPassword confirmationPassword =
    if newPassword <> confirmationPassword then Some "Confirmation password must match new password"
    else validatePassword false newPassword

let mustChangePasswordBecause mustChangePasswordReason =
    match mustChangePasswordReason with
    | FirstSignIn -> "this is the first time you have signed in"
    | MustChangePasswordReason.PasswordReset -> "it has been reset by a system administrator"
let forcedSignOutBecause forcedSignOutReason =
    match forcedSignOutReason with
    | UserTypeChanged -> "your permissions have been changed by a system administrator"
    | PasswordReset -> "your password has been reset by a system administrator"

let canAdministerUsers userType =
    match userType with | BenevolentDictatorForLife | Administrator -> true | _ -> false
let canCreateUsers userType = canAdministerUsers userType
let canCreateUser forUserType userType =
    if not (canCreateUsers userType) then false
    else
        match userType, forUserType with
        | BenevolentDictatorForLife, _ -> true
        | Administrator, Administrator -> true
        | Administrator, Pleb -> true
        | Administrator, PersonaNonGrata -> true
        | _ -> false
let canChangePassword forUserId (userId:UserId, userType) =
    if forUserId <> userId then false
    else
        match userType with
        | BenevolentDictatorForLife | Administrator | Pleb -> true
        | PersonaNonGrata -> false
let canResetPassword (forUserId, forUserType) (userId:UserId, userType) =
    if forUserId = userId then false
    else canCreateUser forUserType userType
let canChangeUserType (forUserId, forUserType) (userId:UserId, userType) =
    if forUserId = userId then false
    else canCreateUser forUserType userType
let canChangeUserTypeTo (forUserId, forUserType) newUserType (userId:UserId, userType) =
    if not (canChangeUserType (forUserId, forUserType) (userId, userType)) then false
    else if forUserType = newUserType then false
    else canCreateUser newUserType userType
let canChangeImageUrl forUserId (userId:UserId, userType) = canChangePassword forUserId (userId, userType)
