module Aornota.Gibet.Server.SourcedLogger

open Serilog

let [<Literal>] private SOURCE_CONTEXT = "SourceContext" // note: same property name as when using ILogger.ForContext<T>()

let sourcedLogger source (logger:ILogger) = logger.ForContext(SOURCE_CONTEXT, sprintf "%s:" source)
