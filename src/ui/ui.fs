module Aornota.Gibet.Ui

open Aornota.Gibet.Common

open Elmish
#if DEBUG
open Elmish.Debug
#endif
open Elmish.HMR
open Elmish.React

open Fable.React
open Fable.React.Props

open Fable.Remoting.Client

open Fulma

// TODO-NMB?...open Thoth.Json

type State = { Counter : Counter option }

type Input =
    | Increment
    | Decrement
    | InitialCountLoaded of Result<Counter, exn>

module Server =
    let api : ICounterApi =
        Remoting.createApi()
        |> Remoting.withRouteBuilder Route.builder
        |> Remoting.buildProxy<ICounterApi>

let initialCounter = Server.api.initialCounter

let initialize () : State * Cmd<Input> =
    let state = { Counter = None }
    let loadCountCmd =
        Cmd.OfAsync.either
            initialCounter
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
    | _, InitialCountLoaded (Ok initialCount) ->
        let nextModel = { Counter = Some initialCount }
        nextModel, Cmd.none
    | _ -> state, Cmd.none

let safeComponents =
    let components =
        span [] [
            a [ Href "https://github.com/giraffe-fsharp/Giraffe/" ] [ str "Giraffe" ]
            str ", "
            a [ Href "http://fable.io/" ] [ str "Fable" ]
            str ", "
            a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
            str ", "
            a [ Href "https://fulma.github.io/Fulma/" ] [ str "Fulma" ]
            str ", "
            a [ Href "https://zaid-ajaj.github.io/Fable.Remoting/" ] [ str "Fable.Remoting" ]
        ]
    p  [] [
        strong [] [ str "gibet (α)" ]
        str " powered by: "
        components
    ]

let show = function
    | { Counter = Some counter } -> string counter.Value
    | { Counter = None   } -> "Loading..."

let button txt onClick =
    Button.button [
        Button.IsFullWidth
        Button.Color IsPrimary
        Button.OnClick onClick
    ] [ str txt ]

let render (state : State) (dispatch : Input -> unit) =
    div [] [
        Navbar.navbar [ Navbar.Color IsPrimary ] [
            Navbar.Item.div [] [
                Heading.h2 [] [ str "gibet (α)" ]
            ]
        ]
        Container.container [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [
                Heading.h3 [] [ str ("Press buttons to manipulate counter: " + show state) ]
            ]
            Columns.columns [] [
                Column.column [] [ button "-" (fun _ -> dispatch Decrement) ]
                Column.column [] [ button "+" (fun _ -> dispatch Increment) ]
            ]
        ]
        Footer.footer [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [
                safeComponents
            ]
        ]
    ]

Program.mkProgram initialize transition render
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
