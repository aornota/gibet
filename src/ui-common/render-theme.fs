module Aornota.Gibet.Ui.Common.Render.Theme

open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Common.Tooltip

open System

open Browser.Types

open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props

open Fulma
open Fulma.Extensions.Wikiki

type private LinkType =
    | Internal of (MouseEvent -> unit)
    | NewWindow of string
    | DownloadFile of url : string * fileName : string

type ButtonInteraction =
    | Clickable of (MouseEvent -> unit)
    | Loading
    | Static
    | NotEnabled

let [<Literal>] private SPACE = " "

let private delete onClick = Delete.delete [ Delete.OnClick onClick ] []

let buttonT theme size colour interaction outlined inverted tooltip children =
    let tooltip = tooltip |> Option.map (fun tooltip -> { tooltip with TooltipColour = tooltip.TooltipColour |> transformColour theme })
    Button.button [
        match size with | Some size -> yield Button.Size size | None -> ()
        yield Button.Color (colour |> transformColour theme)
        match interaction with
        | Clickable onClick -> yield Button.OnClick onClick
        | Loading -> yield Button.IsLoading true
        | Static -> yield Button.IsStatic true
        | NotEnabled -> ()
        if outlined then yield Button.IsOutlined
        if inverted then yield Button.IsInverted
        match tooltip with | Some tooltip -> yield Button.CustomClass (tooltipClass tooltip) | None -> ()
        yield Button.Props [
            yield Disabled (match interaction with | NotEnabled -> true | _ -> false)
            match tooltip with | Some tooltip -> yield tooltipProps tooltip | None -> () ]
    ] children

let cardModalT theme head body =
    let themeClass = themeClass theme
    Modal.modal [ Modal.IsActive true ] [
        Modal.background [] []
        Modal.Card.card [] [
            match head with
            | Some (title, onDismiss) ->
                yield Modal.Card.head [ CustomClass (themeAlternativeClass theme) ] [
                    yield Modal.Card.title [ CustomClass themeClass ] title
                    match onDismiss with | Some onDismiss -> yield Delete.delete [ Delete.OnClick onDismiss ] [] | None -> () ]
            | None -> ()
            yield Modal.Card.body [ CustomClass themeClass ] body ] ]

let footerT theme useAlternativeClass children =
    Footer.footer [ CustomClass (if useAlternativeClass then themeAlternativeClass theme else themeClass theme) ] children

let private linkT theme linkType children =
    let customClasses = [
        yield themeClass theme
        match linkType with | Internal _ -> yield "internal" | _ -> () ]
    let customClass = match customClasses with | _ :: _ -> Some(String.concat SPACE customClasses) | [] -> None
    a [
        match customClass with | Some customClass -> yield ClassName customClass :> IHTMLProp | None -> ()
        match linkType with
        | Internal onClick ->
            yield OnClick onClick :> IHTMLProp
        | NewWindow url ->
            yield Href url :> IHTMLProp
            yield Target "_blank" :> IHTMLProp
        | DownloadFile(url, fileName) ->
            yield Href url :> IHTMLProp
            yield Download fileName :> IHTMLProp
    ] children
let linkTInternal theme onClick children = linkT theme (Internal onClick) children
let linkTNewWindow theme url children = linkT theme (NewWindow url) children
let linkTDownloadFile theme (url, fileName) children = linkT theme (DownloadFile(url, fileName)) children

let navbarT theme colour children =
    Navbar.navbar [
        Navbar.IsFixedTop
        Navbar.Color (colour |> transformColour theme)
        Navbar.CustomClass (themeClass theme)
    ] [ containerFluid children ]
let navbarMenuT theme isActive children =
    Navbar.menu [
        Navbar.Menu.CustomClass (themeClass theme)
        Navbar.Menu.IsActive isActive
    ] children
let navbarDropDownT theme element children =
    let themeClass = themeClass theme
    Navbar.Item.div [
        Navbar.Item.HasDropdown
        Navbar.Item.IsHoverable
    ] [
        Navbar.Link.div [ Navbar.Link.CustomClass themeClass ] [ element ]
        Navbar.Dropdown.div [ Navbar.Dropdown.CustomClass themeClass ] children ]
let navbarDropDownItemT theme isActive children =
    Navbar.Item.div [
        Navbar.Item.CustomClass (themeClass theme)
        Navbar.Item.IsActive isActive
    ] children

let notificationT theme colour onDismiss children =
    Notification.notification [ Notification.Color (colour |> transformColour theme) ] [
        match onDismiss with | Some onDismiss -> yield delete onDismiss | None -> ()
        yield! children ]

let pageLoaderT theme colour =
    PageLoader.pageLoader [
        PageLoader.Color (colour |> transformColour theme)
        PageLoader.IsActive true ] []

let paraT theme size colour weight children =
    Text.p [ Modifiers [
        Modifier.TextSize(Screen.All, size)
        Modifier.TextColor (colour |> transformColour theme)
        Modifier.TextWeight weight
    ] ] children
let paraTSmallest theme children = paraT theme TextSize.Is7 IsBlack TextWeight.Normal children
let paraTSmaller theme children = paraT theme TextSize.Is6 IsBlack TextWeight.Normal children
let paraTSmall theme children = paraT theme TextSize.Is5 IsBlack TextWeight.Normal children
let paraTMedium theme children = paraT theme TextSize.Is4 IsBlack TextWeight.Normal children
let paraTLarge theme children = paraT theme TextSize.Is3 IsBlack TextWeight.Normal children
let paraTLarger theme children = paraT theme TextSize.Is2 IsBlack TextWeight.Normal children
let paraTLargest theme children = paraT theme TextSize.Is1 IsBlack TextWeight.Normal children

let textBoxT theme (key:Guid) text iconClass isPassword error info autoFocus disabled (onChange:string -> unit) onEnter =
    let input = if isPassword then Input.password else Input.text
    Control.div [ Control.HasIconLeft ] [
        yield input [
            match error with | Some _ -> yield Input.Color IsDanger | None -> ()
            yield Input.CustomClass (themeClass theme)
            yield Input.Size IsSmall
            yield Input.DefaultValue text
            yield Input.Props [
                Key(key.ToString())
                Disabled disabled
                AutoFocus autoFocus
                OnChange(fun ev -> !!ev.target?value |> onChange)
                onEnterPressed onEnter ] ]
        match iconClass with | Some iconClass -> yield iconSmallerLeft iconClass | None -> ()
        match error with | Some error -> yield Help.help [ Help.Color IsDanger ] [ str error ] | None -> ()
        match info with | _ :: _ -> yield Help.help [ Help.Color IsInfo ] info | [] -> () ]
