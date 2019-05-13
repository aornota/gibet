module Aornota.Gibet.Ui.Shared

open Aornota.Gibet.Ui.Common.Message

type ModalStatus<'a> =
    | ModalPending
    | ModalFailed of 'a

// (α | β | γ | δ | ε) | ζ | η | θ | ι | κ | λ | μ | ν | ξ | ο | π | ρ | σ | τ | υ | φ | χ | ψ | ω
let [<Literal>] GIBET = "gibet (ε)" // note: also update index.html | ..\..\package.json | ..\..\README.md | READ_ME (.\pages\about\markdown-literals.fs)

let [<Literal>] SOMETHING_HAS_GONE_WRONG = "Something has gone wrong. Please try refreshing the page - and if problems persist, please contact the wesbite administrator."

let unexpectedInputWhenState input state = sprintf "Unexpected %A when %A" input state

let renderDebugMessage theme text = renderMessage theme GIBET Debug text
let renderInfoMessage theme text = renderMessage theme GIBET Info text
let renderWarningMessage theme text = renderMessage theme GIBET Warning text
let renderDangerMessage theme text = renderMessage theme GIBET Danger text
