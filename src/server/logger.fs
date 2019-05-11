module Aornota.Gibet.Server.Logger

open System

open Giraffe.SerilogExtensions

open Serilog

let [<Literal>] private LOG_SOURCE = "Source"

let [<Literal>] private FILE_SIZE_LIMIT = 1_000_000L
let [<Literal>] private RETAINED_FILE_COUNT = 7

// #region defaultLogger
let defaultLogger (fileName:string) =
    let outputTemplate = sprintf "[{Timestamp:HH:mm:ss zzz} {Level:u3}] {%s}{Message:lj}{NewLine}" LOG_SOURCE
    LoggerConfiguration()
        .Destructure.FSharpTypes()
#if DEBUG
        .MinimumLevel.Debug()
        .WriteTo.LiterateConsole(outputTemplate = outputTemplate)
#else
        .MinimumLevel.Information()
        .WriteTo.RollingFile(fileName, outputTemplate = outputTemplate, fileSizeLimitBytes = Nullable<int64>(FILE_SIZE_LIMIT), retainedFileCountLimit = Nullable<int>(RETAINED_FILE_COUNT))
#endif
        .CreateLogger()
// #endregion

let sourcedLogger source (logger:ILogger) = logger.ForContext(LOG_SOURCE, sprintf "%s: " source)
