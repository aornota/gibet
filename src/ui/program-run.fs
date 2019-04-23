module Aornota.Gibet.Ui.Program.Run

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.UI.Common.Marked
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

createObj [ "sanitize" ==> true ] |> unbox |> Globals.marked.setOptions |> ignore // note: "sanitize" ensures Html rendered as text

let [<Literal>] private SECONDS_PER_TICK = 1<second/tick> // "ignored" if less than 1<second>

let private onTick (_:State) =
    let secondsPerTick = if SECONDS_PER_TICK >= 1<second/tick> then SECONDS_PER_TICK else 1<second/tick>
    let millisecondsPerTick = ((secondsPerTick |> float) * 1.<second/tick>) * MILLISECONDS_PER_SECOND
    fun dispatch ->
        Browser.Dom.window.setInterval((fun _ -> OnTick |> dispatch), millisecondsPerTick |> int) |> ignore
    |> Cmd.ofSub
let private onMouseMove (_:State) =
    fun dispatch ->
        Browser.Dom.window.addEventListener("mousemove", (fun _ -> OnMouseMove |> dispatch))
    |> Cmd.ofSub

// #region subscriptions
let private subscriptions state =
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
|> Program.withConsoleTrace
|> Program.withDebugger
#endif
|> Program.run
