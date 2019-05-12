module Aornota.Gibet.Ui.Pages.Chat.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render.Shared
open Aornota.Gibet.Ui.Common.Render.Theme
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.MarkdownLiterals
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open Fulma

let [<Literal>] private PAGE_TITLE = "Chat"

let pageTitle (state:State) = PAGE_TITLE // TODO-NMB: "Unseen count" &c.?...

let renderTab theme isActive (state:State) onClick = tab isActive [ paraTSmallest theme [ linkTInternal theme onClick [ str PAGE_TITLE ] ] ] // TODO-NMB: "Unseen count" &c.?...

let render theme (authUser:AuthUser) (usersData:RemoteData<UserData list, string>) (state:State) (_:int<tick>) (dispatch:Input -> unit) =
    columnsDefault [ contentCentred [
        yield paraT theme TextSize.Is5 IsBlack TextWeight.Bold [ str "Chat" ]
        yield hr theme false
        match usersData with
        | NotRequested -> yield renderDangerMessage theme (ifDebug "UserData has not been requested" UNEXPECTED_ERROR)
        | Pending -> yield paraT theme TextSize.Is7 IsDark TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ]
        | Received(users, _) -> // TODO-NMB...
            yield renderInfoMessage theme "Chat functionality coming soon..."
        | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "UserData Failed -> %s" error) UNEXPECTED_ERROR) ] ]
