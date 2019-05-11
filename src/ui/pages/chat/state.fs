module Aornota.Gibet.Ui.Pages.Chat.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Ui.Pages.Chat.Common
open Aornota.Gibet.Ui.Pages.Chat.ServerApi

open Elmish

let initialize (authUser:AuthUser) : State * Cmd<Input> =
    { ToDo = () }, Cmd.none

let transition (authUser:AuthUser) (input:Input) (state:State) : State * Cmd<Input> =
    match input with
    | ToDo -> state, Cmd.none
