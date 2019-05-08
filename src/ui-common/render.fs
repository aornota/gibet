module Aornota.Gibet.Ui.Common.Render

open Browser.Types

open Fable.React
open Fable.React.Props

open Fulma

let [<Literal>] private KEYBOARD_CODE__ENTER = 13.

let [<Literal>] SPACE = " "

let private padStyle padV padH =
    let padding =
        match padV, padH with
        | Some padV, Some padH -> sprintf "%ipx %ipx" padV padH
        | Some padV, None -> sprintf "%ipx 0" padV
        | None, Some padH -> sprintf "0 %ipx" padH
        | None, None -> "0 0"
    Style [ Padding padding ]

let div props children = div props children
let divVerticalSpace height = div [ padStyle (Some (height / 2)) None ] [ str SPACE ]

let str text = str text
let bold text = b [] [ str text ]
let italic text = i [] [ str text ]
let br = br []

let onEnterPressed onEnter =
    OnKeyDown(fun (ev:KeyboardEvent) ->
        match ev with
        | _ when ev.keyCode = KEYBOARD_CODE__ENTER ->
            ev.preventDefault()
            onEnter()
        | _ -> ())

let column children = Column.column [] children
let columns children = Columns.columns [] children

let containerFluid children = Container.container [ Container.IsFluid ] children

let private content alignment children = Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, alignment) ] ] children
let contentCentred children = content TextAlignment.Centered children
let contentLeft children = content TextAlignment.Left children
let contentRight children = content TextAlignment.Right children

let private field options children = Field.div [ yield! options ] children
let fieldExpanded children = field [ Field.IsExpanded ] children
let fieldGroupedCentred children = field [ Field.IsGroupedCentered ] children

let private icon size options iconClass =
    let customClasses = [
        yield "fas"
        match size with | Some IsSmall -> () | Some IsMedium -> yield "fa-2x" | Some IsLarge -> yield "fa-3x" | None -> yield "fa-lg"
        yield iconClass ]
    let customClass = match customClasses with | _ :: _ -> Some(String.concat SPACE customClasses) | [] -> None
    Icon.icon [
        match size with | Some size -> yield Icon.Size size | None -> ()
        yield! options
    ] [ i [ match customClass with | Some customClass -> yield ClassName customClass | None -> () ] [] ]
let iconSmaller iconClass = icon (Some IsSmall) [] iconClass
let iconSmallerLeft iconClass = icon (Some IsSmall) [ Icon.IsLeft ] iconClass
let iconSmall iconClass = icon None [] iconClass
let iconLarge iconClass = icon (Some IsMedium) [] iconClass
let iconLarger iconClass = icon (Some IsLarge) [] iconClass

let image source size =
    Image.image [
        Image.Props [ Key source ]
        size
    ] [ img [ Src source ] ]

let navbarBrand children = Navbar.Brand.div [] children
let navbarBurger onClick isActive =
    Navbar.burger [
        if isActive then yield Fulma.Common.CustomClass "is-active"
        yield Fulma.Common.Props [ OnClick onClick ]
    ] [ for _ in 1..3 do yield span [] [] ]
let navbarItem children = Navbar.Item.div [] children
let navbarStart children = Navbar.Start.div [] children
let navbarEnd children = Navbar.End.div [] children
