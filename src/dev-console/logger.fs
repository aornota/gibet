module Aornota.Gibet.DevConsole.Logger

open Aornota.Gibet.Common.IfDebug

open Serilog
open Serilog.Formatting.Json

let [<Literal>] private LOG_FILE_NAME = "dev-console_{Date}.log"

let createLogger() =
    let config =
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.LiterateConsole()
    match ifDebug None (Some LOG_FILE_NAME) with
    | Some fileName -> config.WriteTo.RollingFile(JsonFormatter(), fileName) |> ignore
    | None -> ()
    config.CreateLogger()
