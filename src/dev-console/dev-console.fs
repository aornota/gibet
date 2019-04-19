module Aornota.Gibet.DevConsole

open Aornota.Gibet.Common.IfDebug

open System

open Serilog
open Serilog.Formatting.Json
open System.Diagnostics

let [<Literal>] private LOG_FILE_NAME = "dev-console_{Date}.log"

let private lockObj = obj()

let private write (text:string) consoleColor =
    let action = (fun _ ->
        let current = Console.ForegroundColor
        Console.ForegroundColor <- consoleColor
        Console.Write text
        Console.ForegroundColor <- current)
    lock lockObj action

let private writeNewLine text consoleColor = write (sprintf "\n%s" text) consoleColor

let private writeBlankLine() = writeNewLine String.Empty ConsoleColor.White

let private createLogger rollingFileName =
    let config =
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.LiterateConsole()
    match rollingFileName with
    | Some rollingFileName -> config.WriteTo.RollingFile(JsonFormatter(), rollingFileName) |> ignore
    | None -> ()
    config.CreateLogger()

[<EntryPoint>]
let main argv =
    // #region "Running Aornota.Gibet.DevConsole.main..."
    writeNewLine "Running Aornota.Gibet.DevConsole.main" ConsoleColor.Magenta
    write (sprintf " %A" argv) ConsoleColor.DarkMagenta
    write "...\n\n" ConsoleColor.Magenta
    // #endregion

    use logger = createLogger(ifDebug None (Some LOG_FILE_NAME))

    try
        // #region Logging examples
        (*let test = Some 3.14
        logger.Debug("This is a debug message")
        logger.Information("This is an information message: {test}", test)
        logger.Warning("This is a warning message")
        failwith "Fake error. Sad!"*)
        // #endregion

        logger.Information("TODO-NMB...")

    with | exn -> logger.Error("Unexpected error: {message}\n{stackTrace}", exn.Message, exn.StackTrace)

    // #region "Press any key to exit..."
    writeNewLine "Press any key to exit..." ConsoleColor.Magenta
    Console.ReadKey() |> ignore
    writeBlankLine()
    0
    // #endregion
