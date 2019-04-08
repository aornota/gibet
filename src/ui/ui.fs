module Aornota.Gibet.Ui

open Elmish
open Elmish.React
open Elmish.HMR

open Fable.React
open Fable.React.Props

open Thoth.Json

open Aornota.Gibet.Common

open Fulma

// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type State = { Counter: Counter option }

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Input =
| Increment
| Decrement
| InitialCountLoaded of Result<Counter, exn>

module Server =

    open Fable.Remoting.Client

    /// A proxy you can use to talk to server directly
    let api : ICounterApi =
      Remoting.createApi()
      |> Remoting.withRouteBuilder Route.builder
      |> Remoting.buildProxy<ICounterApi>

let initialCounter = Server.api.initialCounter

// defines the initial state and initial command (= side-effect) of the application
let initialize () : State * Cmd<Input> =
    let state = { Counter = None }
    let loadCountCmd =
        Cmd.OfAsync.either
            initialCounter
            ()
            (Ok >> InitialCountLoaded)
            (Error >> InitialCountLoaded)
    state, loadCountCmd

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let transition (input : Input) (state : State) : State * Cmd<Input> =
    match state.Counter, input with
    | Some counter, Increment ->
        let nextModel = { state with Counter = Some { Value = counter.Value + 1 } }
        nextModel, Cmd.none
    | Some counter, Decrement ->
        let nextModel = { state with Counter = Some { Value = counter.Value - 1 } }
        nextModel, Cmd.none
    | _, InitialCountLoaded (Ok initialCount)->
        let nextModel = { Counter = Some initialCount }
        nextModel, Cmd.none
    | _ -> state, Cmd.none

let safeComponents =
    let components =
        span [ ]
           [
             a [ Href "https://github.com/giraffe-fsharp/Giraffe" ] [ str "Giraffe" ]
             str ", "
             a [ Href "http://fable.io" ] [ str "Fable" ]
             str ", "
             a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
             str ", "
             a [ Href "https://fulma.github.io/Fulma" ] [ str "Fulma" ]
             str ", "
             a [ Href "https://zaid-ajaj.github.io/Fable.Remoting/" ] [ str "Fable.Remoting" ]
           ]

    p [ ]
        [ strong [] [ str "gibet (α)" ]
          str " powered by: "
          components ]

let show = function
| { Counter = Some counter } -> string counter.Value
| { Counter = None   } -> "Loading..."

let button txt onClick =
    Button.button
        [ Button.IsFullWidth
          Button.Color IsPrimary
          Button.OnClick onClick ]
        [ str txt ]

let render (state : State) (dispatch : Input -> unit) =
    div []
        [ Navbar.navbar [ Navbar.Color IsPrimary ]
            [ Navbar.Item.div [ ]
                [ Heading.h2 [ ]
                    [ str "gibet (α)" ] ] ]

          Container.container []
              [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ Heading.h3 [] [ str ("Press buttons to manipulate counter: " + show state) ] ]
                Columns.columns []
                    [ Column.column [] [ button "-" (fun _ -> dispatch Decrement) ]
                      Column.column [] [ button "+" (fun _ -> dispatch Increment) ] ] ]

          Footer.footer [ ]
                [ Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ]
                    [ safeComponents ] ] ]

#if DEBUG
open Elmish.Debug
#endif

Program.mkProgram initialize transition render
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
