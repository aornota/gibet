module Aornota.Gibet.Ui.Shared

open Aornota.Gibet.Ui.Common.Message

type ModalStatus<'a> = | ModalPending | ModalFailed of 'a

let [<Literal>] GIBET = "gibet (γ)" // note: also update index.html, package.json, README.md and READ_ME (α | β | γ | δ | ε)

let renderDebugMessage theme text = renderMessage theme GIBET Debug text
let renderInfoMessage theme text = renderMessage theme GIBET Info text
let renderWarningMessage theme text = renderMessage theme GIBET Warning text
let renderDangerMessage theme text = renderMessage theme GIBET Danger text
