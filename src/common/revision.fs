module Aornota.Gibet.Common.Revision

type Rvn = | Rvn of rvn : int

let initialRvn = 1 |> Rvn

let incrementRvn (Rvn rvn) = rvn + 1 |> Rvn

let validateRvn (actual:Rvn) (expected:Rvn) =
    if actual <> expected then sprintf "Actual %A differs from expected %A" actual expected |> Some
    else None
