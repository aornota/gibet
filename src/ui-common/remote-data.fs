module Aornota.Gibet.Ui.Common.RemoteData

open Aornota.Gibet.Common.Revision

type RemoteData<'a, 'b> = | Pending | Received of 'a * Rvn | Failed of 'b
