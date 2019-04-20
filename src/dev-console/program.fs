module Aornota.Gibet.DevConsole.Program

open Aornota.Gibet.DevConsole.Console
open Aornota.Gibet.DevConsole.Logger
//open Aornota.Gibet.DevConsole.TestAgent

open System

open Serilog

let private mainAsync argv = async {
    // #region "Running Aornota.Gibet.DevConsole.main..."
    writeNewLine "Running Aornota.Gibet.DevConsole.Program.mainAsync" ConsoleColor.Magenta
    write (sprintf " %A" argv) ConsoleColor.DarkMagenta
    write "...\n\n" ConsoleColor.Magenta
    // #endregion

    use logger = createLogger()

    try
        // #region Logging examples
        (* let test = Some 3.14
        logger.Debug("This is a debug message")
        logger.Information("This is an information message: {test}", test)
        logger.Warning("This is a warning message")
        failwith "Fake error. Sad!" *)
        // #endregion
        // #region TestAgent and ResilientMailbox<'T>
        (* let initialValue = 5
        logger.Debug("Creating TestAgent with initial value {initialValue}", initialValue)
        let testAgent = TestAgent(initialValue, logger)
        logger.Debug("Posting Increment to TestAgent")
        testAgent.Increment()
        logger.Debug("Posting Increment to TestAgent")
        testAgent.Increment()
        logger.Debug("Posting Fail to TestAgent")
        testAgent.Fail()
        logger.Debug("Posting Decrement to TestAgent")
        testAgent.Decrement()
        do! Async.Sleep 2500 *)
        // #endregion

        logger.Information("TODO-NMB...")

    with | exn -> logger.Error("Unexpected error: {message}\n{stackTrace}", exn.Message, exn.StackTrace)

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
