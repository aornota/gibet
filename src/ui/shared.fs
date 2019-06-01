module Aornota.Gibet.Ui.Shared

open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.ShouldNeverHappen

open Elmish

type ApiStatus<'a> = | ApiPending | ApiFailed of 'a

// (α | β | γ | δ | ε | ζ | η | θ | ι | κ | λ | μ | ν | ξ) | ο | π | ρ | σ | τ | υ | φ | χ | ψ | ω
let [<Literal>] GIBET = "gibet (ξ)" // note: also update index.html | ..\..\package.json | ..\..\README.md | READ_ME (.\pages\about\markdown-literals.fs)

let [<Literal>] ERROR = "ERROR"
let [<Literal>] SOMETHING_HAS_GONE_WRONG = "Something has gone wrong. Please try refreshing the page - and if problems persist, please contact the wesbite administrator."

let unexpectedInputWhenState input state = sprintf "Unexpected %A when %A" input state

let addMessageCmd messageType text (toCmd:Message -> Cmd<'a>) = toCmd(messageDismissable messageType text)

let addDebugErrorCmd error toCmd = addMessageCmd Debug (sprintf "%s -> %s" ERROR error) toCmd
// #region shouldNeverHappenCmd
let shouldNeverHappenCmd error toCmd =
#if DEBUG
    addDebugErrorCmd (shouldNeverHappen error) toCmd
#else
    addMessageCmd Danger SOMETHING_HAS_GONE_WRONG toCmd
#endif
// #endregion

let renderDebugMessage theme text = renderMessage theme GIBET Debug text
let renderInfoMessage theme text = renderMessage theme GIBET Info text
let renderWarningMessage theme text = renderMessage theme GIBET Warning text
let renderDangerMessage theme text = renderMessage theme GIBET Danger text
