module Aornota.Gibet.Common.Revision

type Rvn = | Rvn of int

let initialRvn = Rvn 1

let incrementRvn (Rvn rvn) = Rvn(rvn + 1)

let validateSameRvn (actual:Rvn) (expected:Rvn) = if actual <> expected then Some(sprintf "Actual %A differs from expected %A" actual expected) else None

let validateNextRvn current next = if incrementRvn current <> next then Some(sprintf "Current %A not consistent with next %A" current next) else None
