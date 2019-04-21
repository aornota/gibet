module Aornota.Gibet.Server.Api.CounterApi

open Aornota.Gibet.Common.Api.ICounterApi
open Aornota.Gibet.Common.Domain.Counter

open Serilog

let private initialCounter (logger:ILogger) =
    fun () -> async {
        logger.Debug("Retrieving initial counter...")
        let value = { Value = 42 }
        logger.Debug("...retrieved initial counter: {value}", value)
        return value }

let counterApiReader =
    reader {
        let! logger = resolve<ILogger>()
        return {
            initialCounter = initialCounter logger
        } }
