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
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.MarkdownLiterals
open Aornota.Gibet.Ui.Pages.Chat.State
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System

open Fable.React.Helpers

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

let private renderNewChatMessage (theme, users:UserData list, readyState, hasModal) dispatch = // TODO-NMB: @username / @{user name} processing...
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
            str (sprintf "Chat messages are not persisted, will only be received by signed in users, and will expire after %s. You can use " expiresAfter)
            linkInternal (fun _ -> dispatch ShowMarkdownSyntaxModal) [ str "Markdown syntax" ]
            str " to format your message. A preview of your message will appear below." ])
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

let private renderChatMessages (theme, authUser, users, chatMessages, _:int<tick>) dispatch = // TODO-NMB: See inside...
    // #region renderChatMessage
    let renderChatMessage (chatMessage, timestamp:DateTimeOffset, status) = // TODO-NMB: TaggedUsers...
        let authUserId = authUser.User.UserId
        let userId, UserName userName = chatMessage.Sender
        let colour, textColour =
            match status with
            | MessageReceived _ ->
                match users |> tryFindUser userId with
                | Some(user, signedIn, lastActivity) ->
                    match user, signedIn, lastActivity, authUserId with
                    | Self -> IsLink, IsBlack
                    | RecentlyActive -> IsSuccess, IsBlack
                    | SignedIn -> IsPrimary, IsBlack
                    | NotSignedIn -> IsDark, IsWhite
                    | PersonaNonGrata -> IsLight, IsBlack
                | None -> IsDanger, IsBlack // should never happen
            | MessageExpired -> IsLight, IsBlack
        let expired, onDismiss =
            match status with
            | MessageReceived _ -> false, None
            | MessageExpired -> true, Some(fun _ -> dispatch (RemoveChatMessage chatMessage.ChatMessageId))
        let timestamp =
            if expired then "expired"
            else
#if TICK
                ago timestamp.LocalDateTime
#else
                dateAndTimeText timestamp.LocalDateTime
#endif
        [
            notificationT theme colour onDismiss [
                level false [
                    levelLeft [ levelItem [ contentTLeftSmallest theme (Some textColour) [ strong userName ; str " says" ] ] ]
                    levelRight [ levelItem [ contentTRightSmallest theme None [ str timestamp ] ] ] ]
                markdownNotificationContentTLeft theme chatMessage.Payload ]
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
