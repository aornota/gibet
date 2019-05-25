module Aornota.Gibet.DevConsole.Program

open Aornota.Gibet.DevConsole.Console
open Aornota.Gibet.DevConsole.TestTaggingRegex
open Aornota.Gibet.DevConsole.TestUserRepoAndApi
open Aornota.Gibet.Server.SourcedLogger

open System

open Microsoft.Extensions.Configuration

open Giraffe.SerilogExtensions

open Serilog

let [<Literal>] private SOURCE = "DevConsole.Program"

let private configuration =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", false)
        .AddJsonFile("appsettings.development.json", true)
        .Build()

do Log.Logger <-
    LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .Destructure.FSharpTypes()
        .CreateLogger()

let private logger = Log.Logger
let private sourcedLogger = logger |> sourcedLogger SOURCE

let private mainAsync argv = async {
    // #region "Running SOURCE.mainAsync..."
    writeNewLine (sprintf "Running %s.mainAsync" SOURCE) ConsoleColor.Yellow
    write (sprintf " %A" argv) ConsoleColor.DarkYellow
    write "...\n\n" ConsoleColor.Yellow
    // #endregion

    try
        // #region Logging examples
        (* let test = Some 3.14
        sourcedLogger.Debug("This is a debug message")
        sourcedLogger.Information("This is an information message: {test}", test)
        sourcedLogger.Warning("This is a warning message")
        failwith "Fake error. Sad!" *)
        // #endregion

        // #region testTaggingRegex
        (* testTaggingRegex logger *)
        // #endregion

        // #region testUserRepoAndApi
        match! testUserRepoAndApi configuration logger with | Ok _ -> () | Error error -> failwith error
        // #endregion
    with | exn -> sourcedLogger.Error("Unexpected error: {errorMessage}\n{stackTrace}", exn.Message, exn.StackTrace)

    // #region "Press any key to exit..."
    writeNewLine "Press any key to exit..." ConsoleColor.Yellow
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
