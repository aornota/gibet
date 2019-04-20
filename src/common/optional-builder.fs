[<AutoOpen>]
module Aornota.Gibet.Common.OptionalBuilder

// Based on https://gist.github.com/kekyo/cadc0ec4b016368a0cee81d87fbee63a.

[<Struct>]
type OptionalBuilder =
    member __.Bind(opt, binder) = match opt with | Some value -> binder value | None -> None
    member __.Return(value) = Some value

let optional = OptionalBuilder()
