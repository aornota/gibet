module Aornota.Gibet.Ui.Pages.Chat.Render

open Aornota.Gibet.Common.Domain.Chat
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

let private renderMarkdownSyntaxModal (theme, users:UserData list) dispatch = // TODO-NMB: @username / @{user name} example [and processing]?...
    let title = [ contentCentred None [ paraSmall [ str "Markdown syntax" ] ] ]
    let onDismiss = Some(fun _ -> dispatch CloseMarkdownSyntaxModal)
    let (Markdown markdownSyntax) = Markdown MARKDOWN_SYNTAX
    let body = [
        contentTCentred theme (Some smaller) (Some IsInfo) [ strong "As a very quick introduction to Markdown syntax, the following:" ]
        textAreaT theme markdownSyntaxKey markdownSyntax None None false true ignore
        br
        contentTCentred theme (Some smaller) (Some IsInfo) [ strong "will appear as:" ]
        markdownContentTLeft theme (Markdown markdownSyntax) ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderNewChatMessage (theme, users:UserData list, state, hasModal) dispatch = // TODO-NMB: @username / @{user name} processing...
    let newChatMessage = state.NewChatMessage
    let sendingChatMessage, sendChatMessageInteraction, newChatMessageStatus =
        match state.SendChatMessageApiStatus with
        | Some ApiPending -> true, Loading, None
        | _ ->
            let newChatMessageError = validateChatMessage (Markdown newChatMessage)
            let sendChatMessageInteraction =
                match newChatMessageError with
                | Some _ -> NotEnabled
                | _ -> Clickable(fun _ -> dispatch SendChatMessage)
            let newChatMessageStatus =
                match state.NewChatMessageChanged, newChatMessageError with
                | true, Some error -> Some(IsDanger, String.Empty, helpTDanger theme [ str error ])
                | _ -> None
            false, sendChatMessageInteraction, newChatMessageStatus
    // TODO-NMB: extraInfo - and use CHAT_MESSAGE_LIFETIME?...
    let extraInfo = None
    (* let extraInfo = [
        str "Chat messages are not persisted and will only be received by signed-in users. You can use "
        [ str "Markdown syntax" ] |> link theme (ClickableLink (fun _ -> ShowMarkdownSyntaxModal |> dispatch))
        str " to format your message. A preview of your message will appear below." ; br; br ] *)
    divDefault [
        match state.SendChatMessageApiStatus with
        | Some(ApiFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to send chat message" ]
                contentLeftSmallest [ str error ] ]
            yield br
        | _ -> ()
        yield fieldFullWidth [
            textAreaT theme state.NewChatMessageKey newChatMessage newChatMessageStatus extraInfo (not hasModal) sendingChatMessage (NewChatMessageChanged >> dispatch) ]
        if not (String.IsNullOrWhiteSpace newChatMessage) then
            yield! [ notificationT theme IsPrimary None [ markdownNotificationContentTLeft theme (Markdown newChatMessage) ] ; br ]
        yield fieldGroupedRight [ buttonTSmall theme IsLink sendChatMessageInteraction [ str "Send chat message" ] ] ]

let private renderUserTags (theme, authUser, users:UserData list, _:int<tick>) =
    let userTags =
        users
        |> List.filter (fun (user, _, _) -> user.UserType <> PersonaNonGrata)
        |> List.sortBy (fun (user, signedIn, lastActivity) ->
            let order = match user, signedIn, lastActivity, authUser.User.UserId with | Self -> 1 | RecentlyActive -> 2 | SignedIn -> 3 | NotSignedIn -> 4 | PersonaNonGrata -> 5
            order, user.UserName)
        |> List.map (tagTUserSmall theme authUser.User.UserId)
    divTags userTags

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

let render theme authUser usersData hasModal state (ticks:int<tick>) dispatch =
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
                | Ready(_, readyState) ->
                    yield renderWarningMessage theme "Chat functionality is a work in progress..." // TEMP-NMB...
                    yield lazyView2 renderNewChatMessage (theme, users, readyState, hasModal) dispatch
                    // TEMP-NMB...
                    yield br
                    yield contentRightSmallest [ linkInternal (fun _ -> dispatch ShowMarkdownSyntaxModal) [ str "Markdown syntax" ] ]
                    // ...TEMP-NMB
                    yield hrT theme false
                    yield lazyView renderUserTags (theme, authUser, users, ticks)
                    // TODO-NMB...
            | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "Users RemoteData Failed -> %s" error) UNEXPECTED_ERROR)
            yield divVerticalSpace 5 ] ] ]
