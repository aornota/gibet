module Aornota.Gibet.Common.Domain.User

open Aornota.Gibet.Common.Revision

open System

type UserId = | UserId of Guid with static member Create() = UserId(Guid.NewGuid())

type UserName = | UserName of string
type Password = | Password of string
type ImageUrl = | ImageUrl of string

type UserType =
    | BenevolentDictatorForLife
    | Administrator
    | Pleb
    | PersonaNonGrata

type MustChangePasswordReason =
    | FirstSignIn
    | PasswordReset of UserName
type ForcedSignOutReason =
    | UserTypeChanged of UserName
    | PasswordReset of UserName

type User = {
    UserId : UserId
    Rvn : Rvn
    UserName : UserName
    UserType : UserType
    ImageUrl : ImageUrl option }

type ImageChangeType =
    | ImageChosen
    | ImageChanged
    | ImageRemoved

type Jwt = | Jwt of string

type AuthUser = {
    User : User
    Jwt : Jwt }

let [<Literal>] private SPACE = " "

let [<Literal>] EXAMPLE_ADMIN_USER_NAME = "ann ewity"
let [<Literal>] EXAMPLE_ADMIN_PASSWORD = "ann"

let validateUserName forSignIn (UserName userName) (userNames:UserName list) =
    if userName.StartsWith(SPACE) then Some "User name must not start with a space"
    else if userName.EndsWith(SPACE) then Some "User name must not end with a space"
    else if String.IsNullOrWhiteSpace userName then Some "User name must not be blank"
    else if not forSignIn && userName.Length < 3 then Some "User name must be at least 3 characters"
    else if not forSignIn && userNames |> List.map (fun (UserName userName) -> userName.ToLower()) |> List.contains (userName.ToLower()) then Some "User name is not available"
    else None
let validatePassword forSignIn (Password password) =
    if password.StartsWith(SPACE) then Some "Password must not start with a space"
    else if password.EndsWith(SPACE) then Some "Password must not end with a space"
    else if String.IsNullOrWhiteSpace password then Some "Password must not be blank"
    else if not forSignIn && password.Length < 6 then Some "Password must be at least 6 characters"
    else if not forSignIn && password.ToLower() = "password" then Some (sprintf "'%s' is not a valid password!" password)
    else None

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

let mustChangePasswordBecause mustChangePasswordReason =
    match mustChangePasswordReason with
    | FirstSignIn -> "this is the first time you have signed in", None
    | MustChangePasswordReason.PasswordReset userName -> "it has been reset", Some userName
let forcedSignOutBecause forcedSignOutReason =
    match forcedSignOutReason with
    | UserTypeChanged userName -> "your permissions have been changed", userName
    | PasswordReset userName -> "your password has been reset", userName

let changeType imageChangeType =
    match imageChangeType with
    | Some ImageChosen -> "chosen"
    | Some ImageChanged -> "changed"
    | Some ImageRemoved -> "removed"
    | None -> "unchanged (should never happen)"
