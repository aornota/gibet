module Aornota.Gibet.Common.Domain.User

open Aornota.Gibet.Common.Revision

open System

type UserId = | UserId of Guid with static member Create() = UserId(Guid.NewGuid())

type UserName = | UserName of string
type Password = | Password of string

type UserType = | BenevolentDictatorForLife | Administrator | Pleb | PersonaNonGrata

type MustChangePasswordReason = | FirstSignIn | PasswordReset

type ForcedSignOutReason = | UserTypeChanged | PasswordReset

type User = { // TODO-NMB?...AvatarUrl : string option // e.g. https://github.com/aornota/djnarration/blob/master/src/resources/images/djnarration-24x24.png
    UserId : UserId
    Rvn : Rvn
    UserName : UserName
    UserType : UserType }

type Jwt = | Jwt of string

type AuthUser = {
    User : User
    Jwt : Jwt }

let validateUserName (UserName userName) (userNames:UserName list) =
    if String.IsNullOrWhiteSpace userName then Some "User name must not be blank"
    else if userName.Trim().Length < 3 then Some "User name must be at least 3 characters"
    // TODO-NMB: Limit to specific characters?...
    else if userNames |> List.map (fun (UserName userName) -> userName.ToLower().Trim()) |> List.contains (userName.ToLower().Trim()) then Some "User name is not available"
    else None
let validatePassword (Password password) =
    if String.IsNullOrWhiteSpace password then Some "Password must not be blank"
    else if password.Trim().Length < 6 then Some "Password must be at least 6 characters"
    // TODO-NMB: Other restrictions?...
    else None
let validateConfirmationPassword newPassword confirmationPassword =
    if newPassword <> confirmationPassword then Some "Confirmation password must match new password"
    else validatePassword confirmationPassword

let canCreateUsers userType =
    match userType with | BenevolentDictatorForLife | Administrator -> true | _ -> false
let canCreateUser forUserType userType =
    if userType |> canCreateUsers |> not then false
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
let canResetPassword forUserId forUserType (userId:UserId, userType) =
    if forUserId = userId then false
    else userType |> canCreateUser forUserType
let canChangeUserType forUserId forUserType (userId:UserId, userType) =
    if forUserId = userId then false
    else userType |> canCreateUser forUserType
let canChangeUserTypeTo forUserId forUserType newUserType (userId:UserId, userType) =
    if (userId, userType) |> canChangeUserType forUserId forUserType |> not then false
    else if forUserType = newUserType then false
    else userType |> canCreateUser newUserType
