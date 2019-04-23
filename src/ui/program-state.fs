module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.UI.Common.LocalStorage
open Aornota.Gibet.UI.Common.RemoteData
open Aornota.Gibet.UI.Common.Toast
open Aornota.Gibet.UI.Common.Theme
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.ServerApi

open Elmish
open Elmish.Bridge

open Fable.Import

open Thoth.Json
open System.Data

let [<Literal>] private APP_PREFERENCES_KEY = "gibet-ui-app-preferences"

let private setBodyClass theme =
    Browser.document.body.className <- theme |> themeClass

let private preferencesOrDefault state =
    let appState, lastUser =
        match state with
        | InitializingConnection _ | ReadingPreferences -> None, None
        | RegisteringConnection registeringConnectionState -> registeringConnectionState.AppState |> Some, None
        | AutomaticallySigningIn automaticallySigningInState -> automaticallySigningInState.AppState |> Some, automaticallySigningInState.LastUser |> Some
        | Unauth unauthState -> unauthState.AppState |> Some, None
        | Auth authState -> authState.AppState |> Some, (authState.AuthUser.User.UserName, authState.AuthUser.Jwt) |> Some
    let affinityId, theme =
        match appState with
        | Some appState -> appState.AffinityId, appState.Theme
        | None -> AffinityId.Create(), Light
    {
        AffinityId = affinityId
        Theme = theme
        LastUser = lastUser
    }

let private readPreferencesCmd =
    let readPreferences() = async {
        (* TEMP-NMB...
        do! ifDebugSleepAsync 20 400 *)
        return APP_PREFERENCES_KEY |> Key  |> readJson |> Option.map (fun (Json json) -> json |> Decode.Auto.fromString<Preferences>) }
    Cmd.OfAsync.either readPreferences () (ReadPreferencesResult >> PreferencesInput) (ReadPreferencesExn >> PreferencesInput)
let private writePreferencesCmd preferences =
    let writePreferences preferences = async {
        Encode.Auto.toString<Preferences>(4, preferences) |> Json |> writeJson (APP_PREFERENCES_KEY |> Key) }
    Cmd.OfAsync.either writePreferences preferences (WritePreferencesOk >> PreferencesInput) (WritePreferencesExn >> PreferencesInput)

let private registeringConnectionState(appState, lastUser) = {
    AppState = appState
    LastUser = lastUser }
let private automaticallySigningInState(appState, connectionState, lastUser) = {
    AppState = appState
    ConnectionState = connectionState
    LastUser = lastUser }
let private unauthState(appState, connectionState, signInError) = {
    AppState = appState
    ConnectionState = connectionState
    SigningIn = false
    SignInError = signInError }
let private authState(appState, connectionState, authUser, mustChangePasswordReason) = {
    AppState = appState
    ConnectionState = connectionState
    AuthUser = authUser
    MustChangePasswordReason = mustChangePasswordReason
    SigningOut = false
    UsersData = NotRequested }

let private updateAppState appState state =
    match state with
    | InitializingConnection _ | ReadingPreferences -> state
    | RegisteringConnection registeringConnectionState -> { registeringConnectionState with AppState = appState } |> RegisteringConnection
    | AutomaticallySigningIn automaticallySigningInState -> { automaticallySigningInState with AppState = appState } |> AutomaticallySigningIn
    | Unauth unauthState -> { unauthState with AppState = appState } |> Unauth
    | Auth authState -> { authState with AppState = appState } |> Auth

let initialize() : State * Cmd<Input> =
    false |> InitializingConnection, Cmd.none

let transition input state : State * Cmd<Input> =
    let appState =
        match state with
        | InitializingConnection _ | ReadingPreferences  -> None
        | RegisteringConnection registeringConnectionState -> registeringConnectionState.AppState |> Some
        | AutomaticallySigningIn automaticallySigningInState -> automaticallySigningInState.AppState |> Some
        | Unauth unauthState -> unauthState.AppState |> Some
        | Auth authState -> authState.AppState |> Some
    match input, state, appState with
    // #region RegisterConnection | RemoteUiInput | Disconnected
    | RegisterConnection (appState, lastUser), _, _ ->
        appState.AffinityId |> RemoteServerInput.Register |> Bridge.Send
        (appState, lastUser) |> registeringConnectionState |> RegisteringConnection, Cmd.none
    | RemoteUiInput(Registered(connectionId, serverStarted)), RegisteringConnection registeringConnectionState, _ ->
        let connectionState = { Connection = (connectionId, registeringConnectionState.AppState.AffinityId) ; ServerStarted = serverStarted }
        match registeringConnectionState.LastUser with
        | Some (userName, jwt) ->
            let cmd = Cmd.OfAsync.either userApi.autoSignIn (connectionState.Connection, jwt) AutoSignInResult AutoSignInExn |> Cmd.map SignInInput
            (registeringConnectionState.AppState, connectionState, (userName, jwt)) |> automaticallySigningInState |> AutomaticallySigningIn, cmd
        | None ->
            (registeringConnectionState.AppState, connectionState, None) |> unauthState |> Unauth, Cmd.none
    | RemoteUiInput(Initialized), InitializingConnection _, _ ->
        ReadingPreferences, readPreferencesCmd
    // TODO-NMB: More RemoteUiInput...
    | Disconnected, _, _ -> // TODO-NMB: Rethink what to do on disconnection?...
        true |> InitializingConnection, Cmd.none
    // #endregion
    // #region PreferencesInput
    | PreferencesInput(ReadPreferencesResult(Some(Ok preferences))), ReadingPreferences, _ ->
        let appState = {
            AffinityId = preferences.AffinityId
            Theme = preferences.Theme
            NavbarBurgerIsActive = false }
        appState.Theme |> setBodyClass
        state, (appState, preferences.LastUser) |> RegisterConnection |> Cmd.ofMsg
    | PreferencesInput(ReadPreferencesResult None), ReadingPreferences, _ ->
        let preferences = state |> preferencesOrDefault
        let cmds =
            Cmd.batch [
               preferences |> writePreferencesCmd
               preferences |> Ok |> Some |> ReadPreferencesResult |> PreferencesInput |> Cmd.ofMsg
            ]
        state, cmds
    | PreferencesInput(ReadPreferencesResult(Some(Error error))), ReadingPreferences, _ -> // TODO-NMB: Call addDebugError (no need for toast)?...
        sprintf "ReadPreferencesResult -> %s" error |> Browser.console.log
        state, None |> ReadPreferencesResult |> PreferencesInput |> Cmd.ofMsg
    | PreferencesInput(ReadPreferencesExn exn), ReadingPreferences, _ ->
        state, exn.Message |> Error |> Some |> ReadPreferencesResult |> PreferencesInput |> Cmd.ofMsg
    | PreferencesInput(WritePreferencesOk _), _, _ ->
        state, Cmd.none
    | PreferencesInput(WritePreferencesExn exn), _, _ -> // TODO-NMB: Call addDebugError (no need for toast)?...
        sprintf "WritePreferencesExn -> %s" exn.Message |> Browser.console.log
        state, Cmd.none
    // #endregion
    | ToggleTheme, _, Some appState ->
        let appState = { appState with Theme = match appState.Theme with | Light -> Dark | Dark -> Light }
        appState.Theme |> setBodyClass
        state |> updateAppState appState, state |> preferencesOrDefault |> writePreferencesCmd
    | ToggleNavbarBurger, _, Some appState ->
        let appState = { appState with NavbarBurgerIsActive = appState.NavbarBurgerIsActive |> not }
        state |> updateAppState appState, state |> preferencesOrDefault |> writePreferencesCmd
    // TEMP-NMB...
    | TempSignIn, Unauth unauthState, _ ->
        let userName, password = "neph" |> UserName, "neph" |> Password
        let cmd = Cmd.OfAsync.either userApi.signIn (unauthState.ConnectionState.Connection, userName, password) SignInResult SignInExn |> Cmd.map SignInInput
        { unauthState with SigningIn = true ; SignInError = None } |> Unauth, cmd
    // ...TEMP-NMB
    // #region SignInInput
    | SignInInput(AutoSignInResult(Ok(authUser, mustChangePasswordReason))), AutomaticallySigningIn automaticallySigningInState, _ -> // TODO-NMB: Toast | auto-request UsersData?...
        (automaticallySigningInState.AppState, automaticallySigningInState.ConnectionState, authUser, mustChangePasswordReason) |> authState |> Auth, Cmd.none
    | SignInInput(AutoSignInResult(Error error)), AutomaticallySigningIn automaticallySigningInState, _ -> // TODO-NMB: Toast?...
        (automaticallySigningInState.AppState, automaticallySigningInState.ConnectionState, error |> Some) |> unauthState |> Unauth, Cmd.none
    | SignInInput(AutoSignInExn exn), Unauth _, _ ->
        state, exn.Message |> Error |> AutoSignInResult |> SignInInput |> Cmd.ofMsg
    | SignInInput(SignInResult(Ok(authUser, mustChangePasswordReason))), Unauth unauthState, _ -> // TODO-NMB: Toast | auto-request UsersData?...
        (unauthState.AppState, unauthState.ConnectionState, authUser, mustChangePasswordReason) |> authState |> Auth, Cmd.none
    | SignInInput(SignInResult(Error error)), Unauth unauthState, _ -> // TODO-NMB: Toast?...
        { unauthState with SigningIn = false ; SignInError = error |> Some } |> Unauth, Cmd.none
    | SignInInput(SignInExn exn), Unauth _, _ ->
        state, exn.Message |> Error |> SignInResult |> SignInInput |> Cmd.ofMsg
    // #endregion
    // TEMP-NMB...
    | TempSignOut, Auth authState, _ ->
        let cmd = Cmd.OfAsync.either userApi.signOut (authState.ConnectionState.Connection, authState.AuthUser.Jwt) SignOutResult SignOutExn |> Cmd.map SignOutInput
        { authState with SigningOut = true } |> Auth, cmd
    // ...TEMP-NMB
    // #region SignOutInput
    | SignOutInput(SignOutResult(Ok _)), Auth authState, _ -> // TODO-NMB: Toast...
        (authState.AppState, authState.ConnectionState, None) |> unauthState |> Unauth, Cmd.none
    | SignOutInput(SignOutResult(Error error)), Auth _, _ -> // TODO-NMB: Call addDebugError (no need for toast)?...
        sprintf "SignOutResult -> %s" error |> Browser.console.log
        state, () |> Ok |> SignOutResult |> SignOutInput |> Cmd.ofMsg
    | SignOutInput(SignOutExn exn), Auth _, _ ->
        state, exn.Message |> Error |> SignOutResult |> SignOutInput |> Cmd.ofMsg
    // #endregion
    // TEMP-NMB...
    (*
    | TempGetUsers, Received(authUser, _), NotRequested _ | TempGetUsers, Received(authUser, _), Failed _ | TempGetUsers, Received(authUser, _), Received _ ->
        let connection = ConnectionId.Create(), AffinityId.Create() // TEMP-NMB...
        let cmd = Cmd.OfAsync.either userApi.getUsers (connection, authUser.Jwt) TempGetUsersResult TempGetUsersExn
        { state with UsersData = Pending }, cmd
    | TempGetUsersResult(Ok users), Received _, Pending ->
        { state with UsersData = users |> Received }, Cmd.none
    | TempGetUsersResult(Error error), Received _, Pending ->
        { state with UsersData = error |> Failed }, Cmd.none
    | TempGetUsersExn exn, Received _, Pending ->
        state, exn.Message |> Error |> TempGetUsersResult |> Cmd.ofMsg *)
    // ...TEMP-NMB
    | _ -> // TODO-NMB: Call shouldNeverHappen?...
        sprintf "Unexpected input when %A -> %A" state input |> Browser.console.log
        state, Cmd.none
