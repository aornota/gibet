module Aornota.Gibet.Ui.Pages.Chat.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Render.Theme.Markdown
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.MarkdownLiterals
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System

open Fulma

let [<Literal>] private PAGE_TITLE = "Chat"

let private markdownSyntaxKey = Guid.NewGuid()

let private renderMarkdownSyntaxModal (theme, users:UserData list) dispatch = // TODO-NMB: textAreaT - and @username / @{user name} example?...
    let title = [ contentCentred None [ paraSmall [ str "Markdown syntax" ] ] ]
    let onDismiss = Some(fun _ -> dispatch CloseMarkdownSyntaxModal)
    let body = [
        contentTCentred theme (Some smaller) (Some IsInfo) [ strong "As a very quick introduction to Markdown syntax, the following:" ]
        // TEMP-NMB...
        notificationT theme IsWarning None [ contentLeftSmallest [ strong "TODO-NMB:" ; str " textAreaT theme markdownSyntaxKey MARKDOWN_SYNTAX..." ] ]
        br
        // ...TEMP-NNB
        contentTCentred theme (Some smaller) (Some IsInfo) [ strong "will appear as:" ]
        markdownContentTLeft theme (Markdown MARKDOWN_SYNTAX) ]
    cardModalT theme (Some(title, onDismiss)) body

let pageTitle state = // TODO-NMB: See inside...
    let unseenCount, unseenTaggedCount =
        match state with
        | ReadingLastTimestampSeen _ -> None, None
        | Ready(_, readyState) ->
            let unseenCount = if readyState.UnseenCount > 0 then Some readyState.UnseenCount else None
            let unseenTaggedCount = if readyState.UnseenTaggedCount > 0 then Some readyState.UnseenTaggedCount else None
            unseenCount, unseenTaggedCount
    let extra = // TODO-NMB: Think about how best to display unseenCount / unseenTaggedCount...
        match unseenCount, unseenTaggedCount with
        | Some unseenCount, Some unseenTaggedCount -> sprintf " (%i) [%i]" unseenCount unseenTaggedCount
        | Some unseenCount, None -> sprintf " (%i)" unseenCount
        | None, Some _ -> String.Empty // should never happen
        | _ -> String.Empty
    sprintf "%s%s" PAGE_TITLE extra

let renderTab isActive onClick state = // TODO-NMB: Think about how best to display unseenCount / unseenTaggedCount (but just use pageTitle for now)...
    tab isActive [ linkInternal onClick [ str (state |> pageTitle) ] ]

let render theme (authUser:AuthUser) (usersData:RemoteData<UserData list, string>) (state:State) (_:int<tick>) (dispatch:Input -> unit) =
    divDefault [
        // Note: Render Modals (if any) first so will not be affected by contentCentred.
        match state with
        | Ready(_, readyState) when readyState.ShowingMarkdownSyntaxModal ->
            match usersData with
            | Received(users, _) -> yield lazyView2 renderMarkdownSyntaxModal (theme, users) dispatch
            | _ -> ()
        | _ -> ()
        yield columnsDefault [ contentCentred None [
            yield paraSmall [ strong "Chat" ]
            yield hrT theme false
            match usersData with
            | Pending -> yield contentCentred None [ divVerticalSpace 15 ; iconLarge ICON__SPINNER_PULSE ]
            | Received(users, _) ->
                match state with
                | ReadingLastTimestampSeen _ -> yield contentTCentred theme None (Some IsLink) [ divVerticalSpace 15 ; iconLarge ICON__SPINNER_PULSE ]
                | Ready(pageState, readyState) -> // TODO-NMB...
                    let userTags =
                        users
                        |> List.filter (fun (user, _, _) -> user.UserType <> PersonaNonGrata)
                        |> List.sortBy (fun (user, signedIn, lastActivity) ->
                            let order =
                                match user, signedIn, lastActivity, authUser.User.UserId with
                                | Self -> 1 | RecentlyActive -> 2 | SignedIn -> 3 | NotSignedIn -> 4 | PersonaNonGrata -> 5
                            order, user.UserName)
                        |> List.map (tagTUserSmall theme authUser.User.UserId)
                    yield renderWarningMessage theme "Chat functionality is a work in progress..." // TEMP-NMB...
                    yield divTags userTags
                    yield hrT theme false
                    yield contentRightSmallest [ linkInternal (fun _ -> dispatch ShowMarkdownSyntaxModal) [ str "Markdown syntax" ] ] // TEMP-NMB...

            | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "Users RemoteData Failed -> %s" error) UNEXPECTED_ERROR)
            yield divVerticalSpace 5 ] ] ]
