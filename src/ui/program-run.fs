module Aornota.Gibet.Ui.Program.Run

open Aornota.Gibet.UI.Common.Marked
open Aornota.Gibet.Ui.Program.State
open Aornota.Gibet.Ui.Program.Render

open Elmish
open Elmish.Bridge
#if DEBUG
open Elmish.Debug
#endif
open Elmish.HMR
open Elmish.React

open Fable.Core.JsInterop

Globals.marked.setOptions(unbox(createObj [ "sanitize" ==> true ])) |> ignore // note: "sanitize" ensures Html rendered as text

Program.mkProgram initialize transition render
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
