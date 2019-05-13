module Aornota.Gibet.Ui.Common.Render.Theme

open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.Render.Shared
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

let private delete onClick = Delete.delete [ Delete.OnClick onClick ] []

let buttonT theme size colour interaction outlined inverted tooltip children =
    let tooltip = tooltip |> Option.map (fun tooltip -> { tooltip with TooltipColour = tooltip.TooltipColour |> transformColour theme })
    Button.button [
        match size with | Some size -> yield Button.Size size | None -> ()
        yield Button.Color(colour |> transformColour theme)
        match interaction with
        | Clickable onClick -> yield Button.OnClick onClick
        | Loading -> yield Button.IsLoading true
        | Static -> yield Button.IsStatic true
        | NotEnabled -> ()
        if outlined then yield Button.IsOutlined
        if inverted then yield Button.IsInverted
        match tooltip with | Some tooltip -> yield Button.CustomClass(tooltipClass tooltip) | None -> ()
        yield Button.Props [
            yield Disabled (match interaction with | NotEnabled -> true | _ -> false)
            match tooltip with | Some tooltip -> yield tooltipProps tooltip | None -> () ]
    ] children
let buttonTSmall theme colour interaction children = buttonT theme (Some IsSmall) colour interaction false false None children

let cardModalT theme head body =
    let themeClass = themeClass theme
    Modal.modal [ Modal.IsActive true ] [
        Modal.background [] []
        Modal.Card.card [] [
            match head with
            | Some(title, onDismiss) ->
                yield Modal.Card.head [ CustomClass(themeAlternativeClass theme) ] [
                    yield Modal.Card.title [ CustomClass themeClass ] title
                    match onDismiss with | Some onDismiss -> yield Delete.delete [ Delete.OnClick onDismiss ] [] | None -> () ]
            | None -> ()
            yield Modal.Card.body [ CustomClass themeClass ] body ] ]

let checkT theme size colour hasBackgroundColour (key:Guid) isChecked text disabled onChange =
    Checkradio.checkbox [
        yield Checkradio.Id(key.ToString())
        yield Checkradio.Size size
        if hasBackgroundColour then yield Checkradio.HasBackgroundColor
        yield Checkradio.Color(colour |> transformColour theme)
        yield Checkradio.Checked isChecked
        yield Checkradio.Disabled disabled
        yield Checkradio.OnChange onChange
    ] [ str text ]
let checkTSmall theme colour hasBackgroundColour key isChecked text disabled onChange = checkT theme IsSmall colour hasBackgroundColour key isChecked text disabled onChange

let footerT theme useAlternativeClass children = Footer.footer [ CustomClass(if useAlternativeClass then themeAlternativeClass theme else themeClass theme) ] children

let helpT theme colour children = Help.help [ Help.Color(colour |> transformColour theme) ] children
let helpTInfo theme children = helpT theme IsInfo children
let helpTSuccess theme children = helpT theme IsSuccess children
let helpTWarning theme children = helpT theme IsWarning children
let helpTDanger theme children = helpT theme IsDanger children

let hr theme useAlternativeClass = hr [ ClassName(if useAlternativeClass then themeAlternativeClass theme else themeClass theme) ]

let labelT theme size colour weight children = Label.label [ Label.Modifiers [ sizeM size ; colourM (colour |> transformColour theme) ; weightM weight ] ] children
let labelTSmallest theme children = labelT theme TextSize.Is7 IsBlack TextWeight.Normal children

let private linkT theme linkType children =
    let customClasses = [
        yield themeClass theme
        match linkType with | Internal _ | DownloadFile _ -> yield "internal" | NewWindow _ -> yield "external" ]
    let customClass = match customClasses with | _ :: _ -> Some(String.concat SPACE customClasses) | [] -> None
    a [
        match customClass with | Some customClass -> yield ClassName customClass :> IHTMLProp | None -> ()
        match linkType with
        | Internal onClick -> yield OnClick onClick
        | NewWindow url ->
            yield Href url
            yield Target "_blank"
        | DownloadFile(url, fileName) ->
            yield Href url
            yield Download fileName
    ] children
let linkTInternal theme onClick children = linkT theme (Internal onClick) children
let linkTNewWindow theme url children = linkT theme (NewWindow url) children
let linkTDownloadFile theme (url, fileName) children = linkT theme (DownloadFile(url, fileName)) children

let navbarT theme colour children =
    Navbar.navbar [
        Navbar.IsFixedTop
        Navbar.Color(colour |> transformColour theme)
        Navbar.CustomClass(themeClass theme)
    ] [ containerFluid children ]
let navbarMenuT theme isActive children = Navbar.menu [ Navbar.Menu.CustomClass(themeClass theme) ; Navbar.Menu.IsActive isActive ] children
let navbarDropDownT theme element children =
    let themeClass = themeClass theme
    Navbar.Item.div [
        Navbar.Item.HasDropdown
        Navbar.Item.IsHoverable
    ] [
        Navbar.Link.div [ Navbar.Link.CustomClass themeClass ] [ element ]
        Navbar.Dropdown.div [ Navbar.Dropdown.CustomClass themeClass ] children ]
let navbarDropDownItemT theme isActive children = Navbar.Item.div [ Navbar.Item.CustomClass(themeClass theme) ; Navbar.Item.IsActive isActive ] children

let notificationT theme colour onDismiss children =
    Notification.notification [ Notification.Color(colour |> transformColour theme) ] [
        match onDismiss with | Some onDismiss -> yield delete onDismiss | None -> ()
        yield! children ]

let pageLoaderT theme colour = PageLoader.pageLoader [ PageLoader.Color(colour |> transformColour theme) ; PageLoader.IsActive true ] []

let paraT theme size colour weight children = Text.p [ Modifiers [ sizeM size ; colourM (colour |> transformColour theme) ; weightM weight ] ] children
let paraTSmallest theme children = paraT theme TextSize.Is7 IsBlack TextWeight.Normal children
let paraTSmaller theme children = paraT theme TextSize.Is6 IsBlack TextWeight.Normal children
let paraTSmall theme children = paraT theme TextSize.Is5 IsBlack TextWeight.Normal children
let paraTMedium theme children = paraT theme TextSize.Is4 IsBlack TextWeight.Normal children
let paraTLarge theme children = paraT theme TextSize.Is3 IsBlack TextWeight.Normal children
let paraTLarger theme children = paraT theme TextSize.Is2 IsBlack TextWeight.Normal children
let paraTLargest theme children = paraT theme TextSize.Is1 IsBlack TextWeight.Normal children

// TODO-NMB (cf. checkT): let radioT theme...

let tableT theme useAlternativeClass bordered narrow striped fullWidth children =
    Table.table [
        yield Table.CustomClass(if useAlternativeClass then themeAlternativeClass theme else themeClass theme)
        if bordered then yield Table.IsBordered
        if narrow then yield Table.IsNarrow
        if striped then yield Table.IsStriped
        if fullWidth then yield Table.IsFullWidth
    ] children
let tableTDefault theme useAlternativeClass children = tableT theme useAlternativeClass false true false true children

let tabsT theme options tabs = Tabs.tabs [ yield Tabs.CustomClass(themeClass theme) ; yield! options ] tabs
let tabsTSmall theme tabs = tabsT theme [ Tabs.Size IsSmall ] tabs

let tagT theme size colour rounded onDismiss children =
    Tag.tag [
        yield Tag.Size size
        yield Tag.Color(colour |> transformColour theme)
        if rounded then yield Tag.CustomClass "is-rounded"
    ] [
        yield! children
        match onDismiss with | Some onDismiss -> yield delete onDismiss | None -> () ]
let tagTSmall theme colour children = tagT theme IsSmall colour false None children

let textT theme (key:Guid) text status password iconLeft autoFocus disabled (onChange:string -> unit) onEnter =
    let colour, iconRight, help =
        match status with
        | Some(colour, iconRight, help) -> Some colour, Some iconRight, Some help
        | None -> None, None, None
    let input = if password then Input.password else Input.text
    Control.div [
        match iconLeft with | Some _ -> yield Control.HasIconLeft | None -> ()
        match iconRight with | Some _ -> yield Control.HasIconRight | None -> ()
    ] [
        yield input [
            match colour with | Some colour -> yield Input.Color(colour |> transformColour theme) | None -> ()
            yield Input.CustomClass(themeClass theme)
            yield Input.Size IsSmall
            yield Input.DefaultValue text
            yield Input.Props [
                Key(key.ToString())
                Disabled disabled
                AutoFocus autoFocus
                OnChange(fun ev -> !!ev.target?value |> onChange)
                onEnterPressed onEnter ] ]
        match iconLeft with | Some iconLeft -> yield iconSmallerLeft iconLeft | None -> ()
        match iconRight with | Some iconRight -> yield iconSmallerRight iconRight | None -> ()
        yield ofOption help ]
let textTDefault theme key text status iconLeft autoFocus disabled onChange onEnter = textT theme key text status false (Some iconLeft) autoFocus disabled onChange onEnter
let textTPassword theme key text status autoFocus disabled onChange onEnter = textT theme key text status true (Some ICON__PASSWORD) autoFocus disabled onChange onEnter
