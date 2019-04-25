module Aornota.Gibet.Server.Logger

open System

open Giraffe.SerilogExtensions

open Serilog
// TODO-NMB?...open Serilog.Formatting.Json

let [<Literal>] private LOG_FILE_NAME = "logs/server_{Date}.log"
let [<Literal>] private FILE_SIZE_LIMIT = 1_000_000L
let [<Literal>] private RETAINED_FILE_COUNT = 7

// #region createLogger
let createLogger() =
    LoggerConfiguration()
        .Destructure.FSharpTypes()
#if DEBUG
        .MinimumLevel.Debug()
        .WriteTo.LiterateConsole()
#else
        .MinimumLevel.Information()
        // TODO-NMB?....WriteTo.RollingFile(JsonFormatter(), LOG_FILE_NAME, fileSizeLimitBytes = Nullable<int64>(FILE_SIZE_LIMIT), retainedFileCountLimit = Nullable<int>(RETAINED_FILE_COUNT))
        .WriteTo.RollingFile(LOG_FILE_NAME, fileSizeLimitBytes = Nullable<int64>(FILE_SIZE_LIMIT), retainedFileCountLimit = Nullable<int>(RETAINED_FILE_COUNT))
#endif
        .CreateLogger()
// #endregion
