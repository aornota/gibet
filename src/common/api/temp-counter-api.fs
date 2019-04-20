module Aornota.Gibet.Common.Api.ICounterApi

open Aornota.Gibet.Common.Domain.Counter

type ICounterApi = {
    initialCounter : unit -> Async<Counter>
}
