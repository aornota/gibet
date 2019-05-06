module Aornota.Gibet.Ui.Common.Theme

open Fulma

type Theme = | Light | Dark

let defaultTheme = Light

let themeClass theme = match theme with | Light -> "light" | Dark -> "dark"
let themeAlternativeClass theme = match theme with | Light -> "light-alternative" | Dark -> "dark-alternative"

let transformColour theme colour =
    match theme, colour with
    | Light, _ -> colour
    | Dark, IsBlack -> IsWhite
    | Dark, IsBlackBis -> IsWhiteBis
    | Dark, IsBlackTer -> IsBlackTer
    | Dark, IsDark -> IsLight
    | Dark, IsLight -> IsDark
    | Dark, IsWhite -> IsBlack
    | Dark, IsWhiteBis -> IsBlackBis
    | Dark, IsWhiteTer -> IsBlackTer
    | Dark, IsGreyDark -> IsGreyLight
    | Dark, IsGreyDarker -> IsGreyLighter
    | Dark, IsGreyLight -> IsGreyDark
    | Dark, IsGreyLighter -> IsGreyDarker
    | Dark, _ -> colour

let colourText colour = // TODO-NMB: Not all of these supported by bulma-tooltip?...
    match colour with
    | IsPrimary -> "primary" | IsInfo -> "info" | IsLink -> "link" | IsSuccess -> "success" | IsWarning -> "warning" | IsDanger -> "danger"
    | IsBlack -> "black" | IsBlackBis -> "black-bis" | IsBlackTer -> "black-ter" | IsDark -> "dark"
    | IsLight -> "light" | IsWhite -> "white" | IsWhiteBis -> "white-bis" | IsWhiteTer -> "white-ter"
    | IsGrey -> "grey" | IsGreyDark -> "grey-dark" | IsGreyDarker -> "grey-darker" | IsGreyLight -> "grey-light" | IsGreyLighter -> "grey-lighter"
    | _ -> "unknown-color"
