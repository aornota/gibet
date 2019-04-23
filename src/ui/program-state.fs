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

let [<Literal>] private APP_PREFERENCES_KEY = "gibet-ui-app-preferences"

let private setBodyClass theme =
    Browser.document.body.className <- theme |> themeClass

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
let private writePreferencesOrDefault state =
    let preferences = state |> preferencesOrDefault
    state, preferences |> writePreferencesCmd

let private getUsersCmd(connection, jwt) =
    Cmd.OfAsync.either userApi.getUsers (connection, jwt) GetUsersResult GetUsersExn |> Cmd.map GetUsersInput

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
        let cmds = Cmd.batch [
            preferences |> writePreferencesCmd
            preferences |> Ok |> Some |> ReadPreferencesResult |> PreferencesInput |> Cmd.ofMsg ]
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
    // #region ToggleTheme | ToggleNavbarBurger
    | ToggleTheme, _, Some appState ->
        let appState = { appState with Theme = match appState.Theme with | Light -> Dark | Dark -> Light }
        appState.Theme |> setBodyClass
        state |> updateAppState appState |> writePreferencesOrDefault
    | ToggleNavbarBurger, _, Some appState ->
        let appState = { appState with NavbarBurgerIsActive = appState.NavbarBurgerIsActive |> not }
        state |> updateAppState appState |> writePreferencesOrDefault
    // #endregion
    | TempSignIn, Unauth unauthState, _ -> // TEMP-NMB...
        let userName, password = "neph" |> UserName, "neph" |> Password
        let cmd = Cmd.OfAsync.either userApi.signIn (unauthState.ConnectionState.Connection, userName, password) SignInResult SignInExn |> Cmd.map SignInInput
        { unauthState with SigningIn = true ; SignInError = None } |> Unauth, cmd
    // #region SignInInput
    | SignInInput(AutoSignInResult(Ok(authUser, mustChangePasswordReason))), AutomaticallySigningIn automaticallySigningInState, _ ->
        let authState = (automaticallySigningInState.AppState, automaticallySigningInState.ConnectionState, authUser, mustChangePasswordReason) |> authState
        let (UserName userName) = authUser.User.UserName
        let cmds = Cmd.batch [
            sprintf "You have been automatically signed in as <strong>%s</strong>" userName |> successToastCmd
            authState |> Auth |> preferencesOrDefault |> writePreferencesCmd
            (authState.ConnectionState.Connection, authState.AuthUser.Jwt) |> getUsersCmd ]
        { authState with UsersData = Pending } |> Auth, cmds
    | SignInInput(AutoSignInResult(Error error)), AutomaticallySigningIn automaticallySigningInState, _ ->
        let state = (automaticallySigningInState.AppState, automaticallySigningInState.ConnectionState, error |> Some) |> unauthState |> Unauth
        let (UserName userName), _ = automaticallySigningInState.LastUser
        let cmds = Cmd.batch [
            sprintf "Unable to automatically sign in as <strong>%s</strong>" userName |> warningToastCmd
            state |> preferencesOrDefault |> writePreferencesCmd ]
        state, cmds
    | SignInInput(AutoSignInExn exn), Unauth _, _ ->
        state, exn.Message |> Error |> AutoSignInResult |> SignInInput |> Cmd.ofMsg
    | SignInInput(SignInResult(Ok(authUser, mustChangePasswordReason))), Unauth unauthState, _ ->
        let authState = (unauthState.AppState, unauthState.ConnectionState, authUser, mustChangePasswordReason) |> authState
        let (UserName userName) = authUser.User.UserName
        let cmds = Cmd.batch [
            sprintf "You have signed in as <strong>%s</strong>" userName |> successToastCmd
            authState |> Auth |> preferencesOrDefault |> writePreferencesCmd
            (authState.ConnectionState.Connection, authState.AuthUser.Jwt) |> getUsersCmd ]
        { authState with UsersData = Pending } |> Auth, cmds
    | SignInInput(SignInResult(Error error)), Unauth unauthState, _ ->
        let state = { unauthState with SigningIn = false ; SignInError = error |> Some } |> Unauth
        state, Cmd.none // no need for toast (since error will be displayed on SignInModal)
    | SignInInput(SignInExn exn), Unauth _, _ ->
        state, exn.Message |> Error |> SignInResult |> SignInInput |> Cmd.ofMsg
    // #endregion
    | TempSignOut, Auth authState, _ -> // TEMP-NMB...
        let cmd = Cmd.OfAsync.either userApi.signOut (authState.ConnectionState.Connection, authState.AuthUser.Jwt) SignOutResult SignOutExn |> Cmd.map SignOutInput
        { authState with SigningOut = true } |> Auth, cmd
    // #region SignOutInput
    | SignOutInput(SignOutResult(Ok _)), Auth authState, _ ->
        let state = (authState.AppState, authState.ConnectionState, None) |> unauthState |> Unauth
        let cmds = Cmd.batch [
            "You have signed out" |> successToastCmd
            state |> preferencesOrDefault |> writePreferencesCmd ]
        state, cmds
    | SignOutInput(SignOutResult(Error error)), Auth _, _ -> // TODO-NMB: Call addDebugError (no need for toast)?...
        sprintf "SignOutResult -> %s" error |> Browser.console.log
        state, () |> Ok |> SignOutResult |> SignOutInput |> Cmd.ofMsg
    | SignOutInput(SignOutExn exn), Auth _, _ ->
        state, exn.Message |> Error |> SignOutResult |> SignOutInput |> Cmd.ofMsg
    // #endregion
    | TempGetUsers, Auth authState, _ -> // TEMP-NMB...
        let cmd = (authState.ConnectionState.Connection, authState.AuthUser.Jwt) |> getUsersCmd
        { authState with UsersData = Pending } |> Auth, cmd
    // #region GetUsersInput
    | GetUsersInput(GetUsersResult(Ok users)), Auth authState, _ ->
        { authState with UsersData = users |> Received } |> Auth, Cmd.none
    | GetUsersInput(GetUsersResult(Error error)), Auth authState, _ ->
        { authState with UsersData = error |> Failed } |> Auth, Cmd.none
    | GetUsersInput(GetUsersExn exn), Auth _, _ ->
        state, exn.Message |> Error |> GetUsersResult |> GetUsersInput |> Cmd.ofMsg
    // #endregion
    | _ -> // TODO-NMB: Call shouldNeverHappen?...
        sprintf "Unexpected input when %A -> %A" state input |> Browser.console.log
        state, Cmd.none
