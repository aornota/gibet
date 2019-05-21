[<AutoOpen>]
module Aornota.Gibet.Ui.Common.Render.Theme

open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Common.Tooltip

open System

open Browser.Types

open Fable.Core.JsInterop
module RctH = Fable.React.Helpers
open Fable.React.Props
module RctS = Fable.React.Standard

open Fulma
open Fulma.Extensions.Wikiki

type ButtonInteraction =
    | Clickable of (MouseEvent -> unit)
    | Loading
    | Static
    | NotEnabled

let private colourM textColour = Modifier.TextColor textColour

let buttonT theme size colour interaction tooltip children =
    let tooltip = tooltip |> Option.map (fun tooltip -> { tooltip with TooltipColour = tooltip.TooltipColour |> transformColour theme })
    Button.button [
        match size with | Some size -> yield Button.Size size | None -> ()
        yield Button.Color(colour |> transformColour theme)
        match interaction with
        | Clickable onClick -> yield Button.OnClick onClick
        | Loading -> yield Button.IsLoading true
        | Static -> yield Button.IsStatic true
        | NotEnabled -> ()
        match tooltip with | Some tooltip -> yield Button.CustomClass(tooltipClass tooltip) | None -> ()
        yield Button.Props [
            yield Disabled (match interaction with | NotEnabled -> true | _ -> false)
            match tooltip with | Some tooltip -> yield tooltipProps tooltip | None -> () ]
    ] children
let buttonTSmall theme colour interaction children = buttonT theme (Some IsSmall) colour interaction None children
let buttonTSmallTooltip theme colour interaction tooltip children = buttonT theme (Some IsSmall) colour interaction (Some tooltip) children

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

let contentT theme textAlignment textSize textColour children =
    Content.content [ Content.Modifiers [
        yield alignmentM textAlignment
        match textSize with | Some textSize -> yield sizeM textSize | None -> ()
        match textColour with | Some textColour -> yield colourM (textColour |> transformColour theme) | None -> ()
    ] ] children
let contentTLeft theme textSize textColour children = contentT theme left textSize textColour children
let contentTLeftSmallest theme textColour children = contentT theme left (Some smallest) textColour children
let contentTCentred theme textSize textColour children = contentT theme centred textSize textColour children
let contentTCentredSmallest theme textColour children = contentT theme centred (Some smallest) textColour children
let contentTRight theme textSize textColour children = contentT theme right textSize textColour children
let contentTRightSmallest theme textColour children = contentT theme right (Some smallest) textColour children

let footerT theme useAlternativeClass children = Footer.footer [ CustomClass(if useAlternativeClass then themeAlternativeClass theme else themeClass theme) ] children

let helpT theme colour children = Help.help [ Help.Color(colour |> transformColour theme) ] children
let helpTInfo theme children = helpT theme IsInfo children
let helpTSuccess theme children = helpT theme IsSuccess children
let helpTWarning theme children = helpT theme IsWarning children
let helpTDanger theme children = helpT theme IsDanger children

let hrT theme useAlternativeClass = RctS.hr [ ClassName(if useAlternativeClass then themeAlternativeClass theme else themeClass theme) ]

let private iconTTooltip theme size options iconClass tooltip =
    let tooltip = { tooltip with TooltipColour = tooltip.TooltipColour |> transformColour theme }
    let customClasses = [
        yield "fas"
        match size with | Some IsSmall -> () | Some IsMedium -> yield "fa-2x" | Some IsLarge -> yield "fa-3x" | None -> yield "fa-lg"
        yield iconClass ]
    let customClass = match customClasses with | _ :: _ -> Some(String.concat SPACE customClasses) | [] -> None
    Icon.icon [
        match size with | Some size -> yield Icon.Size size | None -> ()
        yield! options
        yield Icon.CustomClass(tooltipClass tooltip)
        yield Icon.Props [ tooltipProps tooltip ]
    ] [ RctS.i [ match customClass with | Some customClass -> yield ClassName customClass | None -> () ] [] ]
let iconTTooltipSmaller theme iconClass tooltip = iconTTooltip theme (Some IsSmall) [] iconClass tooltip
let iconTTooltipSmall theme iconClass tooltip = iconTTooltip theme None [] iconClass tooltip
let iconTTooltipLarge theme iconClass tooltip = iconTTooltip theme (Some IsMedium) [] iconClass tooltip
let iconTTooltipLarger theme iconClass tooltip = iconTTooltip theme (Some IsLarge) [] iconClass tooltip

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

let paraT theme size colour children = Text.p [ Modifiers [ sizeM size ; colourM (colour |> transformColour theme) ] ] children
let paraTSmallest theme colour children = paraT theme smallest colour children
let paraTSmaller theme colour children = paraT theme smaller colour children
let paraTSmall theme colour children = paraT theme small colour children
let paraTMedium theme colour children = paraT theme medium colour children
let paraTLarge theme colour children = paraT theme large colour children
let paraTLarger theme colour children = paraT theme larger colour children
let paraTLargest theme colour children = paraT theme largest colour children

let radioInlineT theme size colour hasBackgroundColour (key:Guid) isChecked text disabled onChange =
    Checkradio.radioInline [
        yield Checkradio.Id(key.ToString())
        yield Checkradio.Size size
        if hasBackgroundColour then yield Checkradio.HasBackgroundColor
        yield Checkradio.Color(colour |> transformColour theme)
        yield Checkradio.Checked isChecked
        yield Checkradio.Disabled disabled
        yield Checkradio.OnChange onChange
    ] [ text ]
let radioInlineTSmall theme colour hasBackgroundColour key isChecked text disabled onChange = radioInlineT theme IsSmall colour hasBackgroundColour key isChecked text disabled onChange

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
        match size with
        | Some IsLarge -> yield Tag.Size IsLarge
        | Some IsMedium -> yield Tag.Size IsMedium
        | _ -> ()
        yield Tag.Color(colour |> transformColour theme)
        if rounded then yield Tag.CustomClass "is-rounded"
    ] [
        yield! children
        match onDismiss with | Some onDismiss -> yield delete onDismiss | None -> () ]
let tagTSmall theme colour children = tagT theme None colour false None children

let textAreaT theme (key:Guid) text status extraInfo autoFocus disabled (onChange:string -> unit) =
    let colour, help =
        match status with
        | Some(colour, help) -> Some colour, Some help
        | None -> None, None
    Control.div [] [
        yield Textarea.textarea [
            match colour with | Some colour -> yield Textarea.Color(colour |> transformColour theme) | None -> ()
            yield Textarea.CustomClass(themeClass theme)
            yield Textarea.Size IsSmall
            yield Textarea.DefaultValue text
            yield Textarea.Props [
                Key(key.ToString ())
                Disabled disabled
                AutoFocus autoFocus
                OnChange(fun ev -> !!ev.target?value |> onChange) ] ] []
        yield RctH.ofOption help
        yield RctH.ofOption extraInfo ]

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
        yield RctH.ofOption help ]
let textTDefault theme key text status iconLeft autoFocus disabled onChange onEnter = textT theme key text status false (Some iconLeft) autoFocus disabled onChange onEnter
let textTPassword theme key text status autoFocus disabled onChange onEnter = textT theme key text status true (Some ICON__PASSWORD) autoFocus disabled onChange onEnter
