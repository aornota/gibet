module Aornota.Gibet.Server.Logger

open System

open Giraffe.SerilogExtensions

open Serilog

let [<Literal>] private FILE_SIZE_LIMIT = 1_000_000L
let [<Literal>] private RETAINED_FILE_COUNT = 7

let sourced message source = sprintf "%s: %s" source message

// #region createLogger
let createLogger fileName =
    LoggerConfiguration()
        .Destructure.FSharpTypes()
#if DEBUG
        .MinimumLevel.Debug()
        .WriteTo.LiterateConsole()
#else
        .MinimumLevel.Information()
        .WriteTo.RollingFile(fileName, fileSizeLimitBytes = Nullable<int64>(FILE_SIZE_LIMIT), retainedFileCountLimit = Nullable<int>(RETAINED_FILE_COUNT))
#endif
        .CreateLogger()
// #endregion
