module Aornota.Gibet.Ui.Pages.Chat.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.MarkdownLiterals
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

let [<Literal>] private PAGE_TITLE = "Chat"

let pageTitle (state:State) = PAGE_TITLE // TODO-NMB: "Unseen count" &c.?...

let renderTab isActive (state:State) onClick = tab isActive [ linkInternal onClick [ str PAGE_TITLE ] ] // TODO-NMB: "Unseen count" &c.?...

let render theme (authUser:AuthUser) (usersData:RemoteData<UserData list, string>) (state:State) (_:int<tick>) (dispatch:Input -> unit) =
    divDefault [
        // Note: Render Modals (if any) first so will not be affected by contentCentred.
        columnsDefault [ contentCentred None [
            yield paraSmall [ strong "Chat" ]
            yield hrT theme false
            match usersData with
            | NotRequested -> yield renderDangerMessage theme (ifDebug "UserData has not been requested" UNEXPECTED_ERROR)
            | Pending -> yield contentCentred None [ divVerticalSpace 15 ; iconLarge ICON__SPINNER_PULSE ]
            | Received(users, _) -> // TODO-NMB...
                //yield renderWarningMessage theme "Chat functionality is a work in progress..."
                yield renderInfoMessage theme "Chat functionality is coming soon..."
            | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "UserData Failed -> %s" error) UNEXPECTED_ERROR) ] ]
        divVerticalSpace 5 ]
