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

open Fable.React.Helpers

open Fulma

let [<Literal>] PAGE_TITLE = "User administration"

// TODO-NMB: Modals...

let private renderUsers (theme, authUser, users:UserData list, _:int<tick>) dispatch =
    let resetPassword (userId, userType) =
        if canResetPassword (userId, userType) (authUser.User.UserId, authUser.User.UserType) then
            Some(contentRight [ paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowResetPasswordModal userId)) [ str "Reset password" ] ] ])
        else None
    let changeUserType (userId, userType) =
        if canChangeUserType (userId, userType) (authUser.User.UserId, authUser.User.UserType) then
            Some(contentRight [ paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangeUserTypeModal userId)) [ str "Change type" ] ] ])
        else None
    let userRow (user, signedIn, lastActivity) =
        let image = match user.ImageUrl with | Some(ImageUrl imageUrl) -> Some(image imageUrl Image.Is48x48) | None -> None
        tr false [
            td [ ofOption image ]
            td [ paraTSmallest theme [ tagTUserSmall theme (user, signedIn, lastActivity) authUser.User.UserId ] ]
            td [ contentCentred [ paraTSmallest theme [ userType user.UserType ] ] ]
            td [ ofOption (changeUserType (user.UserId, user.UserType)) ]
            td [ ofOption (resetPassword (user.UserId, user.UserType)) ] ]
    let userRows =
        users
        |> List.sortBy (fun (user, _, _) -> (match user.UserType with | BenevolentDictatorForLife -> 1 | Administrator -> 2 | Pleb -> 3 | PersonaNonGrata -> 4), user.UserName)
        |> List.map userRow
    if users.Length > 0 then
        tableTDefault theme false [
            thead [ tr false [
                th []
                th [ paraTSmallest theme [ bold "User name" ] ]
                th [ contentCentred [ paraTSmallest theme [ bold "Type" ] ] ]
                th []
                th [] ] ]
            tbody [ yield! userRows ] ]
    else paraTSmallest theme [ str "There are no users!" ] // should never happen

let private createUsers theme authUser dispatch =
    if canAdministerUsers authUser.User.UserType then Some(contentRight [ paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch ShowCreateUsersModal) [ str "Add users/s" ] ] ])
    else None // should never happen

let render theme authUser usersData state ticks dispatch = // TODO-NMB: parentHasModal?...
    columnsDefault [ contentCentred [
        yield paraT theme TextSize.Is5 IsBlack TextWeight.Bold [ str "User administration" ]
        yield hr theme false
        match usersData with
        | NotRequested -> yield renderDangerMessage theme (ifDebug "UsersData has not been requested" UNEXPECTED_ERROR)
        | Pending -> yield paraT theme TextSize.Is7 IsDark TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ]
        | Received(users, _) ->
            yield renderWarningMessage theme "User administration functionality is a work in progress..." // TEMP-NMB...
            yield lazyView2 renderUsers (theme, authUser, users, ticks) dispatch
            yield ofOption (createUsers theme authUser dispatch)
            match state.CreateUsersModalState, state.ResetPasswordModalState, state.ChangeUserTypeModalState with
            | Some createUsersModalState, _, _ -> // TODO-NMB...
                ()
            | _, Some resetPasswordModalState, _ -> // TODO-NMB...
                ()
            | _, _, Some changeUserTypeModalState -> // TODO-NMB...
                ()
            | _ -> ()
        | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "UsersData Failed -> %s" error) UNEXPECTED_ERROR) ] ]
