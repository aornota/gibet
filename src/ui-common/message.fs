module Aornota.Gibet.Ui.Common.Message

open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Render.Theme
open Aornota.Gibet.Ui.Common.TimestampHelper

open System

open Fable.React

open Fulma

type MessageId = | MessageId of Guid with static member Create() = MessageId(Guid.NewGuid())

type MessageType = | Debug | Info | Warning | Danger

type Message = {
    MessageId : MessageId
    MessageType : MessageType
    Children : ReactElement list
    Timestamp : DateTime
    Dismissable : bool }

// #region render
let private render theme source dispatch message =
    let colour, textColour = match message.MessageType with | Debug -> IsDark, IsWhite | Info -> IsInfo, IsBlack | Warning -> IsWarning, IsBlack | Danger -> IsDanger, IsBlack
    let onDismiss = if message.Dismissable then Some(fun _ -> message.MessageId |> dispatch) else None
    let sourceAndType = sprintf "%s | %s" source (match message.MessageType with | Debug -> "DEBUG" | Info -> "INFORMATION" | Warning -> "WARNING" | Danger -> "ERROR")
    let timestamp =
        if message.Dismissable then
            let timestamp =
#if TICK
                ago message.Timestamp
#else
                dateAndTimeText message.Timestamp
#endif
            Some(levelRight [ levelItem [ contentRight [ paraT theme TextSize.Is7 textColour TextWeight.Normal [ str timestamp ] ] ] ])
        else None
    [
        divVerticalSpace 10
        notificationT theme colour onDismiss [
            level true [
                levelLeft [ levelItem [ contentLeft [ paraT theme TextSize.Is7 textColour TextWeight.Bold [ str sourceAndType ] ] ] ]
                ofOption timestamp ]
            contentLeft message.Children ]
    ]
// #endregion

// #region shouldRender
let private shouldRender (message:Message) =
#if DEBUG
    true
#else
    match message.MessageType with | Debug -> false | Info | Warning | Danger -> true
#endif
// #endregion

let message messageType children timestamp dismissable = {
    MessageId = MessageId.Create ()
    MessageType = messageType
    Children = children
    Timestamp = timestamp
    Dismissable = dismissable }

let debugMessage children dismissable = message Debug children DateTime.Now dismissable
let debugMessageDismissable children = message Debug children DateTime.Now true
let infoMessage children dismissable = message Info children DateTime.Now dismissable
let infoMessageDismissable children = message Info children DateTime.Now true
let warningMessage children dismissable = message Warning children DateTime.Now dismissable
let warningMessageDismissable children = message Warning children DateTime.Now true
let dangerMessage children dismissable = message Danger children DateTime.Now dismissable
let dangerMessageDismissable children = message Danger children DateTime.Now true

let removeMessage messageId messages = messages |> List.filter (fun message -> message.MessageId <> messageId)

let renderMessageSpecial (theme, source, message, _:int<tick>) =
    if shouldRender message then columnContent (render theme source ignore { message with Dismissable = false })
    else divEmpty

let renderMessages (theme, source, messages, _:int<tick>) dispatch =
    match messages |> List.filter shouldRender with
    | _ :: _ ->
        columnContent [
            yield divVerticalSpace 15
            yield! messages
                |> List.sortBy (fun message -> match message.MessageType with | Debug -> 0 | Info -> 3 | Warning -> 2 | Danger -> 1)
                |> List.map (render theme source dispatch)
                |> List.collect id ]
    | [] -> divEmpty
