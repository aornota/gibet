module Aornota.Gibet.Ui.Pages.UserAdmin.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render.Shared
open Aornota.Gibet.Ui.Common.Render.Theme
open Aornota.Gibet.Ui.Pages.UserAdmin.Common
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open Fulma

let [<Literal>] PAGE_TITLE = "User administration"

let render theme (authUser:AuthUser) (usersData:RemoteData<UserData list, string>) (state:State) (dispatch:Input -> unit) =
    columnsDefault [ contentCentred [
        yield paraT theme TextSize.Is5 IsBlack TextWeight.Bold [ str "User administration" ]
        yield hr theme false
        match usersData with
        | NotRequested -> yield renderDangerMessage theme (ifDebug "UserData has not been requested" UNEXPECTED_ERROR)
        | Pending -> yield paraT theme TextSize.Is7 IsDark TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ]
        | Received(users, _) -> // TODO-NMB...
            yield renderWarningMessage theme "User administration functionality is a work in progress..."
        | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "UserData Failed -> %s" error) UNEXPECTED_ERROR) ] ]
