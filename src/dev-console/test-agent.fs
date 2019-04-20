module Aornota.Gibet.DevConsole.TestAgent

open Aornota.Gibet.DevConsole.ResilientMailbox

open Serilog

type private TestInput =
    | Increment
    | Decrement
    | Fail

type TestAgent(initialValue:int, logger:ILogger) =
    let agent = ResilientMailbox<_>.Start(fun inbox ->
        let rec loop value = async {
            match! inbox.Receive() with
            | Increment ->
                let value = value + 1
                logger.Warning("Value incremented: {value}", value)
                return! loop value
            | Decrement ->
                let value = value - 1
                logger.Warning("Value decremented: {value}", value)
                return! loop value
            | Fail ->
                logger.Warning("About to fail...")
                failwithf "Failed (value %i)" value
        }
        logger.Warning("Initial value: {initialValue}", initialValue)
        loop initialValue
    )
    do agent.OnError.Add (fun exn -> logger.Error("Unexpected error: {message}", exn.Message))
    member __.Increment() = Increment |> agent.Post
    member __.Decrement() = Decrement |> agent.Post
    member __.Fail() = Fail |> agent.Post
