module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.UI.Common.LocalStorage
open Aornota.Gibet.UI.Common.RemoteData
open Aornota.Gibet.UI.Common.ShouldNeverHappen
open Aornota.Gibet.UI.Common.Toast
open Aornota.Gibet.UI.Common.Theme
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.ServerApi

open System

open Elmish
open Elmish.Bridge

open Fable.Import

open Thoth.Elmish
open Thoth.Json

let [<Literal>] private APP_PREFERENCES_KEY = "gibet-ui-app-preferences"
let [<Literal>] private ACTIVITY_DEBOUNCER_THRESHOLD = 15.<second> // "ignored" if less than 5.<second>

let private setBodyClass theme =
    Browser.document.body.className <- themeClass theme

let private readPreferencesCmd =
    let readPreferences() = async {
        (* TEMP-NMB...
        do! ifDebugSleepAsync 20 400 *)
        return APP_PREFERENCES_KEY |> Key |> readJson |> Option.map (fun (Json json) -> Decode.Auto.fromString<Preferences> json) }
    Cmd.OfAsync.either readPreferences () ReadPreferencesResult ReadPreferencesExn |> Cmd.map PreferencesInput
let private writePreferencesCmd preferences =
    let writePreferences preferences = async {
        Encode.Auto.toString<Preferences>(4, preferences) |> Json |> writeJson (APP_PREFERENCES_KEY |> Key) }
    Cmd.OfAsync.either writePreferences preferences WritePreferencesOk WritePreferencesExn |> Cmd.map PreferencesInput

let private preferencesOrDefault state =
    let appState, lastUser =
        match state with
        | InitializingConnection _ | ReadingPreferences _ -> None, None
        | RegisteringConnection registeringConnectionState -> Some registeringConnectionState.AppState, None
        | AutomaticallySigningIn automaticallySigningInState -> Some automaticallySigningInState.AppState, Some automaticallySigningInState.LastUser
        | Unauth unauthState -> Some unauthState.AppState, None
        | Auth authState -> Some authState.AppState, Some(authState.AuthUser.User.UserName, authState.AuthUser.Jwt)
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
    let preferences = preferencesOrDefault state
    state, writePreferencesCmd preferences

let private getUsersCmd connection jwt =
    Cmd.OfAsync.either userApi.getUsers (connection, jwt) GetUsersResult GetUsersExn |> Cmd.map GetUsersInput

let private registeringConnectionState appState lastUser connectionId = {
    AppState = appState
    LastUser = lastUser
    ConnectionId = connectionId }
let private automaticallySigningInState appState connectionState lastUser = {
    AppState = appState
    ConnectionState = connectionState
    LastUser = lastUser }
let private unauthState appState connectionState forcedSignOutReason signInError = {
    AppState = appState
    ConnectionState = connectionState
    ForcedSignOutReason = forcedSignOutReason
    SigningIn = false
    SignInError = signInError }
let private authState appState connectionState authUser mustChangePasswordReason = {
    AppState = appState
    ConnectionState = connectionState
    AuthUser = authUser
    MustChangePasswordReason = mustChangePasswordReason
    ActivityDebouncerState = Debouncer.create()
    SigningOut = false
    UsersData = NotRequested }

let private updateAppState appState state =
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> state
    | RegisteringConnection registeringConnectionState -> RegisteringConnection { registeringConnectionState with AppState = appState }
    | AutomaticallySigningIn automaticallySigningInState -> AutomaticallySigningIn { automaticallySigningInState with AppState = appState }
    | Unauth unauthState -> Unauth { unauthState with AppState = appState }
    | Auth authState -> Auth { authState with AppState = appState }

let private addError debugOnly (error:string) (state:State) = // TODO-NMB: Add to state.Messages...
    if debugOnly then Browser.console.log error
    state
let private addDebugError = addError true
// #region shouldNeverHappen
let private shouldNeverHappen (error:string) (state:State) : State * Cmd<Input> =
#if DEBUG
    state |> addDebugError (shouldNeverHappen error), Cmd.none
#else
    state |> addError false SHOULD_NEVER_HAPPEN, Cmd.none
#endif
// #endregion

let private handleRemoteUiInput input state =
    match input, state with
    | Initialized, InitializingConnection connectionId -> ReadingPreferences connectionId, readPreferencesCmd
    | Registered(connectionId, serverStarted), RegisteringConnection registeringConnectionState ->
        let connectionState = { ConnectionId = connectionId ; ServerStarted = serverStarted }
        match registeringConnectionState.LastUser with
        | Some(userName, jwt) ->
            let cmd = Cmd.OfAsync.either userApi.autoSignIn (connectionState.ConnectionId, jwt) AutoSignInResult AutoSignInExn |> Cmd.map SignInInput
            AutomaticallySigningIn (automaticallySigningInState registeringConnectionState.AppState connectionState (userName, jwt)), cmd
        | None ->
            Unauth(unauthState registeringConnectionState.AppState connectionState None None), Cmd.none
    | UserActivity userId, Auth authState -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        let usersData = authState.UsersData |> updateActivity userId
        Auth { authState with UsersData = usersData }, Cmd.none
    | UserSignedIn userId, Auth authState ->
        let usersData = authState.UsersData |> updateSignedIn userId true
        let toastCmd =
            match usersData |> findUser userId with
            | Some(user, _, _) ->
                let (UserName userName) = user.UserName
                sprintf "<strong>%s</strong> has signed in" userName |> infoToastCmd
            | None -> Cmd.none
        Auth { authState with UsersData = usersData }, toastCmd
    | UserSignedOut userId, Auth authState ->
        let usersData = authState.UsersData |> updateSignedIn userId false
        let toastCmd =
            match usersData |> findUser userId with
            | Some(user, _, _) ->
                let (UserName userName) = user.UserName
                sprintf "<strong>%s</strong> has signed out" userName |> infoToastCmd
            | None -> Cmd.none
        Auth { authState with UsersData = usersData }, toastCmd
    | ForceUserSignOut forcedSignOutReason, Auth authState -> // TODO-NMB: How best to handle Some forcedSignOutReason [in addition to toast], e.g. notification? display SignInModal?...
        let state = Unauth(unauthState authState.AppState authState.ConnectionState forcedSignOutReason None)
        let toastCmd, extra =
            match forcedSignOutReason with
            | Some UserTypeChanged -> warningToastCmd, " because your permissions have been changed"
            | Some PasswordReset -> warningToastCmd, " because your password has been reset"
            | None -> infoToastCmd, String.Empty
        let cmds = Cmd.batch [
            sprintf "You have been signed out%s" extra |> toastCmd
            writePreferencesCmd (preferencesOrDefault state) ]
        state, cmds
    | UserUpdated(user, usersRvn), Auth authState -> // TODO-NMB: How best to handle usersRvn mismatch (&c.)?...
        let usersData = authState.UsersData |> updateUser user usersRvn
        Auth { authState with UsersData = usersData }, Cmd.none
    | UserAdded(user, usersRvn), Auth authState -> // TODO-NMB: How best to handle usersRvn mismatch (&c.)?...
        let usersData = authState.UsersData |> addUser user usersRvn
        Auth { authState with UsersData = usersData }, Cmd.none
    // TODO-NMB: More RemoteUiInput?...
    | UnexpectedServerInput error, _ -> state |> shouldNeverHappen error
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected RemoteUiInput when %A -> %A" state input)

let private handleDisconnected connectionState state =
    match state, connectionState with
    | InitializingConnection _, _ -> state, Cmd.none
    | _, Some connectionState -> InitializingConnection (Some connectionState.ConnectionId), Cmd.none
    | _ -> InitializingConnection None, Cmd.none

let private handlePreferencesInput input state =
    match input, state with
    | ReadPreferencesResult(Some(Ok preferences)), ReadingPreferences connectionId ->
        let appState = {
            Ticks = 0<tick>
            AffinityId = preferences.AffinityId
            Theme = preferences.Theme
            NavbarBurgerIsActive = false }
        setBodyClass appState.Theme
        let cmd = RegisterConnection(appState, preferences.LastUser, connectionId) |> Cmd.ofMsg
        state, cmd
    | ReadPreferencesResult None, ReadingPreferences _ ->
        let preferences = preferencesOrDefault state
        let cmds = Cmd.batch [
            writePreferencesCmd preferences
            PreferencesInput(ReadPreferencesResult(Some(Ok preferences))) |> Cmd.ofMsg ]
        state, cmds
    | ReadPreferencesResult(Some(Error error)), ReadingPreferences _ ->
        let cmd = PreferencesInput(ReadPreferencesResult None) |> Cmd.ofMsg
        state |> addDebugError (sprintf "ReadPreferencesResult error -> %s" error), cmd
    | ReadPreferencesExn exn, ReadingPreferences _ ->
        let cmd = PreferencesInput(ReadPreferencesResult(Some (Error exn.Message))) |> Cmd.ofMsg
        state, cmd
    | WritePreferencesOk _, _ -> state, Cmd.none
    | WritePreferencesExn exn, _ -> state |> addDebugError (sprintf "WritePreferencesExn -> %s" exn.Message), Cmd.none
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected PreferencesInput when %A -> %A" state input)

let private handleOnTick appState state : State * Cmd<Input> = // note: will only be used when TICK is defined (see webpack.config.js)
    match appState with
    | Some appState ->
        let appState = { appState with Ticks = appState.Ticks + 1<tick> }
        state |> updateAppState appState, Cmd.none
    | _ ->
        state, Cmd.none

let private handleOnMouseMove state = // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    match state with
    | Auth authState ->
        let minActivityDebouncerThreshold = if ACTIVITY_DEBOUNCER_THRESHOLD >= 5.<second> then ACTIVITY_DEBOUNCER_THRESHOLD else 5.<second>
        let debouncerState, debouncerCmd =
            authState.ActivityDebouncerState |> Debouncer.bounce (TimeSpan.FromSeconds(float minActivityDebouncerThreshold)) "OnMouseMove" OnDebouncedActivity
        Auth { authState with ActivityDebouncerState = debouncerState }, debouncerCmd |> Cmd.map ActivityDebouncerSelfInput
    | _ -> state, Cmd.none

// TODO-NMB: More divide-and-conquer - which might allow more granular pattern-matching (i.e. expected cases only)...

let initialize() : State * Cmd<Input> =
    None |> InitializingConnection, Cmd.none

let transition input state : State * Cmd<Input> =
    let appState, connectionState =
        match state with
        | InitializingConnection _ | ReadingPreferences _ -> None, None
        | RegisteringConnection registeringConnectionState -> registeringConnectionState.AppState |> Some, None
        | AutomaticallySigningIn automaticallySigningInState -> automaticallySigningInState.AppState |> Some, automaticallySigningInState.ConnectionState |> Some
        | Unauth unauthState -> unauthState.AppState |> Some, unauthState.ConnectionState |> Some
        | Auth authState -> authState.AppState |> Some, authState.ConnectionState |> Some
    match input, state, (appState, connectionState) with
    | RegisterConnection(appState, lastUser, connectionId), _, _ ->
        (appState.AffinityId, connectionId) |> RemoteServerInput.Register |> Bridge.Send
        registeringConnectionState appState lastUser connectionId |> RegisteringConnection, Cmd.none
    | RemoteUiInput input, _, _ -> state |> handleRemoteUiInput input
    | Disconnected, _, _ -> state |> handleDisconnected connectionState
    | PreferencesInput input, _, _ -> state |> handlePreferencesInput input
    | OnTick, _, _ -> state |> handleOnTick appState
    | OnMouseMove, _, _ -> state |> handleOnMouseMove
    // #region ActivityDebouncerSelfInput | OnDebouncedActivity // TODO-NMB: Divide-and-conquer...
    | ActivityDebouncerSelfInput debouncerInput, Auth authState, _ -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        let debouncerState, debouncerCmd = Debouncer.update debouncerInput authState.ActivityDebouncerState
        { authState with ActivityDebouncerState = debouncerState } |> Auth, debouncerCmd
    | ActivityDebouncerSelfInput _, _, _ -> // note: will only be used when ACTIVITY is defined (see webpack.config.js) - and ignored anyway
        state, Cmd.none
    | OnDebouncedActivity, Auth _, _ -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        RemoteServerInput.Activity |> Bridge.Send
        state, Cmd.none
    | OnDebouncedActivity, _, _ -> // note: will only be used when ACTIVITY is defined (see webpack.config.js) - and ignored anyway
        state, Cmd.none
    // #endregion
    | ToggleTheme, _, (Some appState, _) ->
        let appState = { appState with Theme = match appState.Theme with | Light -> Dark | Dark -> Light }
        setBodyClass appState.Theme
        state |> updateAppState appState |> writePreferencesOrDefault
    | ToggleNavbarBurger, _, (Some appState, _) ->
        let appState = { appState with NavbarBurgerIsActive = not appState.NavbarBurgerIsActive }
        state |> updateAppState appState |> writePreferencesOrDefault
    | TempSignIn, Unauth unauthState, _ -> // TEMP-NMB...
        let userName, password = UserName "neph", Password "neph"
        let cmd = Cmd.OfAsync.either userApi.signIn (unauthState.ConnectionState.ConnectionId, userName, password) SignInResult SignInExn |> Cmd.map SignInInput
        Unauth { unauthState with SigningIn = true ; SignInError = None }, cmd
    // #region SignInInput // TODO-NMB: Divide-and-conquer...
    | SignInInput(AutoSignInResult(Ok(authUser, mustChangePasswordReason))), AutomaticallySigningIn automaticallySigningInState, _ ->
        let authState = authState automaticallySigningInState.AppState automaticallySigningInState.ConnectionState authUser mustChangePasswordReason
        let (UserName userName) = authUser.User.UserName
        let cmds = Cmd.batch [
            sprintf "You have been automatically signed in as <strong>%s</strong>" userName |> successToastCmd
            preferencesOrDefault (Auth authState) |> writePreferencesCmd
            getUsersCmd authState.ConnectionState.ConnectionId authState.AuthUser.Jwt ]
        Auth { authState with UsersData = Pending }, cmds
    | SignInInput(AutoSignInResult(Error error)), AutomaticallySigningIn automaticallySigningInState, _ ->
        let state = unauthState automaticallySigningInState.AppState automaticallySigningInState.ConnectionState None (Some error) |> Unauth
        let (UserName userName), _ = automaticallySigningInState.LastUser
        let cmds = Cmd.batch [
            sprintf "Unable to automatically sign in as <strong>%s</strong>" userName |> warningToastCmd
            preferencesOrDefault state |> writePreferencesCmd ]
        state, cmds
    | SignInInput(AutoSignInExn exn), AutomaticallySigningIn _, _ ->
        state, exn.Message |> Error |> AutoSignInResult |> SignInInput |> Cmd.ofMsg
    | SignInInput(SignInResult(Ok(authUser, mustChangePasswordReason))), Unauth unauthState, _ ->
        let authState = authState unauthState.AppState unauthState.ConnectionState authUser mustChangePasswordReason
        let (UserName userName) = authUser.User.UserName
        let cmds = Cmd.batch [
            sprintf "You have signed in as <strong>%s</strong>" userName |> successToastCmd
            preferencesOrDefault (Auth authState) |> writePreferencesCmd
            getUsersCmd authState.ConnectionState.ConnectionId authState.AuthUser.Jwt ]
        Auth { authState with UsersData = Pending }, cmds
    | SignInInput(SignInResult(Error error)), Unauth unauthState, _ ->
        let state = { unauthState with SigningIn = false ; SignInError = error |> Some } |> Unauth
        state, Cmd.none // no need for toast (since error will be displayed on SignInModal)
    | SignInInput(SignInExn exn), Unauth _, _ ->
        state, exn.Message |> Error |> SignInResult |> SignInInput |> Cmd.ofMsg
    // #endregion
    | TempSignOut, Auth authState, _ -> // TEMP-NMB...
        let cmd = Cmd.OfAsync.either userApi.signOut (authState.ConnectionState.ConnectionId, authState.AuthUser.Jwt) SignOutResult SignOutExn |> Cmd.map SignOutInput
        Auth { authState with SigningOut = true }, cmd
    // #region SignOutInput // TODO-NMB: Divide-and-conquer...
    | SignOutInput(SignOutResult(Ok _)), Auth authState, _ ->
        let state = Unauth(unauthState authState.AppState authState.ConnectionState None None)
        let cmds = Cmd.batch [
            "You have signed out" |> successToastCmd
            preferencesOrDefault state |> writePreferencesCmd ]
        state, cmds
    | SignOutInput(SignOutResult(Error error)), Auth _, _ -> // TODO-NMB: Call addDebugError (no need for toast)?...
        sprintf "SignOutResult error -> %s" error |> Browser.console.log
        state, () |> Ok |> SignOutResult |> SignOutInput |> Cmd.ofMsg
    | SignOutInput(SignOutExn exn), Auth _, _ ->
        state, exn.Message |> Error |> SignOutResult |> SignOutInput |> Cmd.ofMsg
    // #endregion
    | TempGetUsers, Auth authState, _ -> // TEMP-NMB...
        let cmd = getUsersCmd authState.ConnectionState.ConnectionId authState.AuthUser.Jwt
        Auth { authState with UsersData = Pending }, cmd
    // #region GetUsersInput // TODO-NMB: Divide-and-conquer...
    | GetUsersInput(GetUsersResult(Ok(users, rvn))), Auth authState, _ ->
        let users = users |> List.map (fun (user, signedIn) -> user, signedIn, None)
        { authState with UsersData = (users, rvn) |> Received } |> Auth, Cmd.none
    | GetUsersInput(GetUsersResult(Error error)), Auth authState, _ ->
        { authState with UsersData = error |> Failed } |> Auth, Cmd.none
    | GetUsersInput(GetUsersExn exn), Auth _, _ ->
        state, exn.Message |> Error |> GetUsersResult |> GetUsersInput |> Cmd.ofMsg
    // #endregion
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected Input when %A -> %A" state input)
