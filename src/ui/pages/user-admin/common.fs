module Aornota.Gibet.Ui.Pages.UserAdmin.Common

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Shared

//type CreateUsersModalInput =

//type CreateUsersInput =

//type ResetPasswordModalInput =

//type ResetPasswordInput =

//type ChangeUserTypeModalInput =

//type ChangeUserTypeInput =

type Input =
    | AddMessage of Message
    | ShowCreateUsersModal
    //| CreateUsersModalInput
    //| CreateUsersInput
    | ShowResetPasswordModal of UserId
    //| ResetPasswordModalInput
    //| ResetPasswordInput
    | ShowChangeUserTypeModal of UserId
    //| ChangeUserTypeModalInput
    //| ChangeUserTypeInput

type CreateUsersModalState = { //
    ModalStatus : ModalStatus<string> option }

type ResetPasswordModalState = { //
    ModalStatus : ModalStatus<string> option }

type ChangeUserTypeModalState = { //
    ModalStatus : ModalStatus<string> option }

type State = {
    CreateUsersModalState : CreateUsersModalState option
    ResetPasswordModalState : ResetPasswordModalState option
    ChangeUserTypeModalState : ChangeUserTypeModalState option  }
