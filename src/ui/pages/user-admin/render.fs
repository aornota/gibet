module Aornota.Gibet.Ui.Pages.UserAdmin.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render.Shared
open Aornota.Gibet.Ui.Common.Render.Theme
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
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Add user/s" ] ] ]
    let onDismiss, creatingUser, addUserInteraction, onEnter, userNameStatus, passwordStatus, confirmPasswordStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CancelCreateUsers), (fun _ -> dispatch CreateUser)
        match createUsersModalState.ModalStatus with
        | Some ModalPending -> None, true, Loading, ignore, None, None, None
        | _ ->
            let password, confirmPassword = Password createUsersModalState.Password, Password createUsersModalState.ConfirmPassword
            let userNameError = validateUserName false (UserName createUsersModalState.UserName) (users |> List.map (fun (user, _, _) -> user.UserName))
            let passwordError = validatePassword false password
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
        match createUsersModalState.ModalStatus with
        | Some(ModalFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to add user " ; bold createUsersModalState.UserName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | _ -> ()
        match createUsersModalState.LastUserNameCreated with
        | Some(UserName userName) ->
            yield notificationT theme IsInfo None [
                contentCentred [ paraTSmallest theme [ str "User " ; bold userName ; str " has been added" ] ] ]
            yield br
        | _ -> ()
        yield contentCentred [ paraTSmaller theme [ str "Please enter the details for the new user" ] ]
        yield fieldDefault [
            labelTSmallest theme [ str "User name" ]
            textTDefault theme createUsersModalState.UserNameKey createUsersModalState.UserName userNameStatus ICON__USER true creatingUser (UserNameChanged >> dispatch) onEnter ]
        yield fieldDefault [
            labelTSmallest theme [ str "Password" ]
            textTPassword theme createUsersModalState.PasswordKey createUsersModalState.Password passwordStatus false creatingUser (PasswordChanged >> dispatch) onEnter ]
        yield fieldDefault [
            labelTSmallest theme [ str "Confirm password" ]
            textTPassword theme createUsersModalState.ConfirmPasswordKey createUsersModalState.ConfirmPassword confirmPasswordStatus false creatingUser
                (CreateUsersModalInput.ConfirmPasswordChanged >> dispatch) onEnter ]
        yield fieldGroupedCentred [ yield! userTypeRadios theme authUser None (Some createUsersModalState.UserType) creatingUser (UserTypeChanged >> dispatch) ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink addUserInteraction [ str "Add user" ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderResetPasswordModal (theme, authUser, users:UserData list, resetPasswordModalState:ResetPasswordModalState) dispatch =
    let title, onDismiss, body =
        let onDismiss = (fun _ -> dispatch CancelResetPassword)
        let userId, rvn = resetPasswordModalState.ForUser
        match users |> findUser userId with
        | Some(user, _, _) ->
            let (UserName userName) = user.UserName
            let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Reset password for " ; bold userName ] ] ]
            let onDismiss, resettingPassword, resetPasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus =
                let onEnter = (fun _ -> dispatch ResetPassword)
                match resetPasswordModalState.ModalStatus with
                | Some ModalPending -> None, true, Loading, ignore, None, None
                | _ ->
                    let newPassword, confirmPassword = Password resetPasswordModalState.NewPassword, Password resetPasswordModalState.ConfirmPassword
                    let newPasswordError = validatePassword false newPassword
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
                    yield notificationT theme IsWarning None [ contentCentred [ paraTSmaller theme [ bold userName ; str " has been modified by another user" ] ] ]
                    yield br
                match resetPasswordModalState.ModalStatus with
                | Some(ModalFailed error) ->
                    yield notificationT theme IsDanger None [
                        contentCentred [ paraTSmaller theme [ str "Unable to reset password for " ; bold userName ] ]
                        paraTSmallest theme [ str error ] ]
                    yield br
                | _ -> ()
                yield contentCentred [ paraTSmaller theme [ str "Please enter and confirm the new password for " ; bold userName ] ]
                yield fieldDefault [
                    labelTSmallest theme [ str "New password" ]
                    textTPassword theme resetPasswordModalState.NewPasswordKey resetPasswordModalState.NewPassword newPasswordStatus true resettingPassword
                        (NewPasswordChanged >> dispatch) onEnter ]
                yield fieldDefault [
                    labelTSmallest theme [ str "Confirm password" ]
                    textTPassword theme resetPasswordModalState.ConfirmPasswordKey resetPasswordModalState.ConfirmPassword confirmPasswordStatus false resettingPassword
                        (ConfirmPasswordChanged >> dispatch) onEnter ]
                yield fieldGroupedCentred [ buttonTSmall theme IsLink resetPasswordInteraction [ str "Reset password" ] ] ]
            title, onDismiss, body
        | None -> // should never happen
            let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Reset password" ] ] ]
            let body = [ notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to reset password" ] ]
                contentLeft [ paraTSmallest theme [ str (ifDebug (sprintf "%A not found in users (%A)" userId users) UNEXPECTED_ERROR) ] ] ] ]
            title, Some onDismiss, body
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangeUserTypeModal (theme, authUser, users:UserData list, changeUserTypeModalState:ChangeUserTypeModalState) dispatch =
    let title, onDismiss, body =
        let onDismiss = (fun _ -> dispatch CancelChangeUserType)
        let userId, rvn = changeUserTypeModalState.ForUser
        match users |> findUser userId with
        | Some(user, _, _) ->
            let (UserName userName) = user.UserName
            let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Change type for " ; bold userName ] ] ]
            let onDismiss, changingUserType, changeUserTypeInteraction =
                match changeUserTypeModalState.ModalStatus with
                | Some ModalPending -> None, true, Loading
                | _ ->
                    let changeUserTypeInteraction =
                        match changeUserTypeModalState.NewUserType with
                        | Some newUserType when newUserType <> user.UserType -> Clickable(fun _ -> dispatch ChangeUserType)
                        | _ -> NotEnabled
                    Some onDismiss, false, changeUserTypeInteraction
            let body = [
                if user.Rvn <> rvn then
                    yield notificationT theme IsWarning None [ contentCentred [ paraTSmaller theme [ bold userName ; str " has been modified by another user" ] ] ]
                    yield br
                match changeUserTypeModalState.ModalStatus with
                | Some(ModalFailed error) ->
                    yield notificationT theme IsDanger None [
                        contentCentred [ paraTSmaller theme [ str "Unable to change user type for " ; bold userName ] ]
                        paraTSmallest theme [ str error ] ]
                    yield br
                | _ -> ()
                yield contentCentred [
                    paraTSmaller theme [ str "Please choose the new type for " ; bold userName ]
                    paraT theme TextSize.Is7 IsPrimary TextWeight.Normal [ str "Current type is " ; userTypeElement user.UserType ] ]
                yield fieldGroupedCentred [ yield! userTypeRadios theme authUser (Some user) changeUserTypeModalState.NewUserType changingUserType (NewUserTypeChanged >> dispatch) ]
                yield fieldGroupedCentred [ buttonTSmall theme IsLink changeUserTypeInteraction [ str "Change type" ] ] ]
            title, onDismiss, body
        | None -> // should never happen
            let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Change type" ] ] ]
            let body = [ notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to change type" ] ]
                contentLeft [ paraTSmallest theme [ str (ifDebug (sprintf "%A not found in users (%A)" userId users) UNEXPECTED_ERROR) ] ] ] ]
            title, Some onDismiss, body
    cardModalT theme (Some(title, onDismiss)) body

let private renderUsers (theme, authUser, users:UserData list, _:int<tick>) dispatch =
    if users.Length > 0 then
        let resetPassword (userId, userType, rvn) =
            if canResetPassword (userId, userType) (authUser.User.UserId, authUser.User.UserType) then
                Some(contentRight [ paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowResetPasswordModal(userId, rvn))) [ str "Reset password" ] ] ])
            else None
        let changeUserType (userId, userType, rvn) =
            if canChangeUserType (userId, userType) (authUser.User.UserId, authUser.User.UserType) then
                Some(contentRight [ paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangeUserTypeModal(userId, rvn))) [ str "Change type" ] ] ])
            else None
        let userRow (user, signedIn, lastActivity) =
            let imageUrl = match user.ImageUrl with | Some(ImageUrl imageUrl) -> imageUrl | None -> "blank-48x48.png"
            tr false [
                td [ image imageUrl Image.Is48x48 ]
                td [ paraTSmallest theme [ tagTUserSmall theme (user, signedIn, lastActivity) authUser.User.UserId ] ]
                td [ contentCentred [ paraTSmallest theme [ userTypeElement user.UserType ] ] ]
                td [ ofOption (changeUserType (user.UserId, user.UserType, user.Rvn)) ]
                td [ ofOption (resetPassword (user.UserId, user.UserType, user.Rvn)) ] ]
        let userRows =
            users
            //|> List.sortBy (fun (user, _, _) -> (match user.UserType with | BenevolentDictatorForLife -> 1 | Administrator -> 2 | Pleb -> 3 | PersonaNonGrata -> 4), user.UserName)
            |> List.sortBy (fun (user, _, _) -> user.UserType, user.UserName)
            |> List.map userRow
        tableTDefault theme false [
            thead [ tr false [
                th []
                th [ paraTSmallest theme [ bold "User name" ] ]
                th [ contentCentred [ paraTSmallest theme [ bold "Type" ] ] ]
                th []
                th [] ] ]
            tbody [ yield! userRows ] ]
    else div [] [ notificationT theme IsWarning None [ paraTSmaller theme [ str "There are no users!" ] ] ; divVerticalSpace 10 ] // should never happen

let private createUsers theme authUser dispatch =
    if canAdministerUsers authUser.User.UserType then Some(paraTSmaller theme [ linkTInternal theme (fun _ -> dispatch ShowCreateUsersModal) [ str "Add user/s" ] ])
    else None // should never happen

let render theme authUser usersData state ticks dispatch = // TODO-NMB: parentHasModal?...
    div [] [ columnsDefault [
        yield contentCentred [
            paraT theme TextSize.Is5 IsBlack TextWeight.Bold [ str "User administration" ]
            hr theme false ]
        match usersData with
        | NotRequested -> yield contentCentred [ renderDangerMessage theme (ifDebug "UsersData has not been requested" UNEXPECTED_ERROR) ]
        | Pending -> yield contentCentred [ paraT theme TextSize.Is7 IsDark TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ] ]
        | Received(users, _) ->
            yield contentCentred [
                ofOption (createUsers theme authUser dispatch)
                lazyView2 renderUsers (theme, authUser, users, ticks) dispatch
                divVerticalSpace 5 ]
            match state.CreateUsersModalState, state.ResetPasswordModalState, state.ChangeUserTypeModalState with
            | Some createUsersModalState, _, _ ->
                yield lazyView2 renderCreateUsersModal (theme, authUser, users, createUsersModalState) (CreateUsersModalInput >> dispatch)
            | _, Some resetPasswordModalState, _ ->
                yield lazyView2 renderResetPasswordModal (theme, authUser, users, resetPasswordModalState) (ResetPasswordModalInput >> dispatch)
            | _, _, Some changeUserTypeModalState ->
                yield lazyView2 renderChangeUserTypeModal (theme, authUser, users, changeUserTypeModalState) (ChangeUserTypeModalInput >> dispatch)
            | _ -> ()
        | Failed error -> yield contentCentred [ renderDangerMessage theme (ifDebug (sprintf "UsersData Failed -> %s" error) UNEXPECTED_ERROR) ] ] ]
