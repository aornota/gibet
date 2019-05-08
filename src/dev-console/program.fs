module Aornota.Gibet.DevConsole.Program

open Aornota.Gibet.DevConsole.Console
open Aornota.Gibet.DevConsole.TestUserRepoAndApi
open Aornota.Gibet.Server.Logger

open System

open Serilog

let [<Literal>] private LOG_SOURCE = "DevConsole.Program"

Log.Logger <- createLogger "logs/dev-console_{Date}.log"

let private mainAsync argv = async {
    // #region "Running DevConsole.Program.mainAsync..."
    writeNewLine "Running DevConsole.Program.mainAsync" ConsoleColor.Magenta
    write (sprintf " %A" argv) ConsoleColor.DarkMagenta
    write "...\n\n" ConsoleColor.Magenta
    // #endregion

    try
        // #region Logging examples
        (* let test = Some 3.14
        Log.Logger.Debug(sourced "This is a debug message" LOG_SOURCE)
        Log.Logger.Information(sourced "This is an information message: {test}" LOG_SOURCE, test)
        Log.Logger.Warning(sourced "This is a warning message" LOG_SOURCE)
        failwith "Fake error. Sad!" *)
        // #endregion

        // #region testUserRepoAndApi
        match! testUserRepoAndApi() with | Ok _ -> () | Error error -> failwith error
        // #endregion

        // TEMP-NMB...Log.Logger.Information(sourced "TODO-NMB..." LOG_SOURCE)

    with | exn -> Log.Logger.Error(sourced "Unexpected error: {message}\n{stackTrace}" LOG_SOURCE, exn.Message, exn.StackTrace)

    // #region "Press any key to exit..."
    writeNewLine "Press any key to exit..." ConsoleColor.Magenta
    Console.ReadKey() |> ignore
    writeBlankLine()
    return 0
    // #endregion
}

[<EntryPoint>]
let main argv =
    async {
        do! Async.SwitchToThreadPool()
        return! mainAsync argv
    } |> Async.RunSynchronously
