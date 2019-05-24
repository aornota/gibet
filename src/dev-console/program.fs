module Aornota.Gibet.DevConsole.Program

open Aornota.Gibet.DevConsole.Console
open Aornota.Gibet.DevConsole.TestTaggingRegex
open Aornota.Gibet.DevConsole.TestUserRepoAndApi
open Aornota.Gibet.Server.Logger

open System

open Serilog

let [<Literal>] private SOURCE = "DevConsole.Program"

Log.Logger <- defaultLogger "logs/dev-console_{Date}.log"

let private logger = Log.Logger |> sourcedLogger SOURCE

let private mainAsync argv = async {
    // #region "Running DevConsole.Program.mainAsync..."
    writeNewLine (sprintf "Running %s.mainAsync" SOURCE) ConsoleColor.Magenta
    write (sprintf " %A" argv) ConsoleColor.DarkMagenta
    write "...\n\n" ConsoleColor.Magenta
    // #endregion

    try
        // #region Logging examples
        (* let test = Some 3.14
        logger.Debug(sourced "This is a debug message" LOG_SOURCE)
        logger.Information(sourced "This is an information message: {test}" LOG_SOURCE, test)
        logger.Warning(sourced "This is a warning message" LOG_SOURCE)
        failwith "Fake error. Sad!" *)
        // #endregion

        // #region testTaggingRegex
        testTaggingRegex ()
        // #endregion

        // #region testUserRepoAndApi
        //match! testUserRepoAndApi () with | Ok _ -> () | Error error -> failwith error
        // #endregion
    with | exn -> logger.Error("Unexpected error: {errorMessage}\n{stackTrace}", exn.Message, exn.StackTrace)

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
