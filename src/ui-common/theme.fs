module Aornota.Gibet.UI.Common.Theme

open Fulma

type Theme = | Light | Dark

let defaultTheme = Light

let themeClass theme = match theme with | Light -> "light" | Dark -> "dark"
let themeAlternativeClass theme = match theme with | Light -> "light-alternative" | Dark -> "dark-alternative"

let transformColour colour theme =
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
