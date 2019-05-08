module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.LocalStorage
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.ShouldNeverHappen
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Program.ServerApi
open Aornota.Gibet.Ui.Shared

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
        do! ifDebugSleepAsync 250 1000 *)
        return readJson(Key APP_PREFERENCES_KEY) |> Option.map (fun (Json json) -> Decode.Auto.fromString<Preferences> json) }
    Cmd.OfAsync.either readPreferences () ReadPreferencesResult ReadPreferencesExn |> Cmd.map PreferencesInput
let private writePreferencesCmd preferences =
    let writePreferences preferences = async {
        writeJson (Key APP_PREFERENCES_KEY) (Json(Encode.Auto.toString<Preferences>(4, preferences))) }
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
        | None -> AffinityId.Create(), defaultTheme
    {
        AffinityId = affinityId
        Theme = theme
        LastUser = lastUser
    }
let private writePreferencesOrDefault state =
    let preferences = preferencesOrDefault state
    state, writePreferencesCmd preferences

let private getUsersCmd connection jwt =
    Cmd.OfAsync.either userApi.getUsers (connection, jwt) GetUsersResult GetUsersExn |> Cmd.map (GetUsersInput >> AuthInput >> AppInput)

let private connectionId state =
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> None
    | RegisteringConnection registeringConnectionState -> registeringConnectionState.ConnectionId
    | AutomaticallySigningIn automaticallySigningInState -> Some automaticallySigningInState.ConnectionState.ConnectionId
    | Unauth unauthState -> Some unauthState.ConnectionState.ConnectionId
    | Auth authState -> Some authState.ConnectionState.ConnectionId

let private registeringConnectionState appState lastUser connectionId = {
    AppState = appState
    LastUser = lastUser
    ConnectionId = connectionId }
let private automaticallySigningInState appState connectionState lastUser = {
    AppState = appState
    ConnectionState = connectionState
    LastUser = lastUser }

let private signInModalState userName autoSignInError forcedSignOutReason =
    let userName, focusPassword = match userName with | Some(UserName userName) -> userName, true | None -> String.Empty, false
    {
        UserNameKey = Guid.NewGuid()
        UserName = userName
        UserNameChanged = false
        PasswordKey = Guid.NewGuid()
        Password = String.Empty
        PasswordChanged = false
        FocusPassword = focusPassword
        AutoSignInError = autoSignInError
        ForcedSignOutReason = forcedSignOutReason
        ModalStatus = None
    }
let private unauthState appState connectionState autoSignInError forcedSignOutReason =
    let signInModalState =
        match autoSignInError, forcedSignOutReason with
        | Some(error, userName), _ -> Some(signInModalState (Some userName) (Some(error, userName)) None)
        | None, Some(forcedSignOutReason, userName) -> Some(signInModalState (Some userName) None (Some forcedSignOutReason))
        | None, None -> None
    {
        AppState = appState
        ConnectionState = connectionState
        SignInModalState = signInModalState
    }

let private changePasswordModalState mustChangePasswordReason = {
    NewPasswordKey = Guid.NewGuid()
    NewPassword = String.Empty
    NewPasswordChanged = false
    ConfirmPasswordKey = Guid.NewGuid()
    ConfirmPassword = String.Empty
    ConfirmPasswordChanged = false
    MustChangePasswordReason = mustChangePasswordReason
    ModalStatus = None }
let private changeImageUrlModalState imageUrl = {
    ImageUrlKey = Guid.NewGuid()
    ImageUrl = match imageUrl with | Some(ImageUrl imageUrl) -> imageUrl | None -> String.Empty
    ImageUrlChanged = false
    CurrentImageUrl = imageUrl
    ModalStatus = None }
let private authState appState connectionState authUser mustChangePasswordReason =
    let changePasswordModalState =
        match mustChangePasswordReason with
        | Some mustChangePasswordReason -> Some(changePasswordModalState (Some mustChangePasswordReason))
        | None -> None
    {
        AppState = appState
        ConnectionState = connectionState
        AuthUser = authUser
        ActivityDebouncerState = Debouncer.create()
        ChangePasswordModalState = changePasswordModalState
        ChangeImageUrlModalState = None
        SigningOut = false
        UsersData = NotRequested
    }

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
    let error = "Something has gone wrong. Please try refreshing the page - and if problems persist, please contact the wesbite administrator." // TEMP-NMB...
    state |> addError false SHOULD_NEVER_HAPPEN, Cmd.none
#endif
// #endregion

let private handleRemoteUiInput remoteUiInput state =
    let toastImage imageUrl =
        match imageUrl with
        | Some(ImageUrl imageUrl) -> sprintf "<img src=\"%s\" width=\"24\" height=\"24\" style=\"vertical-align:middle\"><img>&nbsp&nbsp" imageUrl
        | None -> String.Empty
    match remoteUiInput, state with
    | Initialized, InitializingConnection connectionId -> ReadingPreferences connectionId, readPreferencesCmd
    | Registered(connectionId, serverStarted), RegisteringConnection registeringConnectionState ->
        let connectionState = { ConnectionId = connectionId ; ServerStarted = serverStarted }
        match registeringConnectionState.LastUser with
        | Some(userName, jwt) ->
            let cmd = Cmd.OfAsync.either userApi.autoSignIn (connectionState.ConnectionId, jwt) AutoSignInResult AutoSignInExn |> Cmd.map AutoSignInInput
            AutomaticallySigningIn (automaticallySigningInState registeringConnectionState.AppState connectionState (userName, jwt)), cmd
        | None ->
            Unauth(unauthState registeringConnectionState.AppState connectionState None None), Cmd.none
    | UserActivity userId, Auth authState -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        let usersData, error = authState.UsersData |> updateActivity userId
        let state = Auth { authState with UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None -> state, Cmd.none
    | UserSignedIn userId, Auth authState ->
        let usersData, error = authState.UsersData |> updateSignedIn userId true
        let state = Auth { authState with UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None ->
            let toastCmd =
                match usersData |> findUser userId with
                | Some(user, _, _) ->
                    let (UserName userName) = user.UserName
                    sprintf "%s<strong>%s</strong> has signed in" (toastImage user.ImageUrl) userName |> infoToastCmd
                | None -> Cmd.none
            state, toastCmd
    | UserSignedOut userId, Auth authState ->
        let usersData, error = authState.UsersData |> updateSignedIn userId false
        let state = Auth { authState with UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None ->
            let toastCmd =
                match usersData |> findUser userId with
                | Some(user, _, _) ->
                    let (UserName userName) = user.UserName
                    sprintf "%s<strong>%s</strong> has signed out" (toastImage user.ImageUrl) userName |> infoToastCmd
                | None -> Cmd.none
            state, toastCmd
    | ForceUserSignOut forcedSignOutReason, Auth authState ->
        let state =
            match forcedSignOutReason with
            | Some forcedSignOutReason -> Unauth(unauthState authState.AppState authState.ConnectionState None (Some(forcedSignOutReason, authState.AuthUser.User.UserName)))
            | None -> Unauth(unauthState authState.AppState authState.ConnectionState None None)
        let toastCmd, extra =
            match forcedSignOutReason with
            | Some forcedSignOutReason -> warningToastCmd, sprintf " because %s" (forcedSignOutBecause forcedSignOutReason)
            | None -> infoToastCmd, String.Empty
        let cmds = Cmd.batch [
            sprintf "You have been signed out%s" extra |> toastCmd
            writePreferencesCmd (preferencesOrDefault state) ]
        state, cmds
    | UserUpdated(user, usersRvn), Auth authState ->
        let authUser = authState.AuthUser
        let authUser = if user.UserId = authUser.User.UserId then { authUser with User = user } else authUser
        let usersData, error = authState.UsersData |> updateUser user usersRvn
        let state = Auth { authState with AuthUser = authUser ; UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None -> state, ifDebug (sprintf "%A updated (UsersData now %A)" user.UserId usersRvn |> infoToastCmd) Cmd.none
    | UserAdded(user, usersRvn), Auth authState ->
        let usersData, error = authState.UsersData |> addUser user usersRvn
        let state = Auth { authState with UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None -> state, ifDebug (sprintf "%A added (UsersData now %A)" user.UserId usersRvn |> infoToastCmd) Cmd.none
    // TODO-NMB: More RemoteUiInput?...
    | UnexpectedServerInput error, _ -> state |> shouldNeverHappen error
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected RemoteUiInput when %A -> %A" state remoteUiInput)

let private handleDisconnected state =
    match state with
    | InitializingConnection _ -> state, Cmd.none
    | _ -> InitializingConnection (Some state), Cmd.none

let private handlePreferencesInput preferencesInput state =
    match preferencesInput, state with
    | ReadPreferencesResult(Some(Ok preferences)), ReadingPreferences reconnectingState ->
        let appState = {
            Ticks = 0<tick>
            AffinityId = preferences.AffinityId
            Theme = preferences.Theme
            NavbarBurgerIsActive = false }
        setBodyClass appState.Theme
        let connectionId =
            match reconnectingState with
            | Some reconnectingState -> connectionId reconnectingState
            | None -> None
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
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected PreferencesInput when %A -> %A" state preferencesInput)

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
let private handleActivityDebouncerSelfInput selfInput state = // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    match state with
    | Auth authState ->
        let debouncerState, cmd = authState.ActivityDebouncerState |> Debouncer.update selfInput
        Auth { authState with ActivityDebouncerState = debouncerState }, cmd
    | _ -> state, Cmd.none
let private handleOnDebouncedActivity state = // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    match state with
    | Auth _ ->
        Bridge.Send RemoteServerInput.Activity
        state, Cmd.none
    | _ -> state, Cmd.none

let private handleAutoSignInInput autoSignInInput (automaticallySigningInState:AutomaticallySigningInState) state =
    match autoSignInInput with
    | AutoSignInResult(Ok(authUser, mustChangePasswordReason)) ->
        let authState = authState automaticallySigningInState.AppState automaticallySigningInState.ConnectionState authUser mustChangePasswordReason
        let (UserName userName) = authUser.User.UserName
        let cmds = Cmd.batch [
            sprintf "You have been automatically signed in as <strong>%s</strong>" userName |> successToastCmd
            writePreferencesCmd (preferencesOrDefault (Auth authState))
            getUsersCmd authState.ConnectionState.ConnectionId authState.AuthUser.Jwt ]
        Auth { authState with UsersData = Pending }, cmds
    | AutoSignInResult(Error error) ->
        let userName = fst automaticallySigningInState.LastUser
        let state = Unauth(unauthState automaticallySigningInState.AppState automaticallySigningInState.ConnectionState (Some(error, userName)) None)
        let (UserName userName) = userName
        let cmds = Cmd.batch [
            sprintf "Unable to automatically sign in as <strong>%s</strong>" userName |> warningToastCmd
            writePreferencesCmd (preferencesOrDefault state) ]
        state, cmds
    | AutoSignInExn exn -> state, AutoSignInInput(AutoSignInResult(Error exn.Message)) |> Cmd.ofMsg

let private handleSignInModalInput signInModalInput (unauthState:UnauthState) state =
    match unauthState.SignInModalState with
    | Some signInModalState ->
        match signInModalInput, signInModalState.ModalStatus with
        | _, Some ModalPending -> state |> shouldNeverHappen (sprintf "Unexpected SignInModalInput when SignInModalState.ModalStatus is Pending (%A) -> %A" state signInModalInput)
        | UserNameChanged userName, _ ->
            let signInModalState = { signInModalState with UserName = userName ; UserNameChanged = true }
            Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none
        | PasswordChanged password, _ ->
            let signInModalState = { signInModalState with Password = password ; PasswordChanged = true }
            Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none
        | SignIn, _ ->
            let userName, password = UserName(signInModalState.UserName.Trim()), Password(signInModalState.Password.Trim())
            let cmd =
                Cmd.OfAsync.either userApi.signIn (unauthState.ConnectionState.ConnectionId, userName, password) SignInResult SignInExn |> Cmd.map (SignInInput >> UnauthInput >> AppInput)
            let signInModalState = { signInModalState with AutoSignInError = None ; ForcedSignOutReason = None ; ModalStatus = Some ModalPending }
            Unauth { unauthState with SignInModalState = Some signInModalState }, cmd
        | CancelSignIn, _ -> Unauth { unauthState with SignInModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected SignInModalInput when SignInModalState is None (%A) -> %A" state signInModalInput)
let private handleSignInInput signInInput (unauthState:UnauthState) state =
    match unauthState.SignInModalState with
    | Some signInModalState ->
        match signInModalState.ModalStatus with
        | Some ModalPending ->
            match signInInput with
            | SignInResult(Ok(authUser, mustChangePasswordReason)) ->
                let authState = authState unauthState.AppState unauthState.ConnectionState authUser mustChangePasswordReason
                let (UserName userName) = authUser.User.UserName
                let cmds = Cmd.batch [
                    sprintf "You have signed in as <strong>%s</strong>" userName |> successToastCmd
                    writePreferencesCmd (preferencesOrDefault (Auth authState))
                    getUsersCmd authState.ConnectionState.ConnectionId authState.AuthUser.Jwt ]
                Auth { authState with UsersData = Pending }, cmds
            | SignInResult(Error error) ->
                let signInModalState = { signInModalState with ModalStatus = Some(ModalFailed error) }
                Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none // no need for toast (since error will be displayed on SignInModal)
            | SignInExn exn -> state, AppInput(UnauthInput(SignInInput(SignInResult(Error exn.Message)))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected SignInInput when SignInModalState.ModalStatus is not Pending (%A) -> %A" state signInInput)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected SignInInput when SignInModalState is None (%A) -> %A" state signInInput)

let private handleChangePasswordModalInput changePasswordModalInput (authState:AuthState) state =
    match authState.ChangePasswordModalState with
    | Some changePasswordModalState ->
        match changePasswordModalInput, changePasswordModalState.ModalStatus with
        | _, Some ModalPending -> state |> shouldNeverHappen (sprintf "Unexpected ChangePasswordModalInput when ChangePasswordModalState.ModalStatus is Pending (%A) -> %A" state changePasswordModalInput)
        | NewPasswordChanged newPassword, _ ->
            let changePasswordModalState = { changePasswordModalState with NewPassword = newPassword ; NewPasswordChanged = true }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none
        | ConfirmPasswordChanged confirmPassword, _ ->
            let changePasswordModalState = { changePasswordModalState with ConfirmPassword = confirmPassword ; ConfirmPasswordChanged = true }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none
        | ChangePassword, _ ->
            (* Note that although we could use authState.AuthUser.User.Rvn (rather than take this get-user-from-UsersData approach) - because authState.AuthUser.User *will* be updated
               when handling RemoteUiInput(UserUpdated _) - we will need to follow this alternative approach to ascertaining the current Rvn for uesrApi.resetPassword &c. *)
            // TODO-NMB: Switch to using authState.AuthUser.User.Rvn (cf. handling of ChangeImageUrlModalInput ChangeImageUrl below)?...
            match authState.UsersData |> findUser authState.AuthUser.User.UserId with
            | Some(user, _, _) ->
                let password = Password(changePasswordModalState.NewPassword.Trim())
                let cmd =
                    Cmd.OfAsync.either userApi.changePassword (authState.AuthUser.Jwt, password, user.Rvn) ChangePasswordResult ChangePasswordExn
                    |> Cmd.map (ChangePasswordInput >> AuthInput >> AppInput)
                let changePasswordModalState = { changePasswordModalState with ModalStatus = Some ModalPending }
                Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, cmd
            | None ->
                let changePasswordModalState = { changePasswordModalState with ModalStatus = Some (ModalFailed UNEXPECTED_ERROR) }
                let state = Auth { authState with ChangePasswordModalState = Some changePasswordModalState }
                state |> shouldNeverHappen (sprintf "Unexpected ChangePassword when %A not found in authState.UsersData -> %A" authState.AuthUser.User.UserId state)
        | CancelChangePassword, _ -> Auth { authState with ChangePasswordModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected ChangePasswordModalInput when ChangePasswordModalState is None (%A) -> %A" state changePasswordModalInput)
let private handleChangePasswordInput changePasswordInput (authState:AuthState) state =
    match authState.ChangePasswordModalState with
    | Some changePasswordModalState ->
        match changePasswordModalState.ModalStatus with
        | Some ModalPending ->
            match changePasswordInput with
            | ChangePasswordResult(Ok(UserName userName)) ->
                Auth { authState with ChangePasswordModalState = None }, sprintf "Password changed for <strong>%s</strong>" userName |> successToastCmd
            | ChangePasswordResult(Error error) ->
                let changePasswordModalState = { changePasswordModalState with ModalStatus = Some(ModalFailed error) }
                Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none // no need for toast (since error will be displayed on ChangePasswordModal)
            | ChangePasswordExn exn -> state, AppInput(AuthInput(ChangePasswordInput(ChangePasswordResult(Error exn.Message)))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected ChangePasswordInput when ChangePasswordModalState.ModalStatus is not Pending (%A) -> %A" state changePasswordInput)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected ChangePasswordInput when ChangePasswordModalState is None (%A) -> %A" state changePasswordInput)

let private handleChangeImageUrlModalInput changeImageUrlModalInput (authState:AuthState) state =
    match authState.ChangeImageUrlModalState with
    | Some changeImageUrlModalState ->
        match changeImageUrlModalInput, changeImageUrlModalState.ModalStatus with
        | _, Some ModalPending -> state |> shouldNeverHappen (sprintf "Unexpected ChangeImageUrlModalInput when ChangeImageUrlModalState.ModalStatus is Pending (%A) -> %A" state changeImageUrlModalInput)
        | ImageUrlChanged imageUrl, _ ->
            let changeImageUrlModalState = { changeImageUrlModalState with ImageUrl = imageUrl ; ImageUrlChanged = true }
            Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, Cmd.none
        | ChangeImageUrl, _ ->
            // Note that we *do* use authState.AuthUser.User.Rvn here (cf. handling of ChangePasswordModalInput ChangePassword above).
            let imageUrl = changeImageUrlModalState.ImageUrl
            let imageUrl = if String.IsNullOrWhiteSpace imageUrl then None else Some(ImageUrl imageUrl)
            let cmd =
                Cmd.OfAsync.either userApi.changeImageUrl (authState.AuthUser.Jwt, imageUrl, authState.AuthUser.User.Rvn) ChangeImageUrlResult ChangeImageUrlExn
                |> Cmd.map (ChangeImageUrlInput >> AuthInput >> AppInput)
            let changeImageUrlModalState = { changeImageUrlModalState with ModalStatus = Some ModalPending }
            Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, cmd
        | CancelChangeImageUrl, _ -> Auth { authState with ChangeImageUrlModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected ChangeImageUrlModalInput when ChangeImageUrlModalState is None (%A) -> %A" state changeImageUrlModalInput)
let private handleChangeImageUrlInput changeImageUrlInput (authState:AuthState) state =
    match authState.ChangeImageUrlModalState with
    | Some changeImageUrlModalState ->
        match changeImageUrlModalState.ModalStatus with
        | Some ModalPending ->
            match changeImageUrlInput with
            | ChangeImageUrlResult(Ok(UserName userName)) ->
                Auth { authState with ChangeImageUrlModalState = None }, sprintf "Image changed for <strong>%s</strong>" userName |> successToastCmd
            | ChangeImageUrlResult(Error error) ->
                let changeImageUrlModalState = { changeImageUrlModalState with ModalStatus = Some(ModalFailed error) }
                Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, Cmd.none // no need for toast (since error will be displayed on ChangeImageUrlModal)
            | ChangeImageUrlExn exn -> state, AppInput(AuthInput(ChangeImageUrlInput(ChangeImageUrlResult(Error exn.Message)))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected ChangeImageUrlInput when changeImageUrlModalState.ModalStatus is not Pending (%A) -> %A" state changeImageUrlInput)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected ChangeImageUrlInput when changeImageUrlModalState is None (%A) -> %A" state changeImageUrlInput)

let private handleSignOutInput signOutInput (authState:AuthState) state =
    match signOutInput with
    | SignOutResult(Ok _) ->
        let state = Unauth(unauthState authState.AppState authState.ConnectionState None None)
        let cmds = Cmd.batch [
            "You have signed out" |> successToastCmd
            writePreferencesCmd (preferencesOrDefault state) ]
        state, cmds
    | SignOutResult(Error error) ->
        let state = state |> addDebugError (sprintf "SignOutResult error -> %s" error)
        state, AppInput(AuthInput(SignOutInput(SignOutResult(Ok())))) |> Cmd.ofMsg
    | SignOutExn exn -> state, AppInput(AuthInput(SignOutInput(SignOutResult(Error exn.Message)))) |> Cmd.ofMsg

let private handleGetUsersInput getUsersInput (authState:AuthState) state =
    match getUsersInput with
    | GetUsersResult(Ok(users, usersRvn)) ->
        let users = users |> List.map (fun (user, signedIn) -> user, signedIn, None)
        Auth { authState with UsersData = Received(users, usersRvn) }, ifDebug (sprintf "Got %i users (UsersData %A)" users.Length usersRvn |> infoToastCmd) Cmd.none
    | GetUsersResult(Error error) -> Auth { authState with UsersData = Failed error }, ifDebug (sprintf "GetUsersResult error -> %s" error |> errorToastCmd) Cmd.none
    | GetUsersExn exn -> state, AppInput(AuthInput(GetUsersInput(GetUsersResult(Error exn.Message)))) |> Cmd.ofMsg

let initialize() : State * Cmd<Input> =
    None |> InitializingConnection, Cmd.none

let transition input state : State * Cmd<Input> =
    let appState, connectionState =
        match state with
        | InitializingConnection _ | ReadingPreferences _ -> None, None
        | RegisteringConnection registeringConnectionState -> Some registeringConnectionState.AppState, None
        | AutomaticallySigningIn automaticallySigningInState -> Some automaticallySigningInState.AppState, Some automaticallySigningInState.ConnectionState
        | Unauth unauthState -> Some unauthState.AppState, Some unauthState.ConnectionState
        | Auth authState -> Some authState.AppState, Some authState.ConnectionState
    match input, state, (appState, connectionState) with
    | RegisterConnection(appState, lastUser, connectionId), _, _ ->
        Bridge.Send (RemoteServerInput.Register(appState.AffinityId, connectionId))
        RegisteringConnection(registeringConnectionState appState lastUser connectionId), Cmd.none
    | RemoteUiInput remoteUiInput, _, _ -> state |> handleRemoteUiInput remoteUiInput
    | Disconnected, _, _ -> state |> handleDisconnected
    | PreferencesInput preferencesInput, _, _ -> state |> handlePreferencesInput preferencesInput
    | OnTick, _, _ -> state |> handleOnTick appState
    | OnMouseMove, _, _ -> state |> handleOnMouseMove
    | ActivityDebouncerSelfInput selfInput, _, _ -> state |> handleActivityDebouncerSelfInput selfInput
    | OnDebouncedActivity, _, _ -> state |> handleOnDebouncedActivity
    | ToggleTheme, _, (Some appState, _) ->
        let appState = { appState with Theme = match appState.Theme with | Light -> Dark | Dark -> Light }
        setBodyClass appState.Theme
        state |> updateAppState appState |> writePreferencesOrDefault
    | ToggleNavbarBurger, _, (Some appState, _) ->
        let appState = { appState with NavbarBurgerIsActive = not appState.NavbarBurgerIsActive }
        state |> updateAppState appState |> writePreferencesOrDefault
    | AutoSignInInput autoSignInInput, AutomaticallySigningIn automaticallySigningInState, _ -> state |> handleAutoSignInInput autoSignInInput automaticallySigningInState
    | AppInput(UnauthInput ShowSignInModal), Unauth unauthState, _ ->
        match unauthState.SignInModalState with
        | Some signInModalState -> state |> shouldNeverHappen (sprintf "Unexpected ShowSignInModal when SignInModalState is %A" signInModalState)
        | None -> Unauth { unauthState with SignInModalState = Some(signInModalState None None None) }, Cmd.none
    | AppInput(UnauthInput(SignInModalInput signInModalInput)), Unauth unauthState, _ -> state |> handleSignInModalInput signInModalInput unauthState
    | AppInput(UnauthInput(SignInInput signInInput)), Unauth unauthState, _ -> state |> handleSignInInput signInInput unauthState
    | AppInput(AuthInput ShowChangePasswordModal), Auth authState, _ ->
        match authState.ChangePasswordModalState with
        | Some changePasswordModalState -> state |> shouldNeverHappen (sprintf "Unexpected ShowChangePasswordModal when ChangePasswordModalState is %A" changePasswordModalState)
        | None -> Auth { authState with ChangePasswordModalState = Some(changePasswordModalState None) }, Cmd.none
    | AppInput(AuthInput(ChangePasswordModalInput changePasswordModalInput)), Auth authState, _ -> state |> handleChangePasswordModalInput changePasswordModalInput authState
    | AppInput(AuthInput(ChangePasswordInput changePasswordInput)), Auth authState, _ -> state |> handleChangePasswordInput changePasswordInput authState
    | AppInput(AuthInput ShowChangeImageUrlModal), Auth authState, _ ->
        match authState.ChangeImageUrlModalState with
        | Some changeImageUrlModalState -> state |> shouldNeverHappen (sprintf "Unexpected ShowChangeImageUrlModal when ChangeImageUrlModalState is %A" changeImageUrlModalState)
        | None -> Auth { authState with ChangeImageUrlModalState = Some(changeImageUrlModalState authState.AuthUser.User.ImageUrl) }, Cmd.none
    | AppInput(AuthInput(ChangeImageUrlModalInput changeImageUrlModalInput)), Auth authState, _ -> state |> handleChangeImageUrlModalInput changeImageUrlModalInput authState
    | AppInput(AuthInput(ChangeImageUrlInput changeImageUrlInput)), Auth authState, _ -> state |> handleChangeImageUrlInput changeImageUrlInput authState
    | AppInput(AuthInput SignOut), Auth authState, _ ->
        let cmd = Cmd.OfAsync.either userApi.signOut (authState.ConnectionState.ConnectionId, authState.AuthUser.Jwt) SignOutResult SignOutExn |> Cmd.map (SignOutInput >> AuthInput >> AppInput)
        Auth { authState with SigningOut = true }, cmd
    | AppInput(AuthInput(SignOutInput signOutInput)), Auth authState, _ -> state |> handleSignOutInput signOutInput authState
    | AppInput(AuthInput(GetUsersInput getUsersInput)), Auth authState, _ -> state |> handleGetUsersInput getUsersInput authState
    | AppInput(AuthInput TempShowUserAdminPage), Auth authState, _ -> state, "User administration -> not yet implemented" |> warningToastCmd // TEMP-NMB...
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected Input when %A -> %A" state input)
