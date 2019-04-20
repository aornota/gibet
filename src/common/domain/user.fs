module Aornota.Gibet.Common.Domain.User

open Aornota.Gibet.Common.Revision

open System

type UserId = | UserId of guid : Guid with
    static member Create() = Guid.NewGuid() |> UserId

type UserName = | UserName of userName : string
type Password = | Password of password : string

type UserType = | BenevolentDictatorForLife | Administrator | Pleb | PersonaNonGrata

type MustChangePasswordReason = | FirstSignIn | PasswordReset

type User = {
    UserId : UserId
    Rvn : Rvn
    UserName : UserName
    UserType : UserType
    // TODO-NMB?...AvatarUrl : string option // e.g. https://github.com/aornota/djnarration/blob/master/src/resources/images/djnarration-24x24.png
    MustChangePasswordReason : MustChangePasswordReason option
    LastActivity : DateTimeOffset option }

type Jwt = | Jwt of jwt : string

type AuthUser = {
    User : User
    Jwt : Jwt }

let validateUserName (userNames:UserName list) (UserName userName) =
    if String.IsNullOrWhiteSpace userName then "User name must not be blank" |> Error
    else if userName.Trim().Length < 3 then "User name must be at least 3 characters" |> Error
    // TODO-NMB: Limit to specific characters?...
    else if userNames |> List.map (fun (UserName userName) -> userName.ToLower().Trim()) |> List.contains (userName.ToLower().Trim()) then "User name already in use" |> Error
    else () |> Ok
let validatePassword (Password password) =
    if String.IsNullOrWhiteSpace password then "Password must not be blank" |> Error
    else if password.Trim().Length < 6 then "Password must be at least 6 characters" |> Error
    // TODO-NMB: Other restrictions?...
    else () |> Ok
let validateConfirmPassword (Password newPassword) (Password confirmationPassword) =
    if newPassword <> confirmationPassword then "Confirmation password must match new password" |> Error
    else validatePassword(Password confirmationPassword)
