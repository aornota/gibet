module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Domain.Counter

type State = { Counter : Counter option }

type Input =
    | Increment
    | Decrement
    | InitialCountLoaded of Result<Counter, exn>

let [<Literal>] GIBET = "gibet (α)"
