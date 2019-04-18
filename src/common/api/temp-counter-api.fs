module Aornota.Gibet.Common.Api.CounterApi

open Aornota.Gibet.Common.Domain.Counter

type CounterApi = {
    initialCounter : unit -> Async<Counter> }
