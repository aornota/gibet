module Aornota.Gibet.Ui.Program.Run

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.UI.Common.Marked
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.Render
open Aornota.Gibet.Ui.Program.State

open Elmish
open Elmish.Bridge
#if DEBUG
open Elmish.Debug
#endif
open Elmish.HMR
open Elmish.React

open Fable.Core.JsInterop

createObj [ "sanitize" ==> true ] |> unbox |> Globals.marked.setOptions |> ignore // note: "sanitize" ensures Html rendered as text

let private bridgeConfig =
    Bridge.endpoint BRIDGE_ENDPOINT
    |> Bridge.withMapping RemoteUiInput
    |> Bridge.withWhenDown Disconnected

Program.mkProgram initialize transition render
|> Program.withBridgeConfig bridgeConfig
|> Program.withReactSynchronous "elmish-app" // i.e. "<div id="elmish-app">" in index.html
#if DEBUG
|> Program.withConsoleTrace
|> Program.withDebugger
#endif
|> Program.run
