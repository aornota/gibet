module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.UI.Common.RemoteData
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.ServerApi

open Elmish

let initialize() : State * Cmd<Input> =
    let state = {
        AuthUserData = NotRequested
        UsersData = NotRequested }
    state, Cmd.none

let transition input state : State * Cmd<Input> =
    match input, state.AuthUserData, state.UsersData with
    | SignIn, NotRequested _, _ | SignIn, Failed _, _ ->
        let neph, nephPassword = "neph" |> UserName, "neph" |> Password
        let cmd = Cmd.OfAsync.either userApi.signIn (neph, nephPassword) (Ok >> SignInResult) (Error >> SignInResult)
        { state with AuthUserData = Pending }, cmd
    | SignInResult(Ok(Ok(authUser, mustChangePasswordReason))), Pending, _ ->
        { state with AuthUserData = (authUser, mustChangePasswordReason) |> Received }, Cmd.none
    | SignInResult(Ok(Error error)), Pending, _ ->
        { state with AuthUserData = error |> Failed }, Cmd.none
    | SignInResult(Error exn), Pending, _ ->
        state, exn.Message |> Error |> Ok |> SignInResult |> Cmd.ofMsg
    | GetUsers, Received(authUser, _), NotRequested _ | GetUsers, Received(authUser, _), Failed _ | GetUsers, Received(authUser, _), Received _ ->
        let cmd = Cmd.OfAsync.either userApi.getUsers authUser.Jwt (Ok >> GetUsersResult) (Error >> GetUsersResult)
        { state with UsersData = Pending }, cmd
    | GetUsersResult(Ok(Ok users)), Received _, Pending ->
        { state with UsersData = users |> Received }, Cmd.none
    | GetUsersResult(Ok(Error error)), Received _, Pending ->
        { state with UsersData = error |> Failed }, Cmd.none
    | GetUsersResult(Error exn), Received _, Pending ->
        state, exn.Message |> Error |> Ok |> GetUsersResult |> Cmd.ofMsg
    | _ ->
        state, Cmd.none // silently ignore anything unexpected (though could show "debug message" &c.)
