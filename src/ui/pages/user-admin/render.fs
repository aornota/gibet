module Aornota.Gibet.Ui.Pages.UserAdmin.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Pages.UserAdmin.Common
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System

open Fable.React.Helpers

open Fulma

let [<Literal>] PAGE_TITLE = "User administration"

let private userTypes = [ BenevolentDictatorForLife ; Administrator ; Pleb ; PersonaNonGrata ] |> List.map (fun userType -> userType, Guid.NewGuid())

let private userTypeRadios theme authUser user selectedUserType disableAll dispatch =
    let onChange userType = (fun _ -> userType |> dispatch)
    userTypes
    |> List.map (fun (userType, key) ->
        let selected = Some userType = selectedUserType
        let allowed, current =
            match user with
            | Some user -> canChangeUserTypeTo (user.UserId, user.UserType) userType (authUser.User.UserId, authUser.User.UserType), userType = user.UserType
            | None -> canCreateUser userType authUser.User.UserType, false
        let colour =
            if selected then
                if allowed then IsSuccess else IsWarning
            else if current then IsLight
            else IsLink
        let hasBackgrounColour = selected || current
        let disabled = disableAll || not allowed
        let onChange = if selected || disabled then ignore else onChange userType
        radioInlineTSmall theme colour hasBackgrounColour key selected (userTypeElement userType) disabled onChange)

let private renderCreateUsersModal (theme, authUser, users:UserData list, createUsersModalState:CreateUsersModalState) dispatch =
    let title = [ contentCentred None [ paraSmall [ str "Add user/s" ] ] ]
    let onDismiss, creatingUser, addUserInteraction, onEnter, userNameStatus, passwordStatus, confirmPasswordStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CloseCreateUsersModal), (fun _ -> dispatch CreateUser)
        match createUsersModalState.CreateUserApiStatus with
        | Some ApiPending -> None, true, Loading, ignore, None, None, None
        | _ ->
            let userName = (UserName createUsersModalState.UserName)
            let password, confirmPassword = Password createUsersModalState.Password, Password createUsersModalState.ConfirmPassword
            let userNameError = validateUserName false (UserName createUsersModalState.UserName) (users |> List.map (fun (user, _, _) -> user.UserName))
            let passwordError = validatePassword false password userName
            let confirmPasswordError = validateConfirmPassword true confirmPassword password
            let addUserInteraction, onEnter =
                match userNameError, passwordError, confirmPasswordError with
                | None, None, None -> Clickable onEnter, onEnter
                | _ -> NotEnabled, ignore
            let userNameStatus =
                match createUsersModalState.UserNameChanged, userNameError with
                | true, Some error -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                | true, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The user name is valid and available" ])
                | _ -> None
            let passwordStatus =
                match createUsersModalState.PasswordChanged, passwordError with
                | true, Some error -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                | true, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The password is valid" ])
                | _ -> None
            let confirmPasswordStatus =
                match createUsersModalState.ConfirmPasswordChanged, confirmPasswordError, passwordError with
                | true, Some error, _ -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                | true, None, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The confirmation password is valid" ])
                | _ -> None
            Some onDismiss, false, addUserInteraction, onEnter, userNameStatus, passwordStatus, confirmPasswordStatus
    let body = [
        match createUsersModalState.CreateUserApiStatus with
        | Some(ApiFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to add user " ; strong createUsersModalState.UserName ]
                contentLeftSmallest [ str error ] ]
            yield br
        | _ ->
            match createUsersModalState.LastUserNameCreated with
            | Some(UserName userName) ->
                yield notificationT theme IsInfo None [ contentCentredSmaller [ str "User " ; strong userName ; str " has been added" ] ]
                yield br
            | _ -> ()
        yield contentCentredSmaller [ str "Please enter the details for the new user" ]
        yield fieldDefault [
            labelSmallest [ str "User name" ]
            textTDefault theme createUsersModalState.UserNameKey createUsersModalState.UserName userNameStatus ICON__USER true creatingUser (UserNameChanged >> dispatch) onEnter ]
        yield fieldDefault [
            labelSmallest [ str "Password" ]
            textTPassword theme createUsersModalState.PasswordKey createUsersModalState.Password passwordStatus false creatingUser (PasswordChanged >> dispatch) onEnter ]
        yield fieldDefault [
            labelSmallest [ str "Confirm password" ]
            textTPassword theme createUsersModalState.ConfirmPasswordKey createUsersModalState.ConfirmPassword confirmPasswordStatus false creatingUser
                (CreateUsersModalInput.ConfirmPasswordChanged >> dispatch) onEnter ]
        yield fieldGroupedCentred [ yield! userTypeRadios theme authUser None (Some createUsersModalState.UserType) creatingUser (UserTypeChanged >> dispatch) ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink addUserInteraction [ str "Add user" ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderResetPasswordModal (theme, users, resetPasswordModalState:ResetPasswordModalState) dispatch =
    let title, onDismiss, body =
        let onDismiss = (fun _ -> dispatch CloseResetPasswordModal)
        let userId, rvn = resetPasswordModalState.ForUser
        match users |> tryFindUser userId with
        | Some(user, _, _) ->
            let (UserName userName) = user.UserName
            let title = [ contentCentred None [ paraSmall [ str "Reset password for " ; strong userName ] ] ]
            let onDismiss, resettingPassword, resetPasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus =
                let onEnter = (fun _ -> dispatch ResetPassword)
                match resetPasswordModalState.ResetPasswordApiStatus with
                | Some ApiPending -> None, true, Loading, ignore, None, None
                | _ ->
                    let newPassword, confirmPassword = Password resetPasswordModalState.NewPassword, Password resetPasswordModalState.ConfirmPassword
                    let newPasswordError = validatePassword false newPassword (UserName userName)
                    let confirmPasswordError = validateConfirmPassword false confirmPassword newPassword
                    let resetPasswordInteraction, onEnter =
                        match newPasswordError, confirmPasswordError with
                        | None, None -> Clickable onEnter, onEnter
                        | _ -> NotEnabled, ignore
                    let newPasswordStatus =
                        match resetPasswordModalState.NewPasswordChanged, newPasswordError with
                        | true, Some error -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                        | true, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The new password is valid" ])
                        | _ -> None
                    let confirmPasswordStatus =
                        match resetPasswordModalState.ConfirmPasswordChanged, confirmPasswordError, newPasswordError with
                        | true, Some error, _ -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                        | true, None, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The confirmation password is valid" ])
                        | _ -> None
                    Some onDismiss, false, resetPasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus
            let body = [
                if user.Rvn <> rvn then
                    yield notificationT theme IsWarning None [ contentCentredSmaller [ strong userName ; str " has been modified by another connection" ] ]
                    yield br
                match resetPasswordModalState.ResetPasswordApiStatus with
                | Some(ApiFailed error) ->
                    yield notificationT theme IsDanger None [
                        contentCentredSmaller [ str "Unable to reset password for " ; strong userName ]
                        contentLeftSmallest [ str error ] ]
                    yield br
                | _ -> ()
                yield contentCentredSmaller [ str "Please enter and confirm the new password for " ; strong userName ]
                yield fieldDefault [
                    labelSmallest [ str "New password" ]
                    textTPassword theme resetPasswordModalState.NewPasswordKey resetPasswordModalState.NewPassword newPasswordStatus true resettingPassword
                        (NewPasswordChanged >> dispatch) onEnter ]
                yield fieldDefault [
                    labelSmallest [ str "Confirm password" ]
                    textTPassword theme resetPasswordModalState.ConfirmPasswordKey resetPasswordModalState.ConfirmPassword confirmPasswordStatus false resettingPassword
                        (ConfirmPasswordChanged >> dispatch) onEnter ]
                yield fieldGroupedCentred [ buttonTSmall theme IsLink resetPasswordInteraction [ str "Reset password" ] ] ]
            title, onDismiss, body
        | None -> // should never happen
            let title = [ contentCentred None [ paraSmall [ str "Reset password" ] ] ]
            let body = [ notificationT theme IsDanger None [
                contentCentredSmaller [str "Unable to reset password" ]
                contentLeftSmallest [ str (ifDebug (sprintf "%A not found in users (%A)" userId users) UNEXPECTED_ERROR) ] ] ]
            title, Some onDismiss, body
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangeUserTypeModal (theme, authUser, users, changeUserTypeModalState:ChangeUserTypeModalState) dispatch =
    let title, onDismiss, body =
        let onDismiss = (fun _ -> dispatch CloseChangeUserTypeModal)
        let userId, rvn = changeUserTypeModalState.ForUser
        match users |> tryFindUser userId with
        | Some(user, _, _) ->
            let (UserName userName) = user.UserName
            let title = [ contentCentred None [ paraSmall [ str "Change type for " ; strong userName ] ] ]
            let onDismiss, changingUserType, changeUserTypeInteraction =
                match changeUserTypeModalState.ChangeUserTypeApiStatus with
                | Some ApiPending -> None, true, Loading
                | _ ->
                    let changeUserTypeInteraction =
                        match changeUserTypeModalState.NewUserType with
                        | Some newUserType when newUserType <> user.UserType -> Clickable(fun _ -> dispatch ChangeUserType)
                        | _ -> NotEnabled
                    Some onDismiss, false, changeUserTypeInteraction
            let body = [
                if user.Rvn <> rvn then
                    yield notificationT theme IsWarning None [ contentCentredSmaller [ strong userName ; str " has been modified by another connection" ] ]
                    yield br
                match changeUserTypeModalState.ChangeUserTypeApiStatus with
                | Some(ApiFailed error) ->
                    yield notificationT theme IsDanger None [
                        contentCentredSmaller [ str "Unable to change user type for " ; strong userName ]
                        contentLeftSmallest [ str error ] ]
                    yield br
                | _ -> ()
                yield contentCentred None [
                    paraSmaller [ str "Please choose the new type for " ; strong userName ]
                    paraTSmallest theme IsPrimary [ strong "Current type is " ; userTypeElement user.UserType ] ]
                yield fieldGroupedCentred [ yield! userTypeRadios theme authUser (Some user) changeUserTypeModalState.NewUserType changingUserType (NewUserTypeChanged >> dispatch) ]
                yield fieldGroupedCentred [ buttonTSmall theme IsLink changeUserTypeInteraction [ str "Change type" ] ] ]
            title, onDismiss, body
        | None -> // should never happen
            let title = [ contentCentred None [ paraSmall [ str "Change type" ] ] ]
            let body = [ notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to change type" ]
                contentLeftSmallest [ str (ifDebug (sprintf "%A not found in users (%A)" userId users) UNEXPECTED_ERROR) ] ] ]
            title, Some onDismiss, body
    cardModalT theme (Some(title, onDismiss)) body

let private renderUsers (theme, authUser, users:UserData list, _:int<tick>) dispatch =
    if users.Length > 0 then
        let resetPassword (userId, userType, rvn) =
            if canResetPassword (userId, userType) (authUser.User.UserId, authUser.User.UserType) then
                Some(contentRightSmallest [ linkInternal (fun _ -> dispatch (ShowResetPasswordModal(userId, rvn))) [ str "Reset password" ] ])
            else None
        let changeUserType (userId, userType, rvn) =
            if canChangeUserType (userId, userType) (authUser.User.UserId, authUser.User.UserType) then
                Some(contentRightSmallest [ linkInternal (fun _ -> dispatch (ShowChangeUserTypeModal(userId, rvn))) [ str "Change type" ] ])
            else None
        let userRow (user, signedIn, lastActivity) =
            let imageUrl = match user.ImageUrl with | Some(ImageUrl imageUrl) -> imageUrl | None -> "blank-48x48.png"
            tr false [
                td [ image imageUrl Image.Is48x48 ]
                td [ tagTUserSmall theme authUser.User.UserId (user, signedIn, lastActivity) ]
                td [ contentCentredSmallest [ userTypeElement user.UserType ] ]
                td [ ofOption (changeUserType (user.UserId, user.UserType, user.Rvn)) ]
                td [ ofOption (resetPassword (user.UserId, user.UserType, user.Rvn)) ] ]
        let userRows =
            users
            |> List.sortBy (fun (user, _, _) -> user.UserType, user.UserName)
            |> List.map userRow
        tableTDefault theme false [
            thead [ tr false [
                th []
                th [ contentLeftSmallest [ strong "User name" ] ]
                th [ contentCentredSmallest [ strong "Type" ] ]
                th []
                th [] ] ]
            tbody [ yield! userRows ] ]
    else renderWarningMessage theme "There are no users!"

let private createUsers authUser dispatch =
    if canAdministerUsers authUser.User.UserType then Some(paraSmaller [ linkInternal (fun _ -> dispatch ShowCreateUsersModal) [ str "Add user/s" ] ])
    else None // should never happen

let render theme authUser usersData state ticks dispatch =
    divDefault [
        // Note: Render Modals (if any) first so will not be affected by contentCentred.
        match usersData with
        | Received(users, _) ->
            match state.CreateUsersModalState, state.ResetPasswordModalState, state.ChangeUserTypeModalState with
            | Some createUsersModalState, _, _ -> yield lazyView2 renderCreateUsersModal (theme, authUser, users, createUsersModalState) (CreateUsersModalInput >> dispatch)
            | _, Some resetPasswordModalState, _ -> yield lazyView2 renderResetPasswordModal (theme, users, resetPasswordModalState) (ResetPasswordModalInput >> dispatch)
            | _, _, Some changeUserTypeModalState -> yield lazyView2 renderChangeUserTypeModal (theme, authUser, users, changeUserTypeModalState) (ChangeUserTypeModalInput >> dispatch)
            | _ -> ()
        | _ -> ()
        yield columnsDefault [ contentCentred None [
            yield paraSmall [ strong PAGE_TITLE ]
            yield hrT theme false
            match usersData with
            | Pending -> yield contentCentred None [ divVerticalSpace 15 ; iconLarge ICON__SPINNER_PULSE ]
            | Received(users, _) ->
                yield ofOption (createUsers authUser dispatch)
                yield lazyView2 renderUsers (theme, authUser, users, ticks) dispatch
            | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "Users RemoteData Failed -> %s" error) UNEXPECTED_ERROR)
            yield divVerticalSpace 5 ] ] ]
