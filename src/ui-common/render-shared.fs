[<AutoOpen>]
module Aornota.Gibet.Ui.Common.Render.Shared

open Browser.Types

open Fable.React
open Fable.React.Props

open Fulma

type private LinkType =
    | Internal of (MouseEvent -> unit)
    | NewWindow of string
    | DownloadFile of url : string * fileName : string

let [<Literal>] private KEYBOARD_CODE__ENTER = 13.
let [<Literal>] private KEYBOARD_CODE__ESCAPE = 27.

let [<Literal>] SPACE = " "

let left, centred, right = TextAlignment.Left, TextAlignment.Centered, TextAlignment.Right
let smallest, smaller, small, medium, large, larger, largest = TextSize.Is7, TextSize.Is6, TextSize.Is5, TextSize.Is4, TextSize.Is3, TextSize.Is2, TextSize.Is1

let alignmentM textAlignment = Modifier.TextAlignment(Screen.All, textAlignment)
let sizeM textSize = Modifier.TextSize(Screen.All, textSize)

let str text = str text
let strongEm text = strong [] [ em [] [ str text ] ]
let strong text = strong [] [ str text ]
let em text = em [] [ str text ]
let br = br []

let private padStyle padV padH =
    let padding =
        match padV, padH with
        | Some padV, Some padH -> sprintf "%ipx %ipx" padV padH
        | Some padV, None -> sprintf "%ipx 0" padV
        | None, Some padH -> sprintf "0 %ipx" padH
        | _ -> "0 0"
    Style [ Padding padding ]
let div props children = div props children
let divDefault children = div [] children
let divEmpty = divDefault []
let divTags children = div [ ClassName "tags" ] children
let divVerticalSpace height = div [ padStyle (Some(height / 2)) None ] [ str SPACE ]

let onEnterPressed onEnter =
    OnKeyDown(fun (ev:KeyboardEvent) ->
        match ev with
        | _ when ev.keyCode = KEYBOARD_CODE__ENTER ->
            ev.preventDefault()
            onEnter()
        | _ -> ())
let onEscapePressed onEscape =
    OnKeyDown(fun (ev:KeyboardEvent) ->
        match ev with
        | _ when ev.keyCode = KEYBOARD_CODE__ESCAPE ->
            ev.preventDefault()
            onEscape()
        | _ -> ())

let delete onClick = Delete.delete [ Delete.OnClick onClick ] []

let private columns mobile children = Columns.columns [ if mobile then yield Columns.IsMobile ] children
let private column children = Column.column [] children
let private columnEmpty = column []
let columnsDefault children =
    columns true [
        columnEmpty
        Column.column [
            Column.Width(Screen.Mobile, Column.IsFourFifths)
            Column.Width(Screen.Tablet, Column.IsFourFifths)
            Column.Width(Screen.Desktop, Column.IsThreeQuarters)
            Column.Width(Screen.WideScreen, Column.IsThreeFifths)
            Column.Width(Screen.FullHD, Column.IsHalf) ] children
        columnEmpty ]
let columnsLeftAndRight leftChildren rightChildren = columns true [ column leftChildren ; column rightChildren ]

let containerFluid children = Container.container [ Container.IsFluid ] children

let content textAlignment textSize children =
    Content.content [ Content.Modifiers [
        yield alignmentM textAlignment
        match textSize with | Some textSize -> yield sizeM textSize | None -> ()
        //match colour with | Some colour -> yield colourM colour | None -> ()
    ] ] children
let contentLeft textSize children = content left textSize children
let contentLeftSmallest children = content left (Some smallest) children
let contentCentred textSize children = content centred textSize children
let contentCentredSmallest children = content centred (Some smallest) children
let contentCentredSmaller children = content centred (Some smaller) children
let contentRight textSize children = content right textSize children
let contentRightSmallest children = content right (Some smallest) children

let private field options children = Field.div [ yield! options ] children
let fieldDefault children = field [] children
let fieldGroupedCentred children = field [ Field.IsGroupedCentered ] children
let fieldGroupedRight children = field [ Field.IsGroupedRight ] children
let fieldFullWidth children = field [ Field.HasAddonsFullWidth ] children

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
let iconSmallerRight iconClass = icon (Some IsSmall) [ Icon.IsRight ] iconClass
let iconSmall iconClass = icon None [] iconClass
let iconLarge iconClass = icon (Some IsMedium) [] iconClass
let iconLarger iconClass = icon (Some IsLarge) [] iconClass

let image source size = Image.image [ Image.Props [ Key source ] ; size ] [ img [ Src source ] ]

let label size children = Label.label [ Label.Modifiers [ sizeM size ] ] children
let labelSmallest children = label smallest children

let level hasContinuation children = Level.level [ if hasContinuation then yield Level.Level.CustomClass "hasContinuation" ] children
let levelLeft children = Level.left [] children
let levelRight children = Level.right [] children
let levelItem children = Level.item [] children

let private link linkType children =
    a [
        yield ClassName (match linkType with | Internal _ | DownloadFile _ -> "internal" | NewWindow _ -> "external")
        match linkType with
        | Internal onClick -> yield OnClick onClick
        | NewWindow url ->
            yield Href url
            yield Target "_blank"
        | DownloadFile(url, fileName) ->
            yield Href url
            yield Download fileName
    ] children
let linkInternal onClick children = link (Internal onClick) children
let linkNewWindow url children = link (NewWindow url) children
let linkDownloadFile (url, fileName) children = link (DownloadFile(url, fileName)) children

let navbarBrand children = Navbar.Brand.div [] children
let navbarBurger onClick isActive =
    Navbar.burger [
        if isActive then yield Fulma.Common.CustomClass "is-active"
        yield Fulma.Common.Props [ OnClick onClick ]
    ] [ for _ in 1..3 do yield span [] [] ]
let navbarItem children = Navbar.Item.div [] children
let navbarStart children = Navbar.Start.div [] children
let navbarEnd children = Navbar.End.div [] children

let private para size children = Text.p [ Modifiers [ sizeM size ] ] children
let paraSmallest children = para smallest children
let paraSmaller children = para smaller children
let paraSmall children = para small children
let paraMedium children = para medium children
let paraLarge children = para large children
let paraLarger children = para larger children
let paraLargest children = para largest children

let tab isActive children = Tabs.tab [ Tabs.Tab.IsActive isActive ] children

let thead children = thead [] children
let tbody children = tbody [] children
let tr selected children = tr [ if selected then yield ClassName "is-selected" ] children
let th children = th [] children
let td children = td [] children
