module Aornota.Gibet.Server.Bridge.IHub

type IHub<'a, 'b, 'c> =
    abstract GetModels: unit -> 'a list
    abstract BroadcastClient: 'c -> unit
    abstract BroadcastServer: 'b -> unit
    abstract SendClientIf: ('a -> bool) -> 'c -> unit
    abstract SendServerIf: ('a -> bool) -> 'b -> unit
