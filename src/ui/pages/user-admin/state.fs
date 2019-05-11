module Aornota.Gibet.Ui.Pages.UserAdmin.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Pages.UserAdmin.Common
open Aornota.Gibet.Ui.User.Shared
open Aornota.Gibet.Ui.UserApi

open Elmish

let initialize (authUser:AuthUser) : State * Cmd<Input> =
    { ToDo = System.String.Empty }, Cmd.none

let transition (authUser:AuthUser) (usersData:RemoteData<UserData list, string>) (input:Input) (state:State) : State * Cmd<Input> =
    match input with
    | ToDo -> state, Cmd.none
