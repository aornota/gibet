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
open System.Text

open Fable.React.Helpers

open Fulma

let [<Literal>] private PAGE_TITLE = "Chat"

let [<Literal>] private NO_LONGER_EXISTS = "The chat message no longer exists; it has probably been deleted by another connection"

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

let private userImage user = match user.ImageUrl with | Some(ImageUrl imageUrl) -> Some(image imageUrl Image.Is48x48) | None -> None
let private tryUserImage userId users = match users |> tryFindUser userId with | Some(user, _, _) -> userImage user | None -> None

let private userTagOrDefault theme authUser (userId, UserName userName) (users:UserData list) =
    match users |> tryFindUser userId with
    | Some(user, signedIn, lastActivity) -> tagTUserSmall theme authUser.User.UserId (user, signedIn, lastActivity)
    | None -> strong userName

let private tagged theme users taggedUsers =
    match taggedUsers |> List.choose (fun userId -> users |> tryFindUser userId) with
    | [ (user, _, _) ] ->
        let (UserName userName) = user.UserName
        let taggedTooltip = tooltip TooltipRight IsInfo (sprintf "%s will be tagged in this chat message" userName)
        Some(iconTTooltipSmall theme ICON__INFO taggedTooltip)
    | h :: t ->
        let taggedUsers = h :: t
        let count = taggedUsers.Length
        let folder (builder:StringBuilder, i) (user, _, _) =
            let (UserName userName) = user.UserName
            let builder =
                if i = 1 then builder.Append(userName)
                else if i = count && count = 2 then builder.Append(sprintf " and %s" userName)
                else if i = count then builder.Append(sprintf ", and %s" userName)
                else builder.Append(sprintf ", %s" userName)
            builder, i + 1
        let builder, _ = taggedUsers |> List.fold folder (StringBuilder(), 1)
        let taggedTooltip = tooltip TooltipRight IsInfo (sprintf "%s will be tagged in this chat message" (builder.ToString()))
        Some(iconTTooltipSmall theme ICON__INFO taggedTooltip)
    | _ -> None
let private selfTagged theme authUserId taggedUsers =
    if taggedUsers |> List.contains authUserId then
        let taggedTooltip = tooltip TooltipRight IsInfo "You have been tagged in this chat message"
        Some(iconTTooltipSmall theme ICON__INFO taggedTooltip)
    else None

let private extraInfo theme forEdit dispatch =
    let expiresAfter =
        if chatMessageLifetime < 1.<hour> then
            let chatMessageLifetime = hoursToMinutes chatMessageLifetime
            sprintf "%.0f minutes" (floor (float chatMessageLifetime))
        else sprintf "%.0f hours" (floor (float chatMessageLifetime))
    Some(helpTInfo theme [
        if not forEdit then yield str (sprintf "Chat messages are not persisted, will only be received by signed in users, and will expire after %s." expiresAfter)
        yield str " You can use "
        yield linkInternal (fun _ -> dispatch ShowMarkdownSyntaxModal) [ str "Markdown syntax" ]
        yield str " to format your message;"
        yield! [ str " you can also use " ; strong "@" ; strongEm "username" ; str " to tag users" ]
        yield! [ str " (or " ; strong "@{" ; strongEm "username" ; strong "}" ; str " if " ; em "username" ; str " contains spaces)." ]
        yield str " A preview of your message will appear below." ])

let private renderEditChatMessageModal (theme, authUser, users, chatMessages, editChatMessageModalState:EditChatMessageModalState, hasModal) dispatch =
    let title = [ contentCentred None [ paraSmall [ str "Edit chat message" ] ] ]
    let newChatMessage = editChatMessageModalState.NewChatMessage
    let onDismiss, body =
        let onDismiss = (fun _ -> dispatch(EditChatMessageModalInput CloseEditChatMessageModal))
        let chatMessageId, rvn = editChatMessageModalState.ForChatMessage
        match chatMessages |> tryFindChatMessage chatMessageId with
        | Some(chatMessage, _, status) ->
            let onDismiss, editingChatMessage, editChatMessageInteraction, newChatMessageStatus, expired =
                match editChatMessageModalState.EditChatMessageApiStatus with
                | Some ApiPending -> None, true, Loading, None, []
                | _ ->
                    let newChatMessageError = validateChatMessage (Markdown newChatMessage)
                    let expired = expired status
                    let (Markdown currentChatMessage) = chatMessage.Payload
                    let editChatMessageInteraction =
                        match newChatMessageError, expired, newChatMessage <> currentChatMessage with
                        | None, false, true -> Clickable(fun _ -> dispatch(EditChatMessageModalInput EditChatMessage))
                        | _ -> NotEnabled
                    let newChatMessageStatus =
                        match editChatMessageModalState.NewChatMessageChanged, newChatMessageError with
                        | true, Some error -> Some(IsDanger, helpTDanger theme [ str error ])
                        | _ -> None
                    let expired =
                        if expired then [
                            notificationT theme IsWarning None [ contentCentredSmaller [ str "The chat message has now expired and can no longer be edited" ] ]
                            br ]
                        else []
                    Some onDismiss, false, editChatMessageInteraction, newChatMessageStatus, expired
            let extraInfo = extraInfo theme true dispatch
            let Markdown newChatMessage, taggedUsers = users |> processTags (Markdown newChatMessage)
            let userImage = userImage authUser.User
            let userTag = users |> userTagOrDefault theme authUser (authUser.User.UserId, authUser.User.UserName)
            let tagged = taggedUsers |> tagged theme users
            let body = [
                if chatMessage.Rvn <> rvn then
                    yield notificationT theme IsWarning None [ contentCentredSmaller [ str "This chat message has been modified by another connection" ] ]
                    yield br
                match editChatMessageModalState.EditChatMessageApiStatus with
                | Some(ApiFailed error) ->
                    yield notificationT theme IsDanger None [
                        contentCentredSmaller [ str "Unable to edit chat message" ]
                        contentLeftSmallest [ str error ] ]
                    yield br
                | _ -> ()
                yield! expired
                yield fieldDefault [
                    textAreaT theme editChatMessageModalState.NewChatMessageKey newChatMessage newChatMessageStatus extraInfo (not hasModal) editingChatMessage
                        (EditChatMessageModalInput.NewChatMessageChanged >> EditChatMessageModalInput >> dispatch) ]
                if not (String.IsNullOrWhiteSpace newChatMessage) then
                    yield notificationT theme IsBlack None [
                        level false [ levelLeft [ levelItem [ contentLeftSmallest [ ofOption userImage ; userTag ; ofOption tagged ] ] ] ]
                        markdownNotificationContentTLeft theme (Markdown newChatMessage) ]
                    yield br
                yield fieldGroupedCentred [ buttonTSmall theme IsLink editChatMessageInteraction [ str "Edit chat message" ] ] ]
            onDismiss, body
        | None ->
            let body = [ notificationT theme IsWarning None [
                contentCentredSmaller [str "Unable to edit chat message" ]
                contentLeftSmallest [ str (ifDebug (sprintf "%s (%A not found in chat messages)" NO_LONGER_EXISTS chatMessageId) NO_LONGER_EXISTS) ] ] ]
            Some onDismiss, body
    cardModalT theme (Some(title, onDismiss)) body

let private renderDeleteChatMessageModal (theme, authUser, users, chatMessages, deleteChatMessageModalState:DeleteChatMessageModalState) dispatch =
    let title = [ contentCentred None [ paraSmall [ str "Delete chat message" ] ] ]
    let onDismiss, body =
        let onDismiss = (fun _ -> dispatch CloseDeleteChatMessageModal)
        let chatMessageId, rvn = deleteChatMessageModalState.ForChatMessage
        match chatMessages |> tryFindChatMessage chatMessageId with
        | Some(chatMessage, _, status) ->
            let onDismiss, deleteChatMessageInteraction, expired =
                match deleteChatMessageModalState.DeleteChatMessageApiStatus with
                | Some ApiPending -> None, Loading, None
                | _ ->
                    let deleteChatMessageInteraction = Clickable(fun _ -> dispatch DeleteChatMessage)
                    let expired = if expired status then Some(contentLeftSmallest [ str "Please also note that the chat message has now expired" ]) else None
                    Some onDismiss, deleteChatMessageInteraction, expired
            let authUserId = authUser.User.UserId
            let userId, userName = chatMessage.Sender
            let userImage = users |> tryUserImage userId
            let userTag = users |> userTagOrDefault theme authUser (userId, userName)
            let tagged = chatMessage.TaggedUsers |> selfTagged theme authUserId
            let body = [
                if chatMessage.Rvn <> rvn then
                    yield notificationT theme IsWarning None [ contentCentredSmaller [ str "This chat message has been modified by another connection" ] ]
                    yield br
                yield notificationT theme IsWarning None [
                    contentCentredSmaller [ strong "Are you sure that you want to delete this chat message?" ]
                    contentLeftSmallest [ str "Please note that this action is irreversible." ]
                    ofOption expired ]
                match deleteChatMessageModalState.DeleteChatMessageApiStatus with
                | Some(ApiFailed error) ->
                    yield notificationT theme IsDanger None [
                        contentCentredSmaller [ str "Unable to delete chat message" ]
                        contentLeftSmallest [ str error ] ]
                    yield br
                | _ -> ()
                yield br
                yield notificationT theme IsLight None [
                    level false [ levelLeft [ levelItem [ contentLeftSmallest [ ofOption userImage ; userTag ; ofOption tagged ] ] ] ]
                    markdownNotificationContentTLeft theme chatMessage.Payload ]
                yield br
                yield fieldGroupedCentred [ buttonTSmall theme IsLink deleteChatMessageInteraction [ str "Delete chat message" ] ] ]
            onDismiss, body
        | None ->
            let body = [ notificationT theme IsWarning None [
                contentCentredSmaller [str "Unable to delete chat message" ]
                contentLeftSmallest [ str (ifDebug (sprintf "%s (%A not found in chat messages)" NO_LONGER_EXISTS chatMessageId) NO_LONGER_EXISTS) ] ] ]
            Some onDismiss, body
    cardModalT theme (Some(title, onDismiss)) body

let private renderNewChatMessage (theme, authUser, users, readyState, hasModal) dispatch =
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
    let extraInfo = extraInfo theme false dispatch
    let Markdown newChatMessage, taggedUsers = users |> processTags (Markdown newChatMessage)
    let userImage = userImage authUser.User
    let userTag = users |> userTagOrDefault theme authUser (authUser.User.UserId, authUser.User.UserName)
    let tagged = taggedUsers |> tagged theme users
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
            yield notificationT theme IsBlack None [
                level false [ levelLeft [ levelItem [ contentLeftSmallest [ ofOption userImage ; userTag ; ofOption tagged ] ] ] ]
                markdownNotificationContentTLeft theme (Markdown newChatMessage) ]
            yield br
        yield fieldGroupedCentred [ buttonTSmall theme IsLink sendChatMessageInteraction [ str "Send chat message" ] ] ]

let private renderUserTags (theme, authUser, users:UserData list, _:int<tick>) =
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
    let renderChatMessage (chatMessage:ChatMessage, timestamp:DateTimeOffset, status) =
        let chatMessageId, authUserId = chatMessage.ChatMessageId, authUser.User.UserId
        let userId, userName = chatMessage.Sender
        let userImage = users |> tryUserImage userId
        let userTag = users |> userTagOrDefault theme authUser (userId, userName)
        let tagged = chatMessage.TaggedUsers |> selfTagged theme authUserId
        let expired, colour, textColour = match status with | MessageReceived _ -> false, IsLight, IsBlack | MessageExpired -> true, IsDark, IsWhite
        let onDismiss = if expired then Some(fun _ -> dispatch (RemoveExpiredChatMessage chatMessageId)) else None
        let tools =
            if not expired then
                let userType = authUser.User.UserType
                let edit =
                    if canEditChatMessage userId (authUserId, userType) then
                        Some(contentLeftSmallest [ linkInternal (fun _ -> dispatch (ShowEditChatMessageModal chatMessageId)) [ str "Edit" ] ])
                    else None
                let delete =
                    if canDeleteChatMessage userId (authUserId, userType) then
                        Some(contentRightSmallest [ linkInternal (fun _ -> dispatch (ShowDeleteChatMessageModal chatMessageId)) [ str "Delete" ] ])
                    else None
                match edit, delete with
                | Some edit, Some delete -> Some(level false [ levelLeft [ levelItem [ edit ] ] ; levelRight [ levelItem [ delete ] ] ])
                | Some edit, None -> Some(level false [ levelLeft [ levelItem [ edit ] ] ; levelRight [ levelItem [] ] ])
                | None, Some delete -> Some(level false [ levelLeft [ levelItem [] ] ; levelRight [ levelItem [ delete ] ] ])
                | _ -> None
            else None
        let timestamp =
            if expired then "expired"
            else
#if TICK
                ago timestamp.LocalDateTime
#else
                dateAndTimeText timestamp.LocalDateTime
#endif
        let edited = if chatMessage.Edited then Some(paraTSmallest theme IsInfo [ strong " edited" ]) else None
        [
            notificationT theme colour onDismiss [ contentTCentredSmallest theme (Some textColour) [
                level false [
                    levelLeft [ levelItem [ contentLeftSmallest [ ofOption userImage ; userTag ; ofOption tagged ] ] ]
                    levelRight [ levelItem [ contentRightSmallest [ str timestamp ; ofOption edited ] ] ] ]
                markdownNotificationContentTLeft theme chatMessage.Payload
                ofOption tools ] ]
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
    // TODO-NMB: Need separate hasModal values for different contexts?...
    let hasModal =
        if hasModal then true
        else
            match state with
            | Ready(_, readyState) ->
                if readyState.ShowingMarkdownSyntaxModal then true
                else
                    match readyState.EditChatMessageModalState, readyState.DeleteChatMessageModalState with
                    | Some _, _ -> true
                    | _, Some _ -> true
                    | _ -> false
            | _ -> false
    divDefault [
        // Note: Render Modals (if any) first so will not be affected by contentCentred.
        match usersData, state with
        | Received(users, _), Ready(_, readyState) ->
            if readyState.ShowingMarkdownSyntaxModal then yield lazyView2 renderMarkdownSyntaxModal (theme, users) dispatch
            else
                match readyState.ChatMessagesData, readyState.EditChatMessageModalState, readyState.DeleteChatMessageModalState with
                | Received((chatMessages, _), _), Some editChatMessageModalState, _ ->
                    yield lazyView2 renderEditChatMessageModal (theme, authUser, users, chatMessages, editChatMessageModalState, hasModal) dispatch // note: *not* (EditChatMessageModalInput >> dispatch)
                | Received((chatMessages, _), _), _, Some deleteChatMessageModalState ->
                    yield lazyView2 renderDeleteChatMessageModal (theme, authUser, users, chatMessages, deleteChatMessageModalState) (DeleteChatMessageModalInput >> dispatch)
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
                        yield lazyView2 renderNewChatMessage (theme, authUser, users, readyState, hasModal) dispatch
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
