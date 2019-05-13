module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.LocalStorage
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.ShouldNeverHappen
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Pages
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared
open Aornota.Gibet.Ui.UserApi

open System

open Elmish
open Elmish.Bridge

open Fable.Core.JS
open Fable.Import

open Thoth.Json

let [<Literal>] private APP_PREFERENCES_KEY = "gibet-ui-app-preferences"

// #region AUTO_SHOW_SIGN_IN_MODAL
let [<Literal>] private AUTO_SHOW_SIGN_IN_MODAL =
#if DEBUG
    true
#else
    false
#endif
// #endregion

let [<Literal>] private ACTIVITY_THROTTLE = 15.<second> // TODO-NMB: Decide what this should be... // note: "ignored" if less than 5.<second>

let private activityThrottle = max ACTIVITY_THROTTLE 5.<second>

let private setBodyClass theme = Browser.document.body.className <- themeClass theme
let private setTitle pageTitle = Browser.document.title <- sprintf "%s | %s" GIBET pageTitle

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
    let appState, lastUser, lastPage =
        match state with
        | InitializingConnection _ | ReadingPreferences _ -> None, None, None
        | RegisteringConnection registeringConnectionState -> Some registeringConnectionState.AppState, None, None
        | AutomaticallySigningIn automaticallySigningInState -> Some automaticallySigningInState.AppState, Some automaticallySigningInState.LastUser, None
        | Unauth unauthState -> Some unauthState.AppState, None, Some(UnauthPage unauthState.CurrentPage)
        | Auth authState ->
            if authState.StaySignedIn then Some authState.AppState, Some(authState.AuthUser.User.UserName, authState.AuthUser.Jwt), Some authState.CurrentPage
            else Some authState.AppState, None, Some authState.CurrentPage
    let affinityId, theme =
        match appState with
        | Some appState -> appState.AffinityId, appState.Theme
        | None -> AffinityId.Create(), defaultTheme
    {
        AffinityId = affinityId
        Theme = theme
        LastUser = lastUser
        LastPage = lastPage
    }
let private writePreferencesOrDefault state =
    let preferences = state |> preferencesOrDefault
    state, writePreferencesCmd preferences

let private getUsersCmd connection jwt = Cmd.OfAsync.either userApi.getUsers (connection, jwt) GetUsersResult GetUsersExn |> Cmd.map (GetUsersInput >> AuthInput)

let private themeOrDefault state =
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> defaultTheme
    | RegisteringConnection registeringConnectionState -> registeringConnectionState.AppState.Theme
    | AutomaticallySigningIn automaticallySigningInState -> automaticallySigningInState.AppState.Theme
    | Unauth unauthState -> unauthState.AppState.Theme
    | Auth authState -> authState.AppState.Theme
let private connectionId state =
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> None
    | RegisteringConnection registeringConnectionState -> registeringConnectionState.ConnectionId
    | AutomaticallySigningIn automaticallySigningInState -> Some automaticallySigningInState.ConnectionState.ConnectionId
    | Unauth unauthState -> Some unauthState.ConnectionState.ConnectionId
    | Auth authState -> Some authState.ConnectionState.ConnectionId

let private appState affinityId theme = {
    Ticks = 0<tick>
    AffinityId = affinityId
    Theme = theme
    NavbarBurgerIsActive = false }
let private updateAppState appState state =
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> state
    | RegisteringConnection registeringConnectionState -> RegisteringConnection { registeringConnectionState with AppState = appState }
    | AutomaticallySigningIn automaticallySigningInState -> AutomaticallySigningIn { automaticallySigningInState with AppState = appState }
    | Unauth unauthState -> Unauth { unauthState with AppState = appState }
    | Auth authState -> Auth { authState with AppState = appState }

let private registeringConnectionState messages appState lastUser lastPage connectionId = {
    Messages = messages
    AppState = appState
    LastUser = lastUser
    LastPage = lastPage
    ConnectionId = connectionId }

let private automaticallySigningInState messages appState connectionState lastUser lastPage = {
    Messages = messages
    AppState = appState
    ConnectionState = connectionState
    LastUser = lastUser
    LastPage = lastPage }

let private signInModalState userName keepMeSignedIn autoSignInError forcedSignOutReason =
    let userName, focusPassword = match userName with | Some(UserName userName) -> userName, true | None -> String.Empty, false
    {
        UserNameKey = Guid.NewGuid()
        UserName = userName
        UserNameChanged = false
        PasswordKey = Guid.NewGuid()
        Password = String.Empty
        PasswordChanged = false
        KeepMeSignedInKey = Guid.NewGuid()
        KeepMeSignedIn = keepMeSignedIn
        FocusPassword = focusPassword
        AutoSignInError = autoSignInError
        ForcedSignOutReason = forcedSignOutReason
        ModalStatus = None
    }
let private unauthState messages appState connectionState lastPage autoSignInError forcedSignOutReason showSignInModal : UnauthState * Cmd<Input> =
    let signInModalState =
        match autoSignInError, forcedSignOutReason, showSignInModal with
        | Some(error, userName), _, _ -> Some(signInModalState (Some userName) true (Some(error, userName)) None)
        | None, Some(forcedSignOutReason, userName, keepMeSignedIn), _ -> Some(signInModalState (Some userName) keepMeSignedIn None (Some forcedSignOutReason))
        | None, None, true -> Some(signInModalState None true None None)
        | None, None, false -> None
    let currentPage = match lastPage with | Some(UnauthPage page) -> page | _ -> About
    let pageTitle = match currentPage with | About -> About.Render.PAGE_TITLE
    setTitle pageTitle
    let unauthState = {
        Messages = messages
        AppState = appState
        ConnectionState = connectionState
        CurrentPage = currentPage
        SignInModalState = signInModalState }
    unauthState, Cmd.none

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
    ModalStatus = None }
let private authState messages appState connectionState lastPage authUser staySignedIn mustChangePasswordReason =
    let changePasswordModalState =
        match mustChangePasswordReason with
        | Some mustChangePasswordReason -> Some(changePasswordModalState (Some mustChangePasswordReason))
        | None -> None
    let currentPage = match lastPage with | Some page -> page | _ -> UnauthPage About
    let chatState, chatCmd = Chat.State.initialize authUser
    let userAdminState, userAdminCmd =
        match currentPage with
        | AuthPage UserAdmin ->
            let userAdminState, userAdminCmd = UserAdmin.State.initialize authUser
            Some userAdminState, userAdminCmd
        | _ -> None, Cmd.none
    let pageTitle =
        match currentPage with
        | UnauthPage About -> About.Render.PAGE_TITLE
        | AuthPage Chat -> Chat.Render.pageTitle chatState
        | AuthPage UserAdmin -> UserAdmin.Render.PAGE_TITLE
    setTitle pageTitle
    Bridge.Send RemoteServerInput.Activity
    let cmds = Cmd.batch [
        getUsersCmd connectionState.ConnectionId authUser.Jwt
        chatCmd |> Cmd.map (ChatInput >> AuthInput)
        userAdminCmd |> Cmd.map (UserAdminInput >> AuthInput) ]
    let authState = {
        Messages = messages
        AppState = appState
        ConnectionState = connectionState
        AuthUser = authUser
        StaySignedIn = staySignedIn
        LastActivity = DateTime.Now
        CurrentPage = currentPage
        ChatState = chatState
        UserAdminState = userAdminState
        ChangePasswordModalState = changePasswordModalState
        ChangeImageUrlModalState = None
        SigningOut = false
        UsersData = NotRequested }
    authState, cmds

let private addToMessages message state =
    match state with
    | InitializingConnection(messages, reconnectingState) -> InitializingConnection(message :: messages, reconnectingState)
    | ReadingPreferences(messages, reconnectingState) -> ReadingPreferences(message :: messages, reconnectingState)
    | RegisteringConnection registeringConnectionState -> RegisteringConnection { registeringConnectionState with Messages = message :: registeringConnectionState.Messages }
    | AutomaticallySigningIn automaticallySigningInState -> AutomaticallySigningIn { automaticallySigningInState with Messages = message :: automaticallySigningInState.Messages }
    | Unauth unauthState -> Unauth { unauthState with Messages = message :: unauthState.Messages }
    | Auth authState -> Auth { authState with Messages = message :: authState.Messages }
let private dismissMessage messageId state = // note: silently ignore unknown messageId
    match state with
    | InitializingConnection(messages, reconnectingState) -> InitializingConnection(messages |> removeMessage messageId, reconnectingState)
    | ReadingPreferences(messages, reconnectingState) -> ReadingPreferences(messages |> removeMessage messageId, reconnectingState)
    | RegisteringConnection registeringConnectionState ->
        RegisteringConnection { registeringConnectionState with Messages = registeringConnectionState.Messages |> removeMessage messageId }
    | AutomaticallySigningIn automaticallySigningInState ->
        AutomaticallySigningIn { automaticallySigningInState with Messages = automaticallySigningInState.Messages |> removeMessage messageId }
    | Unauth unauthState -> Unauth { unauthState with Messages = unauthState.Messages |> removeMessage messageId }
    | Auth authState -> Auth { authState with Messages = authState.Messages |> removeMessage messageId }
let private addMessage messageType text state = state |> addToMessages (messageDismissable messageType text)

let private addDebugError error state = state |> addMessage Debug (sprintf "ERROR -> %s" error)
// #region shouldNeverHappen
let private shouldNeverHappen error state : State * Cmd<Input> =
#if DEBUG
    state |> addDebugError (shouldNeverHappen error), Cmd.none
#else
    state |> addMessage Danger SOMETHING_HAS_GONE_WRONG, Cmd.none
#endif
// #endregion

let private handleOnTick appState state : State * Cmd<Input> = // note: will only be used when TICK is defined (see webpack.config.js)
    match appState with
    | Some appState -> state |> updateAppState { appState with Ticks = appState.Ticks + 1<tick> }, Cmd.none
    | _ -> state, Cmd.none
let private handleOnMouseMove state : State * Cmd<Input> =  // TODO-NMB: Chat maintenance... // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    match state with
    | Auth authState ->
        let now, lastActivity = DateTime.Now, authState.LastActivity
        let elapsed = now - lastActivity
        let lastActivity =
            if elapsed.TotalSeconds * 1.<second> > activityThrottle then
                Bridge.Send RemoteServerInput.Activity
                now
            else lastActivity
        // TODO-NMB: If authState.CurrentPage = AuthPage Chat, clear "unseen count"?....
        Auth { authState with LastActivity = lastActivity }, Cmd.none
    | _ -> state, Cmd.none

let private handleRemoteUiInput remoteUiInput state =
    let toastImage imageUrl =
        match imageUrl with
        | Some(ImageUrl imageUrl) -> sprintf "<img src=\"%s\" width=\"48\" height=\"48\" style=\"float:left; margin-right: 10px\"><img>" imageUrl
        | None -> String.Empty
    match remoteUiInput, state with
    | Initialized, InitializingConnection(messages, reconnectingState) -> ReadingPreferences(messages, reconnectingState), readPreferencesCmd
    | Registered(connectionId, serverStarted), RegisteringConnection registeringConnectionState ->
        let connectionState = { ConnectionId = connectionId ; ServerStarted = serverStarted }
        match registeringConnectionState.LastUser with
        | Some(userName, jwt) ->
            let cmd = Cmd.OfAsync.either userApi.autoSignIn (connectionState.ConnectionId, jwt) AutoSignInResult AutoSignInExn |> Cmd.map AutoSignInInput
            AutomaticallySigningIn (automaticallySigningInState registeringConnectionState.Messages registeringConnectionState.AppState connectionState (userName, jwt)
                registeringConnectionState.LastPage), cmd
        | None ->
            let unauthState, cmd =
                unauthState registeringConnectionState.Messages registeringConnectionState.AppState connectionState registeringConnectionState.LastPage None None AUTO_SHOW_SIGN_IN_MODAL
            let state, writePreferencesCmd = Unauth unauthState |> writePreferencesOrDefault
            state, Cmd.batch [ cmd ; writePreferencesCmd ]
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
                match usersData |> findUserRD userId with
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
                match usersData |> findUserRD userId with
                | Some(user, _, _) ->
                    let (UserName userName) = user.UserName
                    sprintf "%s<strong>%s</strong> has signed out" (toastImage user.ImageUrl) userName |> infoToastCmd
                | None -> Cmd.none
            state, toastCmd
    | ForceUserSignOut forcedSignOutReason, Auth authState ->
        let toastCmd, because =
            match forcedSignOutBecause forcedSignOutReason with
            | because, Some(UserName byUserName) -> warningToastCmd, sprintf "%s by <strong>%s</strong>" because byUserName
            | because, None -> infoToastCmd, because
        let unauthState, cmd =
            match forcedSignOutReason with
            | SelfSameAffinityDifferentConnection ->
                unauthState authState.Messages authState.AppState authState.ConnectionState (Some authState.CurrentPage) None None false
            | UserTypeChanged byUserName ->
                unauthState authState.Messages authState.AppState authState.ConnectionState (Some authState.CurrentPage) None
                    (Some((UserTypeChanged byUserName), authState.AuthUser.User.UserName, authState.StaySignedIn)) false
        let state = Unauth unauthState
        let state, writePreferencesCmd = state |> writePreferencesOrDefault
        let cmds = Cmd.batch [
            cmd
            sprintf "You have been signed out because %s" because |> toastCmd
            writePreferencesCmd ]
        state, cmds
    | ForceUserChangePassword byUserName, Auth authState ->
        let mustChangePasswordReason = PasswordReset byUserName
        let because =
            match mustChangePasswordBecause mustChangePasswordReason with
            | because, Some(UserName byUserName) -> sprintf "%s by <strong>%s</strong>" because byUserName
            | because, None -> because
        let toastCmd = sprintf "You must change your password because %s" because |> warningToastCmd
        let changePasswordModalState =
            match authState.ChangePasswordModalState with
            | Some changePasswordModalState ->
                { changePasswordModalState with MustChangePasswordReason = Some mustChangePasswordReason }
            | None -> changePasswordModalState (Some mustChangePasswordReason)
        Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, toastCmd
    | UserUpdated(user, usersRvn, userUpdateType), Auth authState ->
        let authUser = authState.AuthUser
        let authUser = if user.UserId = authUser.User.UserId then { authUser with User = user } else authUser
        let usersData, error = authState.UsersData |> updateUser user usersRvn
        let state = Auth { authState with AuthUser = authUser ; UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None -> state, ifDebug (sprintf "%A updated (%A) -> UsersData now %A" user.UserId userUpdateType usersRvn |> infoToastCmd) Cmd.none
    | UserAdded(user, usersRvn), Auth authState ->
        let usersData, error = authState.UsersData |> addUser user usersRvn
        let state = Auth { authState with UsersData = usersData }
        match error with
        | Some error -> state |> shouldNeverHappen error
        | None -> state, ifDebug (sprintf "%A added -> UsersData now %A" user.UserId usersRvn |> infoToastCmd) Cmd.none
    // TODO-NMB: More RemoteUiInput?...
    | UnexpectedServerInput error, _ -> state |> shouldNeverHappen error
    | _ -> state |> shouldNeverHappen (unexpectedInputWhenState remoteUiInput state)

let private handleDisconnected state =
    match state with
    | InitializingConnection _ -> state, Cmd.none
    | _ ->
        let messages =
            match state with
            | InitializingConnection(messages, _) | ReadingPreferences(messages, _) -> messages
            | RegisteringConnection registeringConnectionState -> registeringConnectionState.Messages
            | AutomaticallySigningIn automaticallySigningInState -> automaticallySigningInState.Messages
            | Unauth unauthState -> unauthState.Messages
            | Auth authState -> authState.Messages
        InitializingConnection(messages, Some state), Cmd.none

let private handlePreferencesInput preferencesInput state =
    match preferencesInput, state with
    | ReadPreferencesResult(Some(Ok preferences)), ReadingPreferences(messages, reconnectingState) ->
        let appState = appState preferences.AffinityId preferences.Theme
        setBodyClass appState.Theme
        let connectionId =
            match reconnectingState with
            | Some reconnectingState -> connectionId reconnectingState
            | None -> None
        Bridge.Send(RemoteServerInput.Register(appState.AffinityId, connectionId))
        RegisteringConnection(registeringConnectionState messages appState preferences.LastUser preferences.LastPage connectionId), Cmd.none
    | ReadPreferencesResult None, ReadingPreferences _ ->
        let preferences = state |> preferencesOrDefault
        let cmds = Cmd.batch [
            writePreferencesCmd preferences
            PreferencesInput(ReadPreferencesResult(Some(Ok preferences))) |> Cmd.ofMsg ]
        state, cmds
    | ReadPreferencesResult(Some(Error error)), ReadingPreferences _ ->
        state |> addDebugError (sprintf "ReadPreferencesResult error -> %s" error), PreferencesInput(ReadPreferencesResult None) |> Cmd.ofMsg
    | ReadPreferencesExn exn, ReadingPreferences _ ->
        let cmd = PreferencesInput(ReadPreferencesResult(Some (Error exn.Message))) |> Cmd.ofMsg
        state, cmd
    | WritePreferencesOk _, _ -> state, Cmd.none
    | WritePreferencesExn exn, _ -> state |> addDebugError (sprintf "WritePreferencesExn -> %s" exn.Message), Cmd.none
    | _ -> state |> shouldNeverHappen (unexpectedInputWhenState preferencesInput state)

let private handleAutoSignInInput autoSignInInput (automaticallySigningInState:AutomaticallySigningInState) state =
    match autoSignInInput with
    | AutoSignInResult(Ok(authUser, mustChangePasswordReason)) ->
        let authState, cmd =
            authState automaticallySigningInState.Messages automaticallySigningInState.AppState automaticallySigningInState.ConnectionState automaticallySigningInState.LastPage
                authUser true mustChangePasswordReason
        let state, writePreferencesCmd = Auth { authState with UsersData = Pending } |> writePreferencesOrDefault
        let (UserName userName) = authUser.User.UserName
        let cmds = Cmd.batch [
            cmd
            sprintf "You have been automatically signed in as <strong>%s</strong>" userName |> successToastCmd
            writePreferencesCmd ]
        state, cmds
    | AutoSignInResult(Error error) ->
        let userName = fst automaticallySigningInState.LastUser
        let unauthState, cmd =
            unauthState automaticallySigningInState.Messages automaticallySigningInState.AppState automaticallySigningInState.ConnectionState automaticallySigningInState.LastPage
                (Some(error, userName)) None false
        let state, writePreferencesCmd = Unauth unauthState |> writePreferencesOrDefault
        let (UserName userName) = userName
        let cmds = Cmd.batch [
            cmd
            sprintf "Unable to automatically sign in as <strong>%s</strong>" userName |> warningToastCmd
            writePreferencesCmd ]
        state, cmds
    | AutoSignInExn exn -> state, AutoSignInInput(AutoSignInResult(Error exn.Message)) |> Cmd.ofMsg

let private handleSignInModalInput signInModalInput (unauthState:UnauthState) state =
    match unauthState.SignInModalState with
    | Some signInModalState ->
        match signInModalInput, signInModalState.ModalStatus with
        | _, Some ModalPending -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState.ModalStatus is Pending (%A)" signInModalInput state)
        | UserNameChanged userName, _ ->
            let signInModalState = { signInModalState with UserName = userName ; UserNameChanged = true }
            Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none
        | PasswordChanged password, _ ->
            let signInModalState = { signInModalState with Password = password ; PasswordChanged = true }
            Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none
        | KeepMeSignedInChanged, _ ->
            let signInModalState = { signInModalState with KeepMeSignedIn = not signInModalState.KeepMeSignedIn }
            Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none
        | SignIn, _ ->
            let userName, password = UserName(signInModalState.UserName.Trim()), Password(signInModalState.Password.Trim())
            let cmd =
                Cmd.OfAsync.either userApi.signIn (unauthState.ConnectionState.ConnectionId, userName, password) SignInResult SignInExn |> Cmd.map (SignInInput >> UnauthInput)
            let signInModalState = { signInModalState with AutoSignInError = None ; ForcedSignOutReason = None ; ModalStatus = Some ModalPending }
            Unauth { unauthState with SignInModalState = Some signInModalState }, cmd
        | CancelSignIn, _ -> Unauth { unauthState with SignInModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState is None (%A)" signInModalInput state)
let private handleSignInInput signInInput (unauthState:UnauthState) state =
    match unauthState.SignInModalState with
    | Some signInModalState ->
        match signInModalState.ModalStatus with
        | Some ModalPending ->
            match signInInput with
            | SignInResult(Ok(authUser, mustChangePasswordReason)) ->
                let authState, cmd =
                    authState unauthState.Messages unauthState.AppState unauthState.ConnectionState (Some(UnauthPage unauthState.CurrentPage)) authUser signInModalState.KeepMeSignedIn
                        mustChangePasswordReason
                let state, writePreferencesCmd = Auth { authState with UsersData = Pending } |> writePreferencesOrDefault
                let (UserName userName) = authUser.User.UserName
                let cmds = Cmd.batch [
                    cmd
                    sprintf "You have signed in as <strong>%s</strong>" userName |> successToastCmd
                    writePreferencesCmd ]
                state, cmds
            | SignInResult(Error error) ->
                let signInModalState = { signInModalState with FocusPassword = true ; ModalStatus = Some(ModalFailed(error, UserName signInModalState.UserName)) }
                Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none // no need for toast (since error will be displayed on SignInModal)
            | SignInExn exn -> state, UnauthInput(SignInInput(SignInResult(Error exn.Message))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState.ModalStatus is not Pending (%A)" signInInput state)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState is None (%A)" signInInput state)

let private handleChangePasswordModalInput changePasswordModalInput (authState:AuthState) state =
    match authState.ChangePasswordModalState with
    | Some changePasswordModalState ->
        match changePasswordModalInput, changePasswordModalState.ModalStatus with
        | _, Some ModalPending -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState.ModalStatus is Pending (%A)" changePasswordModalInput state)
        | NewPasswordChanged newPassword, _ ->
            let changePasswordModalState = { changePasswordModalState with NewPassword = newPassword ; NewPasswordChanged = true }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none
        | ConfirmPasswordChanged confirmPassword, _ ->
            let changePasswordModalState = { changePasswordModalState with ConfirmPassword = confirmPassword ; ConfirmPasswordChanged = true }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none
        | ChangePassword, _ ->
            let password = Password(changePasswordModalState.NewPassword.Trim())
            let cmd =
                Cmd.OfAsync.either userApi.changePassword (authState.AuthUser.Jwt, password, authState.AuthUser.User.Rvn) ChangePasswordResult ChangePasswordExn
                |> Cmd.map (ChangePasswordInput >> AuthInput)
            let changePasswordModalState = { changePasswordModalState with ModalStatus = Some ModalPending }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, cmd
        | CancelChangePassword, _ -> Auth { authState with ChangePasswordModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState is None (%A)" changePasswordModalInput state)
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
            | ChangePasswordExn exn -> state, AuthInput(ChangePasswordInput(ChangePasswordResult(Error exn.Message))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState.ModalStatus is not Pending (%A)" changePasswordInput state)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState is None (%A)" changePasswordInput state)

let private handleChangeImageUrlModalInput changeImageUrlModalInput (authState:AuthState) state =
    match authState.ChangeImageUrlModalState with
    | Some changeImageUrlModalState ->
        match changeImageUrlModalInput, changeImageUrlModalState.ModalStatus with
        | _, Some ModalPending -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangeImageUrlModalState.ModalStatus is Pending (%A)" changeImageUrlModalInput state)
        | ImageUrlChanged imageUrl, _ ->
            let changeImageUrlModalState = { changeImageUrlModalState with ImageUrl = imageUrl ; ImageUrlChanged = true }
            Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, Cmd.none
        | ChangeImageUrl, _ ->
            let imageUrl = if String.IsNullOrWhiteSpace changeImageUrlModalState.ImageUrl then None else Some(ImageUrl changeImageUrlModalState.ImageUrl)
            let cmd =
                Cmd.OfAsync.either userApi.changeImageUrl (authState.AuthUser.Jwt, imageUrl, authState.AuthUser.User.Rvn) ChangeImageUrlResult ChangeImageUrlExn
                |> Cmd.map (ChangeImageUrlInput >> AuthInput)
            let changeImageUrlModalState = { changeImageUrlModalState with ModalStatus = Some ModalPending }
            Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, cmd
        | CancelChangeImageUrl, _ -> Auth { authState with ChangeImageUrlModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangeImageUrlModalState is None (%A)" changeImageUrlModalInput state)
let private handleChangeImageUrlInput changeImageUrlInput (authState:AuthState) state =
    match authState.ChangeImageUrlModalState with
    | Some changeImageUrlModalState ->
        match changeImageUrlModalState.ModalStatus with
        | Some ModalPending ->
            match changeImageUrlInput with
            | ChangeImageUrlResult(Ok(UserName userName, imageChangeType)) ->
                Auth { authState with ChangeImageUrlModalState = None }, sprintf "Image %s for <strong>%s</strong>" (changeType imageChangeType) userName |> successToastCmd
            | ChangeImageUrlResult(Error error) ->
                let changeImageUrlModalState = { changeImageUrlModalState with ModalStatus = Some(ModalFailed error) }
                Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, Cmd.none // no need for toast (since error will be displayed on ChangeImageUrlModal)
            | ChangeImageUrlExn exn -> state, AuthInput(ChangeImageUrlInput(ChangeImageUrlResult(Error exn.Message))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when changeImageUrlModalState.ModalStatus is not Pending (%A)" changeImageUrlInput state)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when changeImageUrlModalState is None (%A)" changeImageUrlInput state)

let private handleSignOutInput signOutInput (authState:AuthState) state =
    match signOutInput with
    | SignOutResult(Ok _) ->
        let unauthState, cmd = unauthState authState.Messages authState.AppState authState.ConnectionState (Some authState.CurrentPage) None None false
        let state, writePreferencesCmd = Unauth unauthState |> writePreferencesOrDefault
        let cmds = Cmd.batch [
            cmd
            "You have signed out" |> successToastCmd
            writePreferencesCmd ]
        state, cmds
    | SignOutResult(Error error) -> state |> addDebugError (sprintf "SignOutResult error -> %s" error), AuthInput(SignOutInput(SignOutResult(Ok()))) |> Cmd.ofMsg
    | SignOutExn exn -> state, AuthInput(SignOutInput(SignOutResult(Error exn.Message))) |> Cmd.ofMsg

let private handleGetUsersInput getUsersInput (authState:AuthState) state =
    match getUsersInput with
    | GetUsersResult(Ok(users, usersRvn)) ->
        let users = users |> List.map (fun (user, signedIn) -> user, signedIn, None)
        Auth { authState with UsersData = Received(users, usersRvn) }, ifDebug (sprintf "Got %i users -> UsersData %A" users.Length usersRvn |> infoToastCmd) Cmd.none
    | GetUsersResult(Error error) -> Auth { authState with UsersData = Failed error }, ifDebug (sprintf "GetUsersResult error -> %s" error |> errorToastCmd) Cmd.none
    | GetUsersExn exn -> state, AuthInput(GetUsersInput(GetUsersResult(Error exn.Message))) |> Cmd.ofMsg

// #region initialize
let initialize() : State * Cmd<Input> =
    let state = InitializingConnection([], None)
#if DEBUG
    console.log("Initial state:", state)
#endif
    state, Cmd.none
// #endregion

// #region transition
let transition input state : State * Cmd<Input> =
    let log = match input with | OnTick | OnMouseMove -> false | _ -> true
#if DEBUG
    if log then console.log("Input:", input)
#endif
    let appState, connectionState =
        match state with
        | InitializingConnection _ | ReadingPreferences _ -> None, None
        | RegisteringConnection registeringConnectionState -> Some registeringConnectionState.AppState, None
        | AutomaticallySigningIn automaticallySigningInState -> Some automaticallySigningInState.AppState, Some automaticallySigningInState.ConnectionState
        | Unauth unauthState -> Some unauthState.AppState, Some unauthState.ConnectionState
        | Auth authState -> Some authState.AppState, Some authState.ConnectionState
    let state, cmd =
        match input, state, (appState, connectionState) with
        | OnTick, _, _ -> state |> handleOnTick appState
        | OnMouseMove, _, _ -> state |> handleOnMouseMove
        | AddMessage message, _, _ -> state |> addToMessages message, Cmd.none
        | DismissMessage messageId, _, _ -> state |> dismissMessage messageId, Cmd.none
        | RemoteUiInput remoteUiInput, _, _ -> state |> handleRemoteUiInput remoteUiInput
        | Disconnected, _, _ -> state |> handleDisconnected
        | PreferencesInput preferencesInput, _, _ -> state |> handlePreferencesInput preferencesInput
        | ToggleTheme, _, (Some appState, _) ->
            let appState = { appState with Theme = match appState.Theme with | Light -> Dark | Dark -> Light }
            setBodyClass appState.Theme
            state |> updateAppState appState |> writePreferencesOrDefault
        | ToggleNavbarBurger, _, (Some appState, _) ->
            let appState = { appState with NavbarBurgerIsActive = not appState.NavbarBurgerIsActive }
            state |> updateAppState appState |> writePreferencesOrDefault
        | AutoSignInInput autoSignInInput, AutomaticallySigningIn automaticallySigningInState, _ -> state |> handleAutoSignInInput autoSignInInput automaticallySigningInState
        | UnauthInput(ShowUnauthPage About), Unauth unauthState, _ -> match unauthState.CurrentPage with | About -> state, Cmd.none
        | UnauthInput ShowSignInModal, Unauth unauthState, _ ->
            match unauthState.SignInModalState with
            | Some signInModalState -> state |> shouldNeverHappen (sprintf "Unexpected ShowSignInModal when SignInModalState is %A (%A)" signInModalState state)
            | None -> Unauth { unauthState with SignInModalState = Some(signInModalState None true None None) }, Cmd.none
        | UnauthInput(SignInModalInput signInModalInput), Unauth unauthState, _ -> state |> handleSignInModalInput signInModalInput unauthState
        | UnauthInput(SignInInput signInInput), Unauth unauthState, _ -> state |> handleSignInInput signInInput unauthState
        | AuthInput(ShowPage(UnauthPage About)), Auth authState, _ -> // TODO-NMB: Chat maintenance...
            match authState.CurrentPage with
            | UnauthPage About -> state, Cmd.none
            | AuthPage authPage ->
                setTitle About.Render.PAGE_TITLE
                // TODO-NMB: If authPage = Chat, notify that no longer current?...
                Auth { authState with CurrentPage = UnauthPage About } |> writePreferencesOrDefault
        | AuthInput(ShowPage(AuthPage Chat)), Auth authState, _ ->
            match authState.CurrentPage with
            | AuthPage Chat -> state, Cmd.none
            | page ->
                setTitle (Chat.Render.pageTitle authState.ChatState)
                Auth { authState with CurrentPage = AuthPage Chat } |> writePreferencesOrDefault
        | AuthInput(ShowPage(AuthPage UserAdmin)), Auth authState, _ -> // TODO-NMB: Chat maintenance...
            match authState.CurrentPage with
            | AuthPage UserAdmin -> state, Cmd.none
            | page ->
                let userAdminState, userAdminCmd =
                    match authState.UserAdminState with
                    | Some userAdminState -> userAdminState, Cmd.none
                    | None ->
                        let userAdminState, userAdminCmd = UserAdmin.State.initialize authState.AuthUser
                        userAdminState, userAdminCmd |> Cmd.map (UserAdminInput >> AuthInput)
                setTitle UserAdmin.Render.PAGE_TITLE
                let state, writePreferencesCmd = Auth { authState with CurrentPage = AuthPage UserAdmin ; UserAdminState = Some userAdminState } |> writePreferencesOrDefault
                // TODO-NMB: If page = AuthPage Chat, notify that no longer current?...
                state, Cmd.batch [ userAdminCmd ; writePreferencesCmd ]
        // TODO-NMB...| AuthInput(ChatInput chatInput), Auth authState ,_ -> state, Cmd.none
        | AuthInput(UserAdminInput(UserAdmin.Common.Input.AddMessage message)), Auth _, _ -> state |> addToMessages message, Cmd.none
        | AuthInput(UserAdminInput userAdminInput), Auth authState ,_ ->
            match authState.UserAdminState with
            | Some userAdminState ->
                let userAdminState, userAdminCmd = userAdminState |> UserAdmin.State.transition authState.AuthUser authState.UsersData userAdminInput
                Auth { authState with UserAdminState = Some userAdminState }, userAdminCmd |> Cmd.map (UserAdminInput >> AuthInput)
            | None -> state |> shouldNeverHappen (unexpectedInputWhenState input state)
        | AuthInput ShowChangePasswordModal, Auth authState, _ ->
            match authState.ChangePasswordModalState with
            | Some _ -> state |> shouldNeverHappen (sprintf "Unexpected ShowChangePasswordModal when ChangePasswordModalState is not None (%A)" state)
            | None -> Auth { authState with ChangePasswordModalState = Some(changePasswordModalState None) }, Cmd.none
        | AuthInput(ChangePasswordModalInput changePasswordModalInput), Auth authState, _ -> state |> handleChangePasswordModalInput changePasswordModalInput authState
        | AuthInput(ChangePasswordInput changePasswordInput), Auth authState, _ -> state |> handleChangePasswordInput changePasswordInput authState
        | AuthInput ShowChangeImageUrlModal, Auth authState, _ ->
            match authState.ChangeImageUrlModalState with
            | Some _ -> state |> shouldNeverHappen (sprintf "Unexpected ShowChangeImageUrlModal when ChangeImageUrlModalState is not None (%A)" state)
            | None -> Auth { authState with ChangeImageUrlModalState = Some(changeImageUrlModalState authState.AuthUser.User.ImageUrl) }, Cmd.none
        | AuthInput(ChangeImageUrlModalInput changeImageUrlModalInput), Auth authState, _ -> state |> handleChangeImageUrlModalInput changeImageUrlModalInput authState
        | AuthInput(ChangeImageUrlInput changeImageUrlInput), Auth authState, _ -> state |> handleChangeImageUrlInput changeImageUrlInput authState
        | AuthInput SignOut, Auth authState, _ ->
            let cmd = Cmd.OfAsync.either userApi.signOut (authState.ConnectionState.ConnectionId, authState.AuthUser.Jwt) SignOutResult SignOutExn |> Cmd.map (SignOutInput >> AuthInput)
            Auth { authState with SigningOut = true }, cmd
        | AuthInput(SignOutInput signOutInput), Auth authState, _ -> state |> handleSignOutInput signOutInput authState
        | AuthInput(GetUsersInput getUsersInput), Auth authState, _ -> state |> handleGetUsersInput getUsersInput authState
        | _ -> state |> shouldNeverHappen (unexpectedInputWhenState input state)
#if DEBUG
    if log then console.log("New state:", state)
#endif
    state, cmd
// #endregion
