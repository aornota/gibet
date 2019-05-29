module Aornota.Gibet.Ui.Program.State

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.Revision
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
open Aornota.Gibet.Ui.UsersApi

open System

open Elmish
open Elmish.Bridge

open Fable.Core.JS
open Fable.Import

open Thoth.Json

let [<Literal>] private KEY__APP_PREFERENCES = "gibet-ui-app-preferences"

// #region AUTO_SHOW_SIGN_IN_MODAL
let [<Literal>] private AUTO_SHOW_SIGN_IN_MODAL =
#if DEBUG
    true
#else
    false
#endif
// #endregion

let [<Literal>] private ACTIVITY_THROTTLE = 15.<second> // note: "ignored" if less than 5.<second>

let private activityThrottle = max ACTIVITY_THROTTLE 5.<second>

let private setBodyClass theme = Browser.document.body.className <- themeClass theme
let private setTitle pageTitle = Browser.document.title <- sprintf "%s | %s" GIBET pageTitle

let private readPreferencesCmd =
    let readPreferences () = async {
        (* TEMP-NMB...
        do! ifDebugSleepAsync 250 1000 *)
        return readJson(Key KEY__APP_PREFERENCES) |> Option.map (fun (Json json) -> Decode.Auto.fromString<Preferences> json) }
    Cmd.OfAsync.either readPreferences () ReadPreferencesResult ReadPreferencesExn |> Cmd.map PreferencesInput
let private writePreferencesCmd preferences =
    let writePreferences preferences = async {
        writeJson (Key KEY__APP_PREFERENCES) (Json(Encode.Auto.toString<Preferences>(SPACE_COUNT, preferences))) }
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
        SignInApiStatus = None
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
    ChangePasswordApiStatus = None }
let private changeImageUrlModalState imageUrl = {
    ImageUrlKey = Guid.NewGuid()
    ImageUrl = match imageUrl with | Some(ImageUrl imageUrl) -> imageUrl | None -> String.Empty
    ImageUrlChanged = false
    ChangeImageUrlApiStatus = None }
// #region authState
let private authState messages appState connectionState lastPage authUser staySignedIn mustChangePasswordReason =
    let changePasswordModalState =
        match mustChangePasswordReason with
        | Some mustChangePasswordReason -> Some(changePasswordModalState (Some mustChangePasswordReason))
        | None -> None
    let currentPage = match lastPage with | Some page -> page | _ -> UnauthPage About
    let chatState, chatCmd = Chat.State.initialize (currentPage = AuthPage Chat) authUser
    let userAdminState, userAdminCmd =
        match currentPage with
        | AuthPage UserAdmin ->
            let userAdminState, userAdminCmd = UserAdmin.State.initialize authUser
            Some userAdminState, userAdminCmd
        | _ -> None, Cmd.none
    let pageTitle =
        match currentPage with
        | UnauthPage About -> About.Render.PAGE_TITLE
        | AuthPage Chat -> chatState |> Chat.Render.pageTitle
        | AuthPage UserAdmin -> UserAdmin.Render.PAGE_TITLE
    setTitle pageTitle
#if ACTIVITY
    Bridge.Send RemoteServerInput.Activity
#endif
    let getUsersCmd, usersData =
        if canGetUsers authUser.User.UserType then
            let cmd = Cmd.OfAsync.either usersApi.getUsers (connectionState.ConnectionId, authUser.Jwt) GetUsersResult GetUsersExn |> Cmd.map (GetUsersApiInput >> AuthInput)
            cmd, Pending
        else Cmd.none, Failed NOT_ALLOWED
    let cmds = Cmd.batch [
        getUsersCmd
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
        UsersData = usersData }
    authState, cmds
// #endregion

let private addToMessages message state =
    match state with
    | InitializingConnection(messages, reconnectingState) -> InitializingConnection(message :: messages, reconnectingState)
    | ReadingPreferences(messages, reconnectingState) -> ReadingPreferences(message :: messages, reconnectingState)
    | RegisteringConnection registeringConnectionState -> RegisteringConnection { registeringConnectionState with Messages = message :: registeringConnectionState.Messages }
    | AutomaticallySigningIn automaticallySigningInState -> AutomaticallySigningIn { automaticallySigningInState with Messages = message :: automaticallySigningInState.Messages }
    | Unauth unauthState -> Unauth { unauthState with Messages = message :: unauthState.Messages }
    | Auth authState -> Auth { authState with Messages = message :: authState.Messages }
let private removeMessage messageId state = // note: silently ignore unknown messageId
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

let private addDebugError error state = state |> addMessage Debug (sprintf "%s -> %s" ERROR error)
// #region shouldNeverHappen
let private shouldNeverHappen error state : State * Cmd<Input> =
#if DEBUG
    state |> addDebugError (shouldNeverHappen error), Cmd.none
#else
    state |> addMessage Danger SOMETHING_HAS_GONE_WRONG, Cmd.none
#endif
// #endregion

let private userExists userId (users:UserData list) = match users |> tryFindUser userId with | Some _ -> true | None -> false

let private tryFindUser userId (usersData:RemoteData<UserData list, string>) = match usersData with | Received(users, _) -> users |> tryFindUser userId | _ -> None
let private addOrUpdateUser user usersRvn shouldExist (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, currentUsersRvn) ->
        match validateNextRvn currentUsersRvn usersRvn with
        | Some error -> Error(sprintf "addOrUpdateUser: %s" error)
        | None ->
            match users |> userExists user.UserId, shouldExist with
            | true, true ->
                let users =
                    users
                    |> List.map (fun (otherUser, signedIn, lastActivity) ->
                        if otherUser.UserId = user.UserId then user, signedIn, lastActivity
                        else otherUser, signedIn, lastActivity)
                Ok(Received(users, usersRvn))
            | true, false -> Error(sprintf "addOrUpdateUser: %A already exists" user.UserId)
            | false, true -> Error(sprintf "addOrUpdateUser: %A not found" user.UserId)
            | false, false ->
                let users = (user, false, None) :: users
                Ok(Received(users, usersRvn))
    | _ -> Error "addOrUpdateUser: not Received"
let private addUser user usersRvn usersData = usersData |> addOrUpdateUser user usersRvn false
let private updateUser user usersRvn usersData = usersData |> addOrUpdateUser user usersRvn true
let private updateActivity userId (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, rvn) ->
        if users |> userExists userId then
            let users =
                users
                |> List.map (fun (user, signedIn, lastActivity) ->
                    if user.UserId = userId then user, signedIn, Some DateTimeOffset.UtcNow
                    else user, signedIn, lastActivity)
            Ok(Received(users, rvn))
        else Error(sprintf "updateActivity: %A not found" userId)
    | _ -> Error "updateActivity: not Received"
let private updateSignedIn userId signedIn (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, rvn) ->
        if users |> userExists userId then
            let users =
                users
                |> List.map (fun (user, otherSignedIn, lastActivity) ->
                    if user.UserId = userId then user, signedIn, lastActivity
                    else user, otherSignedIn, lastActivity)
            Ok(Received(users, rvn))
        else Error(sprintf "updateSignedIn: %A not found" userId)
    | _ -> Error "updateSignedIn: not Received"

let private handleOnTick appState state : State * Cmd<Input> = // note: will only be used when TICK is defined (see webpack.config.js)
    match appState with
    | Some appState -> state |> updateAppState { appState with Ticks = appState.Ticks + 1<tick> }, Cmd.none
    | _ -> state, Cmd.none
let private handleOnMouseMove state : State * Cmd<Input> = // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    match state with
    | Auth authState ->
        let now, lastActivity = DateTime.Now, authState.LastActivity
        let elapsed = now - lastActivity
        let lastActivity =
            if elapsed.TotalSeconds * 1.<second> > activityThrottle then
                Bridge.Send RemoteServerInput.Activity
                now
            else lastActivity
        let chatState, chatCmd =
            if authState.CurrentPage = AuthPage Chat then
                match authState.ChatState with
                | Chat.Common.Ready(_, readyState) when readyState.UnseenCount > 0 ->
                    let chatState, chatCmd =
                        authState.ChatState |> Chat.State.transition authState.ConnectionState.ConnectionId authState.AuthUser authState.UsersData Chat.Common.ActivityWhenCurrentPage
                    chatState, chatCmd |> Cmd.map (ChatInput >> AuthInput)
                | _ -> authState.ChatState, Cmd.none
            else authState.ChatState, Cmd.none
        Auth { authState with LastActivity = lastActivity ; ChatState = chatState }, chatCmd
    | _ -> state, Cmd.none

let private handleRemoteUiInput remoteUiInput state =
    let toastImage imageUrl =
        match imageUrl with
        | Some(ImageUrl imageUrl) -> sprintf "<img src=\"%s\" width=\"48\" height=\"48\" style=\"float:left; margin-right: 10px\"><img>" imageUrl
        | None -> String.Empty
    match remoteUiInput, state with
    | Initialized, InitializingConnection(messages, reconnectingState) -> ReadingPreferences(messages, reconnectingState), readPreferencesCmd
    | Registered(connectionId, sinceServerStarted), RegisteringConnection registeringConnectionState ->
        let connectionState = { ConnectionId = connectionId ; ServerStarted = DateTimeOffset.UtcNow.AddSeconds(float -sinceServerStarted) }
        match registeringConnectionState.LastUser with
        | Some(userName, jwt) ->
            let cmd = Cmd.OfAsync.either usersApi.autoSignIn (connectionState.ConnectionId, jwt) AutoSignInResult AutoSignInExn |> Cmd.map AutoSignInApiInput
            AutomaticallySigningIn (automaticallySigningInState registeringConnectionState.Messages registeringConnectionState.AppState connectionState (userName, jwt)
                registeringConnectionState.LastPage), cmd
        | None ->
            let unauthState, cmd =
                unauthState registeringConnectionState.Messages registeringConnectionState.AppState connectionState registeringConnectionState.LastPage None None AUTO_SHOW_SIGN_IN_MODAL
            let state, writePreferencesCmd = Unauth unauthState |> writePreferencesOrDefault
            state, Cmd.batch [ cmd ; writePreferencesCmd ]
    | UserActivity userId, Auth authState -> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
        match authState.UsersData |> updateActivity userId with
        | Ok usersData -> Auth { authState with UsersData = usersData }, Cmd.none
        | Error error -> state |> shouldNeverHappen error
    | UserSignedIn userId, Auth authState ->
        match authState.UsersData |> updateSignedIn userId true with
        | Ok usersData ->
            let state = Auth { authState with UsersData = usersData }
            let toastCmd =
                match usersData |> tryFindUser userId with
                | Some(user, _, _) ->
                    let (UserName userName) = user.UserName
                    sprintf "%s<strong>%s</strong> has signed in" (toastImage user.ImageUrl) userName |> infoToastCmd
                | None -> Cmd.none
            state, toastCmd
        | Error error -> state |> shouldNeverHappen error
    | UserSignedOut userId, Auth authState ->
        match authState.UsersData |> updateSignedIn userId false with
        | Ok usersData ->
            let state = Auth { authState with UsersData = usersData }
            let toastCmd =
                match usersData |> tryFindUser userId with
                | Some(user, _, _) ->
                    let (UserName userName) = user.UserName
                    sprintf "%s<strong>%s</strong> has signed out" (toastImage user.ImageUrl) userName |> infoToastCmd
                | None -> Cmd.none
            state, toastCmd
        | Error error -> state |> shouldNeverHappen error
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
    | UserUpdated(user, userUpdateType, usersRvn), Auth authState ->
        let authUser = authState.AuthUser
        let authUser = if user.UserId = authUser.User.UserId then { authUser with User = user } else authUser
        match authState.UsersData |> updateUser user usersRvn with
        | Ok usersData ->
            let state = Auth { authState with AuthUser = authUser ; UsersData = usersData }
            state, ifDebug (sprintf "%A updated (%A) -> UsersData now %A" user.UserId userUpdateType usersRvn |> infoToastCmd) Cmd.none
        | Error error -> state |> shouldNeverHappen error
    | UserAdded(user, usersRvn), Auth authState ->
        match authState.UsersData |> addUser user usersRvn with
        | Ok usersData ->
            let state = Auth { authState with UsersData = usersData }
            state, ifDebug (sprintf "%A added -> UsersData now %A" user.UserId usersRvn |> infoToastCmd) Cmd.none
        | Error error -> state |> shouldNeverHappen error
    | RemoteChatInput remoteChatInput, Auth authState ->
        let chatState, chatCmd =
            authState.ChatState |> Chat.State.transition authState.ConnectionState.ConnectionId authState.AuthUser authState.UsersData (Chat.Common.RemoteChatInput remoteChatInput)
        Auth { authState with ChatState = chatState }, chatCmd |> Cmd.map (ChatInput >> AuthInput)
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
    | ReadPreferencesExn exn, ReadingPreferences _ -> state, PreferencesInput(ReadPreferencesResult(Some(Error exn.Message))) |> Cmd.ofMsg
    | WritePreferencesOk _, _ -> state, Cmd.none
    | WritePreferencesExn exn, _ -> state |> addDebugError (sprintf "WritePreferencesExn -> %s" exn.Message), Cmd.none
    | _ -> state |> shouldNeverHappen (unexpectedInputWhenState preferencesInput state)

let private handleAutoSignInApiInput autoSignInApiInput (automaticallySigningInState:AutomaticallySigningInState) state =
    match autoSignInApiInput with
    | AutoSignInResult(Ok(authUser, mustChangePasswordReason)) ->
        let authState, cmd =
            authState automaticallySigningInState.Messages automaticallySigningInState.AppState automaticallySigningInState.ConnectionState automaticallySigningInState.LastPage
                authUser true mustChangePasswordReason
        let state, writePreferencesCmd = Auth authState |> writePreferencesOrDefault
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
    | AutoSignInExn exn -> state, AutoSignInApiInput(AutoSignInResult(Error exn.Message)) |> Cmd.ofMsg

let private handleSignInModalInput signInModalInput (unauthState:UnauthState) state =
    match unauthState.SignInModalState with
    | Some signInModalState ->
        match signInModalInput, signInModalState.SignInApiStatus with
        | _, Some ApiPending -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState.SignInApiStatus is Pending (%A)" signInModalInput unauthState)
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
                Cmd.OfAsync.either usersApi.signIn (unauthState.ConnectionState.ConnectionId, userName, password) SignInResult SignInExn |> Cmd.map (SignInApiInput >> UnauthInput)
            let signInModalState = { signInModalState with AutoSignInError = None ; ForcedSignOutReason = None ; SignInApiStatus = Some ApiPending }
            Unauth { unauthState with SignInModalState = Some signInModalState }, cmd
        | CloseSignInModal, _ -> Unauth { unauthState with SignInModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState is None (%A)" signInModalInput unauthState)
let private handleSignInApiInput signInApiInput (unauthState:UnauthState) state =
    match unauthState.SignInModalState with
    | Some signInModalState ->
        match signInModalState.SignInApiStatus with
        | Some ApiPending ->
            match signInApiInput with
            | SignInResult(Ok(authUser, mustChangePasswordReason)) ->
                let authState, cmd =
                    authState unauthState.Messages unauthState.AppState unauthState.ConnectionState (Some(UnauthPage unauthState.CurrentPage)) authUser signInModalState.KeepMeSignedIn
                        mustChangePasswordReason
                let state, writePreferencesCmd = Auth authState |> writePreferencesOrDefault
                let (UserName userName) = authUser.User.UserName
                let cmds = Cmd.batch [
                    cmd
                    sprintf "You have signed in as <strong>%s</strong>" userName |> successToastCmd
                    writePreferencesCmd ]
                state, cmds
            | SignInResult(Error error) ->
                let signInModalState = { signInModalState with FocusPassword = true ; SignInApiStatus = Some(ApiFailed(error, UserName signInModalState.UserName)) }
                Unauth { unauthState with SignInModalState = Some signInModalState }, Cmd.none // no need for toast (since error will be displayed on SignInModal)
            | SignInExn exn -> state, UnauthInput(SignInApiInput(SignInResult(Error exn.Message))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState.SignInApiStatus is not Pending (%A)" signInApiInput unauthState)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when SignInModalState is None (%A)" signInApiInput unauthState)

let private handleChangePasswordModalInput changePasswordModalInput (authState:AuthState) state =
    match authState.ChangePasswordModalState with
    | Some changePasswordModalState ->
        match changePasswordModalInput, changePasswordModalState.ChangePasswordApiStatus with
        | _, Some ApiPending ->
            state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState.ChangePasswordApiStatus is Pending (%A)" changePasswordModalInput authState)
        | NewPasswordChanged newPassword, _ ->
            let changePasswordModalState = { changePasswordModalState with NewPassword = newPassword ; NewPasswordChanged = true }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none
        | ConfirmPasswordChanged confirmPassword, _ ->
            let changePasswordModalState = { changePasswordModalState with ConfirmPassword = confirmPassword ; ConfirmPasswordChanged = true }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none
        | ChangePassword, _ ->
            let password = Password(changePasswordModalState.NewPassword.Trim())
            let cmd =
                Cmd.OfAsync.either usersApi.changePassword (authState.AuthUser.Jwt, password, authState.AuthUser.User.Rvn) ChangePasswordResult ChangePasswordExn
                |> Cmd.map (ChangePasswordApiInput >> AuthInput)
            let changePasswordModalState = { changePasswordModalState with ChangePasswordApiStatus = Some ApiPending }
            Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, cmd
        | CloseChangePasswordModal, _ -> Auth { authState with ChangePasswordModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState is None (%A)" changePasswordModalInput authState)
let private handleChangePasswordApiInput changePasswordApiInput (authState:AuthState) state =
    match authState.ChangePasswordModalState with
    | Some changePasswordModalState ->
        match changePasswordModalState.ChangePasswordApiStatus with
        | Some ApiPending ->
            match changePasswordApiInput with
            | ChangePasswordResult(Ok(UserName userName)) ->
                Auth { authState with ChangePasswordModalState = None }, sprintf "Password changed for <strong>%s</strong>" userName |> successToastCmd
            | ChangePasswordResult(Error error) ->
                let changePasswordModalState = { changePasswordModalState with ChangePasswordApiStatus = Some(ApiFailed error) }
                Auth { authState with ChangePasswordModalState = Some changePasswordModalState }, Cmd.none // no need for toast (since error will be displayed on ChangePasswordModal)
            | ChangePasswordExn exn -> state, AuthInput(ChangePasswordApiInput(ChangePasswordResult(Error exn.Message))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState.ChangePasswordApiStatus is not Pending (%A)" changePasswordApiInput authState)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangePasswordModalState is None (%A)" changePasswordApiInput authState)

let private handleChangeImageUrlModalInput changeImageUrlModalInput (authState:AuthState) state =
    match authState.ChangeImageUrlModalState with
    | Some changeImageUrlModalState ->
        match changeImageUrlModalInput, changeImageUrlModalState.ChangeImageUrlApiStatus with
        | _, Some ApiPending ->
            state |> shouldNeverHappen (sprintf "Unexpected %A when ChangeImageUrlModalState.ChangeImageUrlApiStatus is Pending (%A)" changeImageUrlModalInput authState)
        | ImageUrlChanged imageUrl, _ ->
            let changeImageUrlModalState = { changeImageUrlModalState with ImageUrl = imageUrl ; ImageUrlChanged = true }
            Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, Cmd.none
        | ChangeImageUrl, _ ->
            let imageUrl = if String.IsNullOrWhiteSpace(changeImageUrlModalState.ImageUrl) then None else Some(ImageUrl changeImageUrlModalState.ImageUrl)
            let cmd =
                Cmd.OfAsync.either usersApi.changeImageUrl (authState.AuthUser.Jwt, imageUrl, authState.AuthUser.User.Rvn) ChangeImageUrlResult ChangeImageUrlExn
                |> Cmd.map (ChangeImageUrlApiInput >> AuthInput)
            let changeImageUrlModalState = { changeImageUrlModalState with ChangeImageUrlApiStatus = Some ApiPending }
            Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, cmd
        | CloseChangeImageUrlModal, _ -> Auth { authState with ChangeImageUrlModalState = None }, Cmd.none
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when ChangeImageUrlModalState is None (%A)" changeImageUrlModalInput authState)
let private handleChangeImageUrlApiInput changeImageUrlApiInput (authState:AuthState) state =
    match authState.ChangeImageUrlModalState with
    | Some changeImageUrlModalState ->
        match changeImageUrlModalState.ChangeImageUrlApiStatus with
        | Some ApiPending ->
            match changeImageUrlApiInput with
            | ChangeImageUrlResult(Ok(UserName userName, imageChangeType)) ->
                Auth { authState with ChangeImageUrlModalState = None }, sprintf "Image %s for <strong>%s</strong>" (changeType imageChangeType) userName |> successToastCmd
            | ChangeImageUrlResult(Error error) ->
                let changeImageUrlModalState = { changeImageUrlModalState with ChangeImageUrlApiStatus = Some(ApiFailed error) }
                Auth { authState with ChangeImageUrlModalState = Some changeImageUrlModalState }, Cmd.none // no need for toast (since error will be displayed on ChangeImageUrlModal)
            | ChangeImageUrlExn exn -> state, AuthInput(ChangeImageUrlApiInput(ChangeImageUrlResult(Error exn.Message))) |> Cmd.ofMsg
        | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when changeImageUrlModalState.ChangeImageUrlApiStatus is not Pending (%A)" changeImageUrlApiInput authState)
    | None -> state |> shouldNeverHappen (sprintf "Unexpected %A when changeImageUrlModalState is None (%A)" changeImageUrlApiInput authState)

let private handleSignOutApiInput signOutApiInput (authState:AuthState) state =
    if authState.SigningOut then
        match signOutApiInput with
        | SignOutResult(Ok _) ->
            let unauthState, cmd = unauthState authState.Messages authState.AppState authState.ConnectionState (Some authState.CurrentPage) None None false
            let state, writePreferencesCmd = Unauth unauthState |> writePreferencesOrDefault
            let cmds = Cmd.batch [
                cmd
                "You have signed out" |> successToastCmd
                writePreferencesCmd ]
            state, cmds
        | SignOutResult(Error error) -> state |> addDebugError (sprintf "SignOutResult error -> %s" error), AuthInput(SignOutApiInput(SignOutResult(Ok()))) |> Cmd.ofMsg
        | SignOutExn exn -> state, AuthInput(SignOutApiInput(SignOutResult(Error exn.Message))) |> Cmd.ofMsg
    else state |> shouldNeverHappen (sprintf "Unexpected %A when not SigningOut (%A)" signOutApiInput authState)

let private handleGetUsersApiInput getUsersApiInput (authState:AuthState) state =
    match authState.UsersData with
    | Pending ->
        match getUsersApiInput with
        | GetUsersResult(Ok(users, usersRvn)) ->
            let users = users |> List.map (fun (user, signedIn) -> user, signedIn, None)
            Auth { authState with UsersData = Received(users, usersRvn) }, ifDebug (sprintf "Got %i user/s -> UsersData %A" users.Length usersRvn |> infoToastCmd) Cmd.none
        | GetUsersResult(Error error) -> Auth { authState with UsersData = Failed error }, ifDebug (sprintf "GetUsersResult error -> %s" error |> errorToastCmd) Cmd.none
        | GetUsersExn exn -> state, AuthInput(GetUsersApiInput(GetUsersResult(Error exn.Message))) |> Cmd.ofMsg
    | _ -> state |> shouldNeverHappen (sprintf "Unexpected %A when UsersData is not Pending (%A)" getUsersApiInput authState)

let private updateChatIsCurrentPage isCurrentPage connectionId authUser usersData chatState =
    let chatState, chatCmd = chatState |> Chat.State.transition connectionId authUser usersData (Chat.Common.UpdateIsCurrentPage isCurrentPage)
    if isCurrentPage then setTitle (chatState |> Chat.Render.pageTitle)
    chatState, chatCmd |> Cmd.map (ChatInput >> AuthInput)

// #region initialize
let initialize () : State * Cmd<Input> =
    let state = InitializingConnection([], None)
#if DEBUG
    console.log("Initial state:", state)
#endif
    state, Cmd.none
// #endregion

// #region transition
let transition input state =
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
        | DismissMessage messageId, _, _ -> state |> removeMessage messageId, Cmd.none
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
        | AutoSignInApiInput autoSignInApiInput, AutomaticallySigningIn automaticallySigningInState, _ -> state |> handleAutoSignInApiInput autoSignInApiInput automaticallySigningInState
        | UnauthInput(ShowUnauthPage About), Unauth unauthState, _ -> match unauthState.CurrentPage with | About -> state, Cmd.none
        | UnauthInput ShowSignInModal, Unauth unauthState, _ ->
            match unauthState.SignInModalState with
            | Some signInModalState -> state |> shouldNeverHappen (sprintf "Unexpected ShowSignInModal when SignInModalState is %A (%A)" signInModalState state)
            | None -> Unauth { unauthState with SignInModalState = Some(signInModalState None true None None) }, Cmd.none
        | UnauthInput(SignInModalInput signInModalInput), Unauth unauthState, _ -> state |> handleSignInModalInput signInModalInput unauthState
        | UnauthInput(SignInApiInput signInApiInput), Unauth unauthState, _ -> state |> handleSignInApiInput signInApiInput unauthState
        | AuthInput(ShowPage(UnauthPage About)), Auth authState, _ ->
            match authState.CurrentPage with
            | UnauthPage About -> state, Cmd.none
            | AuthPage authPage ->
                setTitle About.Render.PAGE_TITLE
                let chatState, chatCmd =
                    if authPage = Chat then authState.ChatState |> updateChatIsCurrentPage false authState.ConnectionState.ConnectionId authState.AuthUser authState.UsersData
                    else authState.ChatState, Cmd.none
                let state, writePreferencesCmd = Auth { authState with CurrentPage = UnauthPage About ; ChatState = chatState } |> writePreferencesOrDefault
                state, Cmd.batch [ chatCmd ; writePreferencesCmd ]
        | AuthInput(ShowPage(AuthPage Chat)), Auth authState, _ ->
            match authState.CurrentPage with
            | AuthPage Chat -> state, Cmd.none
            | _ ->
                let chatState, chatCmd = authState.ChatState |> updateChatIsCurrentPage true authState.ConnectionState.ConnectionId authState.AuthUser authState.UsersData
                let state, writePreferencesCmd = Auth { authState with CurrentPage = AuthPage Chat ; ChatState = chatState } |> writePreferencesOrDefault
                state, Cmd.batch [ chatCmd ; writePreferencesCmd ]
        | AuthInput(ShowPage(AuthPage UserAdmin)), Auth authState, _ ->
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
                let chatState, chatCmd =
                    if page = AuthPage Chat then authState.ChatState |> updateChatIsCurrentPage false authState.ConnectionState.ConnectionId authState.AuthUser authState.UsersData
                    else authState.ChatState, Cmd.none
                let authState = { authState with CurrentPage = AuthPage UserAdmin ; ChatState = chatState ; UserAdminState = Some userAdminState }
                let state, writePreferencesCmd = Auth authState |> writePreferencesOrDefault
                state, Cmd.batch [ userAdminCmd ; chatCmd ; writePreferencesCmd ]
        | AuthInput(ChatInput(Chat.Common.Input.AddMessage message)), Auth _, _ -> state |> addToMessages message, Cmd.none
        | AuthInput(ChatInput Chat.Common.Input.UpdatePageTitle), Auth authState, _ ->
            if authState.CurrentPage = AuthPage Chat then setTitle (authState.ChatState |> Chat.Render.pageTitle)
            state, Cmd.none
        | AuthInput(ChatInput chatInput), Auth authState ,_ ->
            let chatState, chatCmd =
                authState.ChatState |> Chat.State.transition authState.ConnectionState.ConnectionId authState.AuthUser authState.UsersData chatInput
            Auth { authState with ChatState = chatState }, chatCmd |> Cmd.map (ChatInput >> AuthInput)
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
        | AuthInput(ChangePasswordApiInput changePasswordApiInput), Auth authState, _ -> state |> handleChangePasswordApiInput changePasswordApiInput authState
        | AuthInput ShowChangeImageUrlModal, Auth authState, _ ->
            match authState.ChangeImageUrlModalState with
            | Some _ -> state |> shouldNeverHappen (sprintf "Unexpected ShowChangeImageUrlModal when ChangeImageUrlModalState is not None (%A)" state)
            | None -> Auth { authState with ChangeImageUrlModalState = Some(changeImageUrlModalState authState.AuthUser.User.ImageUrl) }, Cmd.none
        | AuthInput(ChangeImageUrlModalInput changeImageUrlModalInput), Auth authState, _ -> state |> handleChangeImageUrlModalInput changeImageUrlModalInput authState
        | AuthInput(ChangeImageUrlApiInput changeImageUrlApiInput), Auth authState, _ -> state |> handleChangeImageUrlApiInput changeImageUrlApiInput authState
        | AuthInput SignOut, Auth authState, _ ->
            let cmd = Cmd.OfAsync.either usersApi.signOut (authState.ConnectionState.ConnectionId, authState.AuthUser.Jwt) SignOutResult SignOutExn |> Cmd.map (SignOutApiInput >> AuthInput)
            Auth { authState with SigningOut = true }, cmd
        | AuthInput(SignOutApiInput signOutApiInput), Auth authState, _ -> state |> handleSignOutApiInput signOutApiInput authState
        | AuthInput(GetUsersApiInput getUsersApiInput), Auth authState, _ -> state |> handleGetUsersApiInput getUsersApiInput authState
        | _ -> state |> shouldNeverHappen (unexpectedInputWhenState input state)
#if DEBUG
    if log then console.log("New state:", state)
#endif
    state, cmd
// #endregion
