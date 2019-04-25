module Aornota.Gibet.DevConsole.Logger

open Serilog
// TEMP-NMB...open Serilog.Formatting.Json

let [<Literal>] private LOG_FILE_NAME = "logs/dev-console_{Date}.log"

let createLogger() =
    LoggerConfiguration()
#if DEBUG
        .MinimumLevel.Debug()
        .WriteTo.LiterateConsole()
#else
        .MinimumLevel.Information()
        .WriteTo.LiterateConsole()
        // TEMP-NMB....WriteTo.RollingFile(JsonFormatter(), LOG_FILE_NAME)
        .WriteTo.RollingFile(LOG_FILE_NAME)
#endif
        .CreateLogger()
