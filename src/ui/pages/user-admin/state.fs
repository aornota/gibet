module Aornota.Gibet.Ui.Pages.UserAdmin.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.ShouldNeverHappen
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Pages.UserAdmin.Common
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared
open Aornota.Gibet.Ui.UserApi

open System

open Elmish

let private addMessageCmd messageType text = AddMessage(messageDismissable messageType text) |> Cmd.ofMsg

let private addDebugErrorCmd error = addMessageCmd Debug (sprintf "ERROR -> %s" error)
// #region shouldNeverHappenCmd
let private shouldNeverHappenCmd error =
#if DEBUG
    addDebugErrorCmd (shouldNeverHappen error)
#else
    addMessageCmd Danger SOMETHING_HAS_GONE_WRONG
#endif
// #endregion

let private _createUsersModalState userType lastUserNameCreated = {
    UserNameKey = Guid.NewGuid()
    UserName = String.Empty
    UserNameChanged = false
    PasswordKey = Guid.NewGuid()
    Password = String.Empty
    PasswordChanged = false
    ConfirmPasswordKey = Guid.NewGuid()
    ConfirmPassword = String.Empty
    ConfirmPasswordChanged = false
    UserType = userType
    LastUserNameCreated = lastUserNameCreated
    ModalStatus = None }
let private resetPasswordModalState forUser = {
    ForUser = forUser
    NewPasswordKey = Guid.NewGuid()
    NewPassword = String.Empty
    NewPasswordChanged = false
    ConfirmPasswordKey = Guid.NewGuid()
    ConfirmPassword = String.Empty
    ConfirmPasswordChanged = false
    ModalStatus = None }
let private changeUserTypeModalState forUser = {
    ForUser = forUser
    NewUserType = None
    ModalStatus = None }

let private handleCreateUsersModalInput createUsersModalInput (authUser:AuthUser) (users:UserData list) state =
    match state.CreateUsersModalState with
    | Some createUsersModalState ->
        match createUsersModalInput, createUsersModalState.ModalStatus with
        | _, Some ModalPending -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState.ModalStatus is Pending (%A)" createUsersModalInput state)
        | UserNameChanged userName, _ ->
            let createUsersModalState = { createUsersModalState with UserName = userName ; UserNameChanged = true }
            { state with CreateUsersModalState = Some createUsersModalState }, Cmd.none
        | PasswordChanged password, _ ->
            let createUsersModalState = { createUsersModalState with Password = password ; PasswordChanged = true }
            { state with CreateUsersModalState = Some createUsersModalState }, Cmd.none
        | CreateUsersModalInput.ConfirmPasswordChanged confirmPassword, _ ->
            let createUsersModalState = { createUsersModalState with ConfirmPassword = confirmPassword ; ConfirmPasswordChanged = true }
            { state with CreateUsersModalState = Some createUsersModalState }, Cmd.none
        | UserTypeChanged userType, _ ->
            let createUsersModalState = { createUsersModalState with UserType = userType }
            { state with CreateUsersModalState = Some createUsersModalState }, Cmd.none
        | CreateUser, _ ->
            let userName, password = UserName(createUsersModalState.UserName.Trim()), Password(createUsersModalState.Password.Trim())
            let cmd = Cmd.OfAsync.either userApi.createUser (authUser.Jwt, userName, password, createUsersModalState.UserType) CreateUserResult CreateUserExn |> Cmd.map CreateUserInput
            let createUsersModalState = { createUsersModalState with LastUserNameCreated = None ; ModalStatus = Some ModalPending }
            { state with CreateUsersModalState = Some createUsersModalState }, cmd
        | CancelCreateUsers, _ -> { state with CreateUsersModalState = None }, Cmd.none
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState is None (%A)" createUsersModalInput state)
let private handleCreateUserInput createUserInput (authUser:AuthUser) (users:UserData list) state =
    match state.CreateUsersModalState with
    | Some createUsersModalState ->
        match createUsersModalState.ModalStatus with
        | Some ModalPending ->
            match createUserInput with
            | CreateUserResult(Ok(UserName userName)) ->
                let toastCmd = sprintf "User <strong>%s</strong> added" userName |> successToastCmd
                { state with CreateUsersModalState = Some(_createUsersModalState createUsersModalState.UserType (Some(UserName userName))) }, toastCmd
            | CreateUserResult(Error error) ->
                let createUsersModalState = { createUsersModalState with ModalStatus = Some(ModalFailed error) }
                { state with CreateUsersModalState = Some createUsersModalState }, Cmd.none // no need for toast (since error will be displayed on CreateUsersModal)
            | CreateUserExn exn -> state, CreateUserInput(CreateUserResult(Error exn.Message)) |> Cmd.ofMsg
        | _ -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState.ModalStatus is not Pending (%A)" createUserInput state)
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState is None (%A)" createUserInput state)

let private handleResetPasswordModalInput resetPasswordModalInput (authUser:AuthUser) (users:UserData list) state =
    match state.ResetPasswordModalState with
    | Some resetPasswordModalState ->
        match resetPasswordModalInput, resetPasswordModalState.ModalStatus with
        | _, Some ModalPending -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState.ModalStatus is Pending (%A)" resetPasswordModalInput state)
        | NewPasswordChanged newPassword, _ ->
            let resetPasswordModalState = { resetPasswordModalState with NewPassword = newPassword ; NewPasswordChanged = true }
            { state with ResetPasswordModalState = Some resetPasswordModalState }, Cmd.none
        | ConfirmPasswordChanged confirmPassword, _ ->
            let resetPasswordModalState = { resetPasswordModalState with ConfirmPassword = confirmPassword ; ConfirmPasswordChanged = true }
            { state with ResetPasswordModalState = Some resetPasswordModalState }, Cmd.none
        | ResetPassword, _ ->
            let userId, _ =  resetPasswordModalState.ForUser
            match users |> findUser userId with
            | Some(user, _, _) ->
                let password = Password(resetPasswordModalState.NewPassword.Trim())
                let cmd = Cmd.OfAsync.either userApi.resetPassword (authUser.Jwt, userId, password, user.Rvn) ResetPasswordResult ResetPasswordExn |> Cmd.map ResetPasswordInput
                let resetPasswordModalState = { resetPasswordModalState with ModalStatus = Some ModalPending }
                { state with ResetPasswordModalState = Some resetPasswordModalState }, cmd
            | None ->
                let resetPasswordModalState = { resetPasswordModalState with ModalStatus = Some (ModalFailed UNEXPECTED_ERROR) }
                let state = { state with ResetPasswordModalState = Some resetPasswordModalState }
                state, shouldNeverHappenCmd (sprintf "Unexpected ResetPassword when %A not found in users (%A)" userId state)
        | CancelResetPassword, _ -> { state with ResetPasswordModalState = None }, Cmd.none
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState is None (%A)" resetPasswordModalInput state)
let private handleResetPasswordInput resetPasswordInput (authUser:AuthUser) (users:UserData list) state =
    match state.ResetPasswordModalState with
    | Some resetPasswordModalState ->
        match resetPasswordModalState.ModalStatus with
        | Some ModalPending ->
            match resetPasswordInput with
            | ResetPasswordResult(Ok(UserName userName)) ->
                { state with ResetPasswordModalState = None }, sprintf "Password reset for <strong>%s</strong>" userName |> successToastCmd
            | ResetPasswordResult(Error error) ->
                let resetPasswordModalState = { resetPasswordModalState with ModalStatus = Some(ModalFailed error) }
                { state with ResetPasswordModalState = Some resetPasswordModalState }, Cmd.none // no need for toast (since error will be displayed on ResetPasswordModal)
            | ResetPasswordExn exn -> state, ResetPasswordInput(ResetPasswordResult(Error exn.Message)) |> Cmd.ofMsg
        | _ -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState.ModalStatus is not Pending (%A)" resetPasswordInput state)
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState is None (%A)" resetPasswordInput state)

let private handleChangeUserTypeModalInput changeUserTypeModalInput (authUser:AuthUser) (users:UserData list) state =
    match state.ChangeUserTypeModalState with
    | Some changeUserTypeModalState ->
        match changeUserTypeModalInput, changeUserTypeModalState.ModalStatus with
        | _, Some ModalPending -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState.ModalStatus is Pending (%A)" changeUserTypeModalInput state)
        | NewUserTypeChanged newUserType, _ ->
            let changeUserTypeModalState = { changeUserTypeModalState with NewUserType = Some newUserType }
            { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, Cmd.none
        | ChangeUserType, _ ->
            let userId, _ =  changeUserTypeModalState.ForUser
            match users |> findUser userId, changeUserTypeModalState.NewUserType with
            | Some(user, _, _), Some newUserType ->
                let cmd = Cmd.OfAsync.either userApi.changeUserType (authUser.Jwt, userId, newUserType, user.Rvn) ChangeUserTypeResult ChangeUserTypeExn |> Cmd.map ChangeUserTypeInput
                let changeUserTypeModalState = { changeUserTypeModalState with ModalStatus = Some ModalPending }
                { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, cmd
            | Some _, None ->
                let changeUserTypeModalState = { changeUserTypeModalState with ModalStatus = Some (ModalFailed UNEXPECTED_ERROR) }
                let state = { state with ChangeUserTypeModalState = Some changeUserTypeModalState }
                state, shouldNeverHappenCmd (sprintf "Unexpected ChangeUserType when NewUserType is None (%A)" state)
            | None, _ ->
                let changeUserTypeModalState = { changeUserTypeModalState with ModalStatus = Some (ModalFailed UNEXPECTED_ERROR) }
                let state = { state with ChangeUserTypeModalState = Some changeUserTypeModalState }
                state, shouldNeverHappenCmd (sprintf "Unexpected ChangeUserType when %A not found in users (%A)" userId state)
        | CancelChangeUserType, _ -> { state with ChangeUserTypeModalState = None }, Cmd.none
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState is None (%A)" changeUserTypeModalInput state)
let private handleChangeUserTypeInput changeUserTypeInput (authUser:AuthUser) (users:UserData list) state =
    match state.ChangeUserTypeModalState with
    | Some changeUserTypeModalState ->
        match changeUserTypeModalState.ModalStatus with
        | Some ModalPending ->
            match changeUserTypeInput with
            | ChangeUserTypeResult(Ok(UserName userName)) ->
                { state with ChangeUserTypeModalState = None }, sprintf "Type changed for <strong>%s</strong>" userName |> successToastCmd
            | ChangeUserTypeResult(Error error) ->
                let changeUserTypeModalState = { changeUserTypeModalState with ModalStatus = Some(ModalFailed error) }
                { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, Cmd.none // no need for toast (since error will be displayed on ChangeUserTypeModal)
            | ChangeUserTypeExn exn -> state, ChangeUserTypeInput(ChangeUserTypeResult(Error exn.Message)) |> Cmd.ofMsg
        | _ -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState.ModalStatus is not Pending (%A)" changeUserTypeInput state)
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState is None (%A)" changeUserTypeInput state)

let initialize (_:AuthUser) : State * Cmd<Input> =
    let state = {
        CreateUsersModalState = None
        ResetPasswordModalState = None
        ChangeUserTypeModalState = None }
    state, Cmd.none

let transition authUser (usersData:RemoteData<UserData list, string>) input state =
    match input, usersData with
    // Note: AddMessage will have been handled by Program.State.transition.
    | ShowCreateUsersModal, Received _ ->
        match state.CreateUsersModalState with
        | Some _ -> state, shouldNeverHappenCmd (sprintf "Unexpected ShowCreateUsersModal when CreateUsersModalState is not None (%A)" state)
        | None -> { state with CreateUsersModalState = Some(_createUsersModalState Pleb None) }, Cmd.none
    | CreateUsersModalInput createUsersModalInput, Received(users, _) -> state |> handleCreateUsersModalInput createUsersModalInput authUser users
    | CreateUserInput createUserInput, Received(users, _) -> state |> handleCreateUserInput createUserInput authUser users
    | ShowResetPasswordModal(userId, rvn), Received _ ->
        match state.ResetPasswordModalState with
        | Some _ -> state, shouldNeverHappenCmd (sprintf "Unexpected ShowResetPasswordModal when ResetPasswordModalState is not None (%A)" state)
        | None -> { state with ResetPasswordModalState = Some(resetPasswordModalState (userId, rvn)) }, Cmd.none
    | ResetPasswordModalInput resetPasswordModalInput, Received(users, _) -> state |> handleResetPasswordModalInput resetPasswordModalInput authUser users
    | ResetPasswordInput resetPasswordInput, Received(users, _) -> state |> handleResetPasswordInput resetPasswordInput authUser users
    | ShowChangeUserTypeModal(userId, rvn), Received _ ->
        match state.ChangeUserTypeModalState with
        | Some _ -> state, shouldNeverHappenCmd (sprintf "Unexpected ShowChangeUserTypeModal when ChangeUserTypeModalState is not None (%A)" state)
        | None -> { state with ChangeUserTypeModalState = Some(changeUserTypeModalState (userId, rvn)) }, Cmd.none
    | ChangeUserTypeModalInput changeUserTypeModalInput, Received(users, _) -> state |> handleChangeUserTypeModalInput changeUserTypeModalInput authUser users
    | ChangeUserTypeInput changeUserTypeInput, Received(users, _) -> state |> handleChangeUserTypeInput changeUserTypeInput authUser users
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)