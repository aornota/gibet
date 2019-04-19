module Aornota.Gibet.Common.Domain.User

open Aornota.Gibet.Common.Revision

open System

type UserId = | UserId of guid : Guid with
    static member Create() = Guid.NewGuid() |> UserId

type UserName = | UserName of userName : string
type Password = | Password of password : string

type UserType = | BenevolentDictatorForLife | Administrator | Pleb | PersonaNonGrata

type Permissions = | ToDo // TODO-NMB?...

type MustChangePasswordReason =
    | FirstSignIn
    | PasswordReset

type Jwt = | Jwt of jwt : string

// TODO-NMB: Is 'TUserType worthwhile?...
type AuthenticatedUser<'TUserType> = {
    UserId : UserId
    Rvn : Rvn
    UserName : UserName
    // TODO-NMB: e.g. https://github.com/aornota/djnarration/blob/master/src/resources/images/djnarration-24x24.png?...AvatarUrl : string option
    UserType : 'TUserType
    // TODO-NMB: Derive "permissions" dynamically (e.g. canDoXyz userType)?...Permissions : Permissions
    MustChangePasswordReason : MustChangePasswordReason option
    Jwt : Jwt }

type AuthUser = AuthenticatedUser<UserType>

type UserUnauthDto = { UserId : UserId ; UserName : UserName }
type UserAuthDto = { Rvn : Rvn ; UserType : UserType ; LastActivity : DateTimeOffset option }

type UserDto = UserUnauthDto * UserAuthDto option

let validateUserName (userNames:UserName list) (UserName userName) =
    if String.IsNullOrWhiteSpace userName then "User name must not be blank" |> Some
    else if userName.Trim().Length < 3 then "User name must be at least 3 characters" |> Some
    // TODO-NMB: Limit to specific characters?...
    else if userNames |> List.map (fun (UserName userName) -> userName.ToLower().Trim()) |> List.contains (userName.ToLower().Trim()) then "User name already in use" |> Some
    else None
let validatePassword (Password password) =
    if String.IsNullOrWhiteSpace password then "Password must not be blank" |> Some
    else if password.Trim().Length < 6 then "Password must be at least 6 characters" |> Some
    // TODO-NMB: Other restrictions?...
    else None
let validateConfirmPassword (Password newPassword) (Password confirmPassword) =
    if newPassword <> confirmPassword then "Confirmation password must match new password" |> Some
    else validatePassword(Password confirmPassword)
