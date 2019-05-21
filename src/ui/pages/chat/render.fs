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
open Aornota.Gibet.Ui.Common.TimestampHelper
open Aornota.Gibet.Ui.Common.Tooltip
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.MarkdownLiterals
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System

open Fable.React.Helpers

open Fulma

let [<Literal>] private PAGE_TITLE = "Chat"

let private markdownSyntaxKey = Guid.NewGuid()

let private renderMarkdownSyntaxModal (theme, users) dispatch =
    let title = [ contentCentred None [ paraSmall [ str "Markdown syntax" ] ] ]
    let onDismiss = Some(fun _ -> dispatch CloseMarkdownSyntaxModal)
    let markdownSyntax = MARKDOWN_SYNTAX.Replace("EXAMPLE_ADMIN_USER_NAME", EXAMPLE_ADMIN_USER_NAME)
    let processed, _ = users |> processTags (Markdown markdownSyntax)
    let body = [
        contentTCentred theme (Some smaller) (Some IsInfo) [ strong "As a very quick introduction to Markdown syntax, the following:" ]
        textAreaT theme markdownSyntaxKey markdownSyntax None None false true ignore
        br
        contentTCentred theme (Some smaller) (Some IsInfo) [ strong "will appear as:" ]
        markdownContentTLeft theme processed ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderNewChatMessage (theme, users, readyState, hasModal) dispatch =
    let newChatMessage = readyState.NewChatMessage
    let sendingChatMessage, sendChatMessageInteraction, newChatMessageStatus =
        match readyState.SendChatMessageApiStatus with
        | Some ApiPending -> true, Loading, None
        | _ ->
            let newChatMessageError = validateChatMessage (Markdown newChatMessage)
            let sendChatMessageInteraction =
                match newChatMessageError with
                | Some _ -> NotEnabled
                | _ -> Clickable(fun _ -> dispatch SendChatMessage)
            let newChatMessageStatus =
                match readyState.NewChatMessageChanged, newChatMessageError with
                | true, Some error -> Some(IsDanger, helpTDanger theme [ str error ])
                | _ -> None
            false, sendChatMessageInteraction, newChatMessageStatus
    let extraInfo =
        let expiresAfter =
            if chatMessageLifetime < 1.<hour> then
                let chatMessageLifetime = hoursToMinutes chatMessageLifetime
                sprintf "%.0f minutes" (floor (float chatMessageLifetime))
            else sprintf "%.0f hours" (floor (float chatMessageLifetime))
        Some(helpTInfo theme [
            str (sprintf "Chat messages are not persisted, will only be received by signed in users, and will expire after %s." expiresAfter)
            str " You can use "
            linkInternal (fun _ -> dispatch ShowMarkdownSyntaxModal) [ str "Markdown syntax" ]
            str " to format your message;"
            str " you can also use " ; strong "@" ; strongEm "username" ; str " to tag users"
            str " (or " ; strong "@{" ; strongEm "username" ; strong "}" ; str " if " ; em "username" ; str " contains spaces)."
            str " A preview of your message will appear below." ])
    let Markdown newChatMessage, _ = users |> processTags (Markdown newChatMessage)
    divDefault [
        match readyState.SendChatMessageApiStatus with
        | Some(ApiFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to send chat message" ]
                contentLeftSmallest [ str error ] ]
            yield br
        | _ -> ()
        yield fieldDefault [
            textAreaT theme readyState.NewChatMessageKey newChatMessage newChatMessageStatus extraInfo (not hasModal) sendingChatMessage (NewChatMessageChanged >> dispatch) ]
        if not (String.IsNullOrWhiteSpace newChatMessage) then
            yield! [ notificationT theme IsLink None [ markdownNotificationContentTLeft theme (Markdown newChatMessage) ] ; br ]
        yield fieldGroupedRight [ buttonTSmall theme IsLink sendChatMessageInteraction [ str "Send chat message" ] ] ]

let private renderUserTags (theme, authUser, users, _:int<tick>) =
    let userTags =
        users
        |> List.filter (fun (user, _, _) -> user.UserType <> PersonaNonGrata)
        |> List.sortBy (fun (user, signedIn, lastActivity) ->
            let order = match user, signedIn, lastActivity, authUser.User.UserId with | Self -> 1 | RecentlyActive -> 2 | SignedIn -> 3 | NotSignedIn -> 4 | PersonaNonGrata -> 5
            order, user.UserName)
        |> List.map (tagTUserSmall theme authUser.User.UserId)
    divTags userTags

let private renderChatMessages (theme, authUser, users, chatMessages, _:int<tick>) dispatch =
    // #region renderChatMessage
    let renderChatMessage (chatMessage, timestamp:DateTimeOffset, status) =
        let authUserId = authUser.User.UserId
        let userId, UserName userName = chatMessage.Sender
        let tagged =
            if userId <> authUserId && chatMessage.TaggedUsers |> List.contains authUserId then
                let taggedTooltip = tooltip TooltipRight IsInfo "You have been tagged in this chat message"
                Some(iconTTooltipSmall theme ICON__INFO taggedTooltip)
            else None
        let expired, colour, textColour =
            match status with
            | MessageReceived _ ->
                let colour, textColour =
                    match users |> tryFindUser userId with
                    | Some(user, signedIn, lastActivity) ->
                        match user, signedIn, lastActivity, authUserId with
                        | Self -> IsLink, IsBlack
                        | RecentlyActive -> IsSuccess, IsBlack
                        | SignedIn -> IsPrimary, IsBlack
                        | NotSignedIn -> IsDark, IsWhite
                        | PersonaNonGrata -> IsLight, IsBlack
                    | None -> IsDanger, IsBlack // should never happen
                false, colour, textColour
            | MessageExpired -> true, IsLight, IsBlack
        let onDismiss = if expired then Some(fun _ -> dispatch (RemoveChatMessage chatMessage.ChatMessageId)) else None
        let timestamp =
            if expired then "expired"
            else
#if TICK
                ago timestamp.LocalDateTime
#else
                dateAndTimeText timestamp.LocalDateTime
#endif
        [
            notificationT theme colour onDismiss [ contentTCentredSmallest theme (Some textColour) [
                level false [
                    levelLeft [ levelItem [ contentLeftSmallest [ strong userName ; str " says" ; ofOption tagged ] ] ]
                    levelRight [ levelItem [ contentRightSmallest [ str timestamp ] ] ] ]
                markdownNotificationContentTLeft theme chatMessage.Payload ] ]
            divVerticalSpace 10
        ]
    // #endregion
    let chatMessages =
        chatMessages
        |> List.sortBy (fun (_, timestamp, _) -> timestamp)
        |> List.rev
        |> List.map renderChatMessage
        |> List.collect id
    divDefault [ yield! chatMessages ]

let private moreChatMessages theme authUser chatMessages count apiStatus dispatch =
    if canGetChatMessages authUser.User.UserType then
        let receivedOrdinals = chatMessages |> List.choose (fun (_, _, status) -> match status with | MessageReceived ordinal -> Some ordinal | MessageExpired -> None)
        let receivedCount = receivedOrdinals.Length
        let moreCount = count - receivedCount
        if receivedCount > 0 && moreCount > 0 then
            let minOrdinal = receivedOrdinals |> List.min
            let extra =
                match queryBatchSize with
                | Some queryBatchSize ->
                    if moreCount > queryBatchSize then sprintf " (next %i of %i)" queryBatchSize moreCount
                    else sprintf " (next %i of %i)" moreCount moreCount
                | None -> String.Empty // should never happen (i.e. because will have received all chat messages from initial getChatMessages call)
            let moreChatMessages = linkInternal (fun _ -> dispatch (MoreChatMessages minOrdinal)) [ str (sprintf "More chat messages%s" extra) ]
            let moreContent =
                match apiStatus with
                | Some ApiPending -> contentRightSmallest [ str "Retrieving more chat messages... " ; iconSmaller ICON__SPINNER_PULSE ]
                | Some(ApiFailed error) -> contentRightSmallest [ paraTSmallest theme IsDanger [ str error ] ; paraSmallest [ moreChatMessages ] ]
                | None -> contentRightSmallest [ moreChatMessages ]
            Some moreContent
        else None
    else None

let private nonZeroUnseenCounts state =
    match state with
    | ReadingLatestChatSeen _ -> None, None
    | Ready(_, readyState) ->
        let unseenCount, unseenTaggedCount = readyState.UnseenCount, readyState.UnseenTaggedCount
        (if unseenCount > 0 then Some unseenCount else None), (if unseenTaggedCount > 0 then Some unseenTaggedCount else None)

let pageTitle state =
    let extra =
        match state |> nonZeroUnseenCounts with
        | Some _, Some _ -> "** "
        | Some _, None -> "* "
        | None, Some _ -> "* " // should never happen
        | _ -> String.Empty
    sprintf "%s%s" extra PAGE_TITLE

let renderTab isActive onClick state =
    let unseenTaggedExtra unseenTaggedCount = [ iconSmaller ICON__INFO ; str (sprintf "%i" unseenTaggedCount) ]
    let unseenExtra, unseenTaggedExtra =
        match state |> nonZeroUnseenCounts with
        | Some unseenCount, Some unseenTaggedCount -> sprintf " (%i)" unseenCount, unseenTaggedExtra unseenTaggedCount
        | Some unseenCount, None -> sprintf " (%i)" unseenCount, []
        | None, Some unseenTaggedCount -> SPACE, unseenTaggedExtra unseenTaggedCount // should never happen
        | _ -> String.Empty, []
    tab isActive [ linkInternal onClick [ yield str (sprintf "%s%s" PAGE_TITLE unseenExtra) ; yield! unseenTaggedExtra ] ]

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
                | ReadingLatestChatSeen _ -> yield contentTCentred theme None (Some IsLink) [ divVerticalSpace 15 ; iconLarge ICON__SPINNER_PULSE ]
                | Ready(_, readyState) ->
                    if canSendChatMessage authUser.User.UserType then
                        yield lazyView2 renderNewChatMessage (theme, users, readyState, hasModal) dispatch
                        yield hrT theme false
                    yield lazyView renderUserTags (theme, authUser, users, ticks)
                    match readyState.ChatMessagesData with
                    | Pending -> yield contentCentred None [ divVerticalSpace 15 ; iconSmall ICON__SPINNER_PULSE ]
                    | Received((chatMessages, count), _) ->
                        yield lazyView2 renderChatMessages (theme, authUser, users, chatMessages, ticks) dispatch
                        yield ofOption (moreChatMessages theme authUser chatMessages count readyState.MoreChatMessagesApiStatus dispatch)
                    | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "ChatMessages RemoteData Failed -> %s" error) UNEXPECTED_ERROR)
            | Failed error -> yield renderDangerMessage theme (ifDebug (sprintf "Users RemoteData Failed -> %s" error) UNEXPECTED_ERROR)
            yield divVerticalSpace 5 ] ] ]
