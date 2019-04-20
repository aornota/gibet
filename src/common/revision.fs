module Aornota.Gibet.Common.Revision

type Rvn = | Rvn of rvn : int

let initialRvn = 1 |> Rvn

let incrementRvn (Rvn rvn) = rvn + 1 |> Rvn

let validateRvn (Rvn actualRvn) (Rvn expectedRvn) =
    if actualRvn <> expectedRvn then sprintf "Actual %A differs from expected %A" actualRvn expectedRvn |> Error
    else () |> Ok
