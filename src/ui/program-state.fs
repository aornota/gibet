module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.ServerApi

open Elmish

let initialize() : State * Cmd<Input> =
    let state = {
        AuthUser = None
        SignInError = None
        Users = None
        GetUsersError = None }
    state, Cmd.none

let transition input state : State * Cmd<Input> =
    match input, state.AuthUser with
    | SignIn, None ->
        let neph, nephPassword = "neph" |> UserName, "neph" |> Password
        state, Cmd.OfAsync.either userApi.signIn (neph, nephPassword) (Ok >> SignInResult) (Error >> SignInResult)
    | SignInResult(Ok(Ok authUser)), None ->
        { state with AuthUser = authUser |> Some ; SignInError = None }, Cmd.none
    | SignInResult(Ok(Error error)), None ->
        { state with SignInError = error |> Some }, Cmd.none
    | SignInResult(Error exn), None ->
        { state with SignInError = exn.Message |> Some }, Cmd.none
    | GetUsers, Some authUser ->
        state, Cmd.OfAsync.either userApi.getUsers authUser.Jwt (Ok >> GetUsersResult) (Error >> GetUsersResult)
    | GetUsersResult(Ok(Ok users)), Some _ ->
        { state with Users = users |> Some ; GetUsersError = None }, Cmd.none
    | GetUsersResult(Ok(Error error)), Some _ ->
        { state with GetUsersError = error |> Some }, Cmd.none
    | GetUsersResult(Error exn), Some _ ->
        { state with GetUsersError = exn.Message |> Some }, Cmd.none
    | _ ->
        state, Cmd.none // silently ignore anything unexpected (though could show "debug message" &c.)
