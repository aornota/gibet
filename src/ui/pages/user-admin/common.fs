module Aornota.Gibet.Ui.Pages.UserAdmin.Common

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Shared

open System

type CreateUsersModalInput =
    | UserNameChanged of string
    | PasswordChanged of string
    | ConfirmPasswordChanged of string
    | UserTypeChanged of UserType
    | CreateUser
    | CloseCreateUsersModal
type CreateUserApiInput =
    | CreateUserResult of Result<UserName, string>
    | CreateUserExn of exn

type ResetPasswordModalInput =
    | NewPasswordChanged of string
    | ConfirmPasswordChanged of string
    | ResetPassword
    | CloseResetPasswordModal
type ResetPasswordApiInput =
    | ResetPasswordResult of Result<UserName, string>
    | ResetPasswordExn of exn

type ChangeUserTypeModalInput =
    | NewUserTypeChanged of UserType
    | ChangeUserType
    | CloseChangeUserTypeModal
type ChangeUserTypeApiInput =
    | ChangeUserTypeResult of Result<UserName, string>
    | ChangeUserTypeExn of exn

type Input =
    | AddMessage of Message // note: handled by Program.State.transition
    | ShowCreateUsersModal
    | CreateUsersModalInput of CreateUsersModalInput
    | CreateUserApiInput of CreateUserApiInput
    | ShowResetPasswordModal of UserId * Rvn
    | ResetPasswordModalInput of ResetPasswordModalInput
    | ResetPasswordApiInput of ResetPasswordApiInput
    | ShowChangeUserTypeModal of UserId * Rvn
    | ChangeUserTypeModalInput of ChangeUserTypeModalInput
    | ChangeUserTypeApiInput of ChangeUserTypeApiInput

type CreateUsersModalState = {
    UserNameKey : Guid
    UserName : string
    UserNameChanged : bool
    PasswordKey : Guid
    Password : string
    PasswordChanged : bool
    ConfirmPasswordKey : Guid
    ConfirmPassword : string
    ConfirmPasswordChanged : bool
    UserType : UserType
    LastUserNameCreated : UserName option
    CreateUserApiStatus : ApiStatus<string> option }

type ResetPasswordModalState = {
    ForUser : UserId * Rvn
    NewPasswordKey : Guid
    NewPassword : string
    NewPasswordChanged : bool
    ConfirmPasswordKey : Guid
    ConfirmPassword : string
    ConfirmPasswordChanged : bool
    ResetPasswordApiStatus : ApiStatus<string> option }

type ChangeUserTypeModalState = {
    ForUser : UserId * Rvn
    NewUserType : UserType option
    ChangeUserTypeApiStatus : ApiStatus<string> option }

type State = {
    CreateUsersModalState : CreateUsersModalState option
    ResetPasswordModalState : ResetPasswordModalState option
    ChangeUserTypeModalState : ChangeUserTypeModalState option  }
