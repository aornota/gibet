module Aornota.Gibet.UI.Common.Render.Theme

open Aornota.Gibet.UI.Common.Render
open Aornota.Gibet.UI.Common.Theme
open Aornota.Gibet.UI.Common.Tooltip

open Browser.Types

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

let navbar theme colour children =
    Navbar.navbar [
        Navbar.IsFixedTop
        Navbar.Color (colour |> transformColour theme)
        Navbar.CustomClass (themeClass theme)
    ] [ containerFluid children ]

let pageLoader theme colour =
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
