module Aornota.Gibet.Ui.Pages.UserAdmin.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Pages.UserAdmin.Common
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared
open Aornota.Gibet.Ui.UsersApi

open System

open Elmish

let private addMessageCmd messageType text = addMessageCmd messageType text (AddMessage >> Cmd.ofMsg)
let private addDebugErrorCmd error = addDebugErrorCmd error (AddMessage >> Cmd.ofMsg)
let private shouldNeverHappenCmd error = shouldNeverHappenCmd error (AddMessage >> Cmd.ofMsg)

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
    CreateUserApiStatus = None }
let private resetPasswordModalState forUser = {
    ForUser = forUser
    NewPasswordKey = Guid.NewGuid()
    NewPassword = String.Empty
    NewPasswordChanged = false
    ConfirmPasswordKey = Guid.NewGuid()
    ConfirmPassword = String.Empty
    ConfirmPasswordChanged = false
    ResetPasswordApiStatus = None }
let private changeUserTypeModalState forUser = {
    ForUser = forUser
    NewUserType = None
    ChangeUserTypeApiStatus = None }

let private handleCreateUsersModalInput createUsersModalInput (authUser:AuthUser) (users:UserData list) state =
    match state.CreateUsersModalState with
    | Some createUsersModalState ->
        match createUsersModalInput, createUsersModalState.CreateUserApiStatus with
        | _, Some ApiPending -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState.CreateUserApiStatus is Pending (%A)" createUsersModalInput state)
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
            let cmd = Cmd.OfAsync.either usersApi.createUser (authUser.Jwt, userName, password, createUsersModalState.UserType) CreateUserResult CreateUserExn |> Cmd.map CreateUserApiInput
            let createUsersModalState = { createUsersModalState with LastUserNameCreated = None ; CreateUserApiStatus = Some ApiPending }
            { state with CreateUsersModalState = Some createUsersModalState }, cmd
        | CloseCreateUsersModal, _ -> { state with CreateUsersModalState = None }, Cmd.none
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState is None (%A)" createUsersModalInput state)
let private handleCreateUserApiInput createUserApiInput (authUser:AuthUser) (users:UserData list) state =
    match state.CreateUsersModalState with
    | Some createUsersModalState ->
        match createUsersModalState.CreateUserApiStatus with
        | Some ApiPending ->
            match createUserApiInput with
            | CreateUserResult(Ok(_, UserName userName)) ->
                let toastCmd = sprintf "User <strong>%s</strong> added" userName |> successToastCmd
                { state with CreateUsersModalState = Some(_createUsersModalState createUsersModalState.UserType (Some(UserName userName))) }, toastCmd
            | CreateUserResult(Error error) ->
                let createUsersModalState = { createUsersModalState with CreateUserApiStatus = Some(ApiFailed error) }
                { state with CreateUsersModalState = Some createUsersModalState }, Cmd.none // no need for toast (since error will be displayed on CreateUsersModal)
            | CreateUserExn exn -> state, CreateUserApiInput(CreateUserResult(Error exn.Message)) |> Cmd.ofMsg
        | _ -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState.CreateUserApiStatus is not Pending (%A)" createUserApiInput state)
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when CreateUsersModalState is None (%A)" createUserApiInput state)

let private handleResetPasswordModalInput resetPasswordModalInput (authUser:AuthUser) (users:UserData list) state =
    match state.ResetPasswordModalState with
    | Some resetPasswordModalState ->
        match resetPasswordModalInput, resetPasswordModalState.ResetPasswordApiStatus with
        | _, Some ApiPending -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState.ResetPasswordApiStatus is Pending (%A)" resetPasswordModalInput state)
        | NewPasswordChanged newPassword, _ ->
            let resetPasswordModalState = { resetPasswordModalState with NewPassword = newPassword ; NewPasswordChanged = true }
            { state with ResetPasswordModalState = Some resetPasswordModalState }, Cmd.none
        | ConfirmPasswordChanged confirmPassword, _ ->
            let resetPasswordModalState = { resetPasswordModalState with ConfirmPassword = confirmPassword ; ConfirmPasswordChanged = true }
            { state with ResetPasswordModalState = Some resetPasswordModalState }, Cmd.none
        | ResetPassword, _ ->
            let userId, _ =  resetPasswordModalState.ForUser
            match users |> tryFindUser userId with
            | Some(user, _, _) ->
                let password = Password(resetPasswordModalState.NewPassword.Trim())
                let cmd = Cmd.OfAsync.either usersApi.resetPassword (authUser.Jwt, userId, password, user.Rvn) ResetPasswordResult ResetPasswordExn |> Cmd.map ResetPasswordApiInput
                let resetPasswordModalState = { resetPasswordModalState with ResetPasswordApiStatus = Some ApiPending }
                { state with ResetPasswordModalState = Some resetPasswordModalState }, cmd
            | None ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected ResetPassword when %A not found in users (%A)" userId state)
                let resetPasswordModalState = { resetPasswordModalState with ResetPasswordApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                { state with ResetPasswordModalState = Some resetPasswordModalState }, cmd
        | CloseResetPasswordModal, _ -> { state with ResetPasswordModalState = None }, Cmd.none
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState is None (%A)" resetPasswordModalInput state)
let private handleResetPasswordApiInput resetPasswordApiInput (authUser:AuthUser) (users:UserData list) state =
    match state.ResetPasswordModalState with
    | Some resetPasswordModalState ->
        match resetPasswordModalState.ResetPasswordApiStatus with
        | Some ApiPending ->
            match resetPasswordApiInput with
            | ResetPasswordResult(Ok(UserName userName)) ->
                { state with ResetPasswordModalState = None }, sprintf "Password reset for <strong>%s</strong>" userName |> successToastCmd
            | ResetPasswordResult(Error error) ->
                let resetPasswordModalState = { resetPasswordModalState with ResetPasswordApiStatus = Some(ApiFailed error) }
                { state with ResetPasswordModalState = Some resetPasswordModalState }, Cmd.none // no need for toast (since error will be displayed on ResetPasswordModal)
            | ResetPasswordExn exn -> state, ResetPasswordApiInput(ResetPasswordResult(Error exn.Message)) |> Cmd.ofMsg
        | _ -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState.ResetPasswordApiStatus is not Pending (%A)" resetPasswordApiInput state)
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ResetPasswordModalState is None (%A)" resetPasswordApiInput state)

let private handleChangeUserTypeModalInput changeUserTypeModalInput (authUser:AuthUser) (users:UserData list) state =
    match state.ChangeUserTypeModalState with
    | Some changeUserTypeModalState ->
        match changeUserTypeModalInput, changeUserTypeModalState.ChangeUserTypeApiStatus with
        | _, Some ApiPending -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState.ChangeUserTypeApiStatus is Pending (%A)" changeUserTypeModalInput state)
        | NewUserTypeChanged newUserType, _ ->
            let changeUserTypeModalState = { changeUserTypeModalState with NewUserType = Some newUserType }
            { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, Cmd.none
        | ChangeUserType, _ ->
            let userId, _ =  changeUserTypeModalState.ForUser
            match users |> tryFindUser userId, changeUserTypeModalState.NewUserType with
            | Some(user, _, _), Some newUserType ->
                let cmd = Cmd.OfAsync.either usersApi.changeUserType (authUser.Jwt, userId, newUserType, user.Rvn) ChangeUserTypeResult ChangeUserTypeExn |> Cmd.map ChangeUserTypeApiInput
                let changeUserTypeModalState = { changeUserTypeModalState with ChangeUserTypeApiStatus = Some ApiPending }
                { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, cmd
            | Some _, None ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected ChangeUserType when NewUserType is None (%A)" state)
                let changeUserTypeModalState = { changeUserTypeModalState with ChangeUserTypeApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, cmd
            | None, _ ->
                let cmd = shouldNeverHappenCmd (sprintf "Unexpected ChangeUserType when %A not found in users (%A)" userId state)
                let changeUserTypeModalState = { changeUserTypeModalState with ChangeUserTypeApiStatus = Some(ApiFailed UNEXPECTED_ERROR) }
                { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, cmd
        | CloseChangeUserTypeModal, _ -> { state with ChangeUserTypeModalState = None }, Cmd.none
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState is None (%A)" changeUserTypeModalInput state)
let private handleChangeUserTypeApiInput changeUserTypeApiInput (authUser:AuthUser) (users:UserData list) state =
    match state.ChangeUserTypeModalState with
    | Some changeUserTypeModalState ->
        match changeUserTypeModalState.ChangeUserTypeApiStatus with
        | Some ApiPending ->
            match changeUserTypeApiInput with
            | ChangeUserTypeResult(Ok(UserName userName)) ->
                { state with ChangeUserTypeModalState = None }, sprintf "Type changed for <strong>%s</strong>" userName |> successToastCmd
            | ChangeUserTypeResult(Error error) ->
                let changeUserTypeModalState = { changeUserTypeModalState with ChangeUserTypeApiStatus = Some(ApiFailed error) }
                { state with ChangeUserTypeModalState = Some changeUserTypeModalState }, Cmd.none // no need for toast (since error will be displayed on ChangeUserTypeModal)
            | ChangeUserTypeExn exn -> state, ChangeUserTypeApiInput(ChangeUserTypeResult(Error exn.Message)) |> Cmd.ofMsg
        | _ -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState.ChangeUserTypeApiStatus is not Pending (%A)" changeUserTypeApiInput state)
    | None -> state, shouldNeverHappenCmd (sprintf "Unexpected %A when ChangeUserTypeModalState is None (%A)" changeUserTypeApiInput state)

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
    | CreateUserApiInput createUserApiInput, Received(users, _) -> state |> handleCreateUserApiInput createUserApiInput authUser users
    | ShowResetPasswordModal(userId, rvn), Received _ ->
        match state.ResetPasswordModalState with
        | Some _ -> state, shouldNeverHappenCmd (sprintf "Unexpected ShowResetPasswordModal when ResetPasswordModalState is not None (%A)" state)
        | None -> { state with ResetPasswordModalState = Some(resetPasswordModalState (userId, rvn)) }, Cmd.none
    | ResetPasswordModalInput resetPasswordModalInput, Received(users, _) -> state |> handleResetPasswordModalInput resetPasswordModalInput authUser users
    | ResetPasswordApiInput resetPasswordApiInput, Received(users, _) -> state |> handleResetPasswordApiInput resetPasswordApiInput authUser users
    | ShowChangeUserTypeModal(userId, rvn), Received _ ->
        match state.ChangeUserTypeModalState with
        | Some _ -> state, shouldNeverHappenCmd (sprintf "Unexpected ShowChangeUserTypeModal when ChangeUserTypeModalState is not None (%A)" state)
        | None -> { state with ChangeUserTypeModalState = Some(changeUserTypeModalState (userId, rvn)) }, Cmd.none
    | ChangeUserTypeModalInput changeUserTypeModalInput, Received(users, _) -> state |> handleChangeUserTypeModalInput changeUserTypeModalInput authUser users
    | ChangeUserTypeApiInput changeUserTypeApiInput, Received(users, _) -> state |> handleChangeUserTypeApiInput changeUserTypeApiInput authUser users
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)
