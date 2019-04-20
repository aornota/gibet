module Aornota.Gibet.Server.Logger

open Giraffe.SerilogExtensions

open Serilog
//open Serilog.Formatting.Json

let [<Literal>] private LOG_FILE_NAME = "logs/server_{Date}.log"

let createLogger() =
    LoggerConfiguration()
        .Destructure.FSharpTypes()
#if DEBUG
        .MinimumLevel.Debug()
        .WriteTo.LiterateConsole()
#else
        .MinimumLevel.Information()
        //.WriteTo.RollingFile(JsonFormatter(), LOG_FILE_NAME)
        .WriteTo.RollingFile(LOG_FILE_NAME)
#endif
        .CreateLogger()
