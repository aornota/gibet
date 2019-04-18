module Aornota.Gibet.Common.Revision

type Rvn = | Rvn of rvn : int

let initialRvn = 1 |> Rvn

let incrementRvn (Rvn rvn) = rvn + 1 |> Rvn

let validateNextRvn (currentRvn:Rvn option) (Rvn nextRvn) =
    match currentRvn, nextRvn with
    | None, nextRvn when nextRvn = 1 -> true
    | Some (Rvn currentRvn), nextRvn when currentRvn + 1 = nextRvn -> true
    | _ -> false
