module Aornota.Gibet.UI.Common.RemoteData

open Aornota.Gibet.Common.Revision

type RemoteData<'a, 'b> = | NotRequested | Pending | Received of 'a * Rvn | Failed of 'b

let pending remoteData =
    match remoteData with | Pending -> true | _ -> false
let received remoteData =
    match remoteData with | Received _ -> true | _ -> false
let receivedData remoteData =
    match remoteData with | Received(data, rvn) -> (data, rvn) |> Some | _ -> None
let failed remoteData =
    match remoteData with | Failed _ -> true | _ -> false
let failure remoteData =
    match remoteData with | Failed failure -> failure |> Some | _ -> None
