module Aornota.Gibet.Ui.Program.Run

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Marked
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.Render
open Aornota.Gibet.Ui.Program.State

open Elmish
open Elmish.Bridge
open Elmish.Debug
open Elmish.React
#if HMR
open Elmish.HMR // note: needs to be last open Elmish.Xyz (see https://elmish.github.io/hmr/)
#endif

open Fable.Core.JsInterop

let [<Literal>] private SECONDS_PER_TICK = 1<second/tick> // note: "ignored" if less than 1<second>

Globals.marked.setOptions(unbox(createObj [ "sanitize" ==> true ])) |> ignore // note: "sanitize" ensures Html within Markdown rendered as text

let private millisecondsPerTick = float(max SECONDS_PER_TICK 1<second/tick>) * MILLISECONDS_PER_SECOND

let private onTick (_:State) =
    fun dispatch ->
        Browser.Dom.window.setInterval((fun _ -> dispatch OnTick), int millisecondsPerTick) |> ignore
    |> Cmd.ofSub
let private onMouseMove (_:State) =
    fun dispatch ->
        Browser.Dom.window.addEventListener("mousemove", (fun _ -> dispatch OnMouseMove))
    |> Cmd.ofSub

// #region subscriptions
let private subscriptions (state:State) : Cmd<Input> =
    Cmd.batch [
#if TICK
        state |> onTick
#endif
#if ACTIVITY
        state |> onMouseMove
#endif
    ]
// #endregion

let private bridgeConfig =
    Bridge.endpoint BRIDGE_ENDPOINT
    |> Bridge.withMapping RemoteUiInput
    |> Bridge.withWhenDown Disconnected

Program.mkProgram initialize transition render
|> Program.withBridgeConfig bridgeConfig
|> Program.withSubscription subscriptions
|> Program.withReactSynchronous "elmish-app" // i.e. <div id="elmish-app"> in index.html
#if DEBUG
(* Note: Rather than using Program.withConsoleTrace, logging is handled "manually" in State.initialize and State.transition, which allows selective logging of inputs (e.g. suppressing
   logging for OnTick | OnMouseMove | &c.). *)
|> Program.withDebugger
#endif
|> Program.run
