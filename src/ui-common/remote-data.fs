module Aornota.Gibet.UI.Common.RemoteData

type RemoteData<'a, 'b> =
    | NotRequested
    | Pending
    | Received of 'a
    | Failed of 'b
