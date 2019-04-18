module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Domain.Counter
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.ServerApi

open Elmish

let initialize() : State * Cmd<Input> =
    let state = { Counter = None }
    let loadCountCmd =
        Cmd.OfAsync.either
            counterApi.initialCounter
            ()
            (Ok >> InitialCountLoaded)
            (Error >> InitialCountLoaded)
    state, loadCountCmd

let transition (input : Input) (state : State) : State * Cmd<Input> =
    match state.Counter, input with
    | Some counter, Increment ->
        let nextModel = { state with Counter = Some { Value = counter.Value + 1 } }
        nextModel, Cmd.none
    | Some counter, Decrement ->
        let nextModel = { state with Counter = Some { Value = counter.Value - 1 } }
        nextModel, Cmd.none
    | _, InitialCountLoaded(Ok initialCount) ->
        let nextModel = { Counter = Some initialCount }
        nextModel, Cmd.none
    | _ -> state, Cmd.none
