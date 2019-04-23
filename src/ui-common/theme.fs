module Aornota.Gibet.UI.Common.Theme

type Theme = | Light | Dark

let themeClass theme =
    match theme with
    | Light -> "light"
    | Dark -> "dark"
let themeAlternativeClass theme =
    match theme with
    | Light -> "light-alternative"
    | Dark -> "dark-alternative"

// TODO-NMB: transformSemantic (&c.)...
