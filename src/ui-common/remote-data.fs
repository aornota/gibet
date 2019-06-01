module Aornota.Gibet.Ui.Common.RemoteData

open Aornota.Gibet.Common.Rvn

type RemoteData<'a, 'b, 'c> = | Pending | Received of 'a * 'b | Failed of 'c
