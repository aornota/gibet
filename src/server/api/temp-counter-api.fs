module Aornota.Gibet.Server.Api.CounterApi

open Aornota.Gibet.Common.Api.ICounterApi
open Aornota.Gibet.Common.Domain.Counter

open Microsoft.AspNetCore.Http

open Giraffe.SerilogExtensions

open Serilog

let private initialCounter (logger:ILogger) = async {
    logger.Debug("Retrieving initial counter...")
    let value = { Value = 42 }
    logger.Debug("...retrieved initial counter: {value}", value)
    return value
}

let private counter (logger:ILogger) = {
    initialCounter = fun _ -> initialCounter logger
}

let counterApi (context:HttpContext) =
    let logger : ILogger = context.Logger()
    counter logger
