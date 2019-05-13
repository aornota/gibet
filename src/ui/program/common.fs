module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Pages
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared

open System

type LastUser = UserName * Jwt

type UnauthPage = | About
type AuthPage = | Chat | UserAdmin
type Page = | UnauthPage of UnauthPage | AuthPage of AuthPage

type Preferences = {
    AffinityId : AffinityId
    Theme : Theme
    LastUser : LastUser option
    LastPage : Page option }

type PreferencesInput =
    | ReadPreferencesResult of Result<Preferences, string> option
    | ReadPreferencesExn of exn
    | WritePreferencesOk of unit
    | WritePreferencesExn of exn

type AutoSignInInput =
    | AutoSignInResult of Result<AuthUser * MustChangePasswordReason option, string>
    | AutoSignInExn of exn

type SignInModalInput =
    | UserNameChanged of string
    | PasswordChanged of string
    | KeepMeSignedInChanged
    | SignIn
    | CancelSignIn
type SignInInput =
    | SignInResult of Result<AuthUser * MustChangePasswordReason option, string>
    | SignInExn of exn

type UnauthInput =
    | ShowUnauthPage of UnauthPage
    // Note: No need for AboutInput as About page has no inputs.
    | ShowSignInModal
    | SignInModalInput of SignInModalInput
    | SignInInput of SignInInput

type ChangePasswordModalInput =
    | NewPasswordChanged of string
    | ConfirmPasswordChanged of string
    | ChangePassword
    | CancelChangePassword
type ChangePasswordInput =
    | ChangePasswordResult of Result<UserName, string>
    | ChangePasswordExn of exn

type ChangeImageUrlModalInput =
    | ImageUrlChanged of string
    | ChangeImageUrl
    | CancelChangeImageUrl
type ChangeImageUrlInput =
    | ChangeImageUrlResult of Result<UserName * ImageChangeType option, string>
    | ChangeImageUrlExn of exn

type SignOutInput =
    | SignOutResult of Result<unit, string>
    | SignOutExn of exn

type GetUsersInput =
    | GetUsersResult of Result<(User * bool) list * Rvn, string>
    | GetUsersExn of exn

type AuthInput =
    | ShowPage of Page
    // Note: No need for AboutInput as About page has no inputs.
    | ChatInput of Chat.Common.Input
    | UserAdminInput of UserAdmin.Common.Input
    | ShowChangePasswordModal
    | ChangePasswordModalInput of ChangePasswordModalInput
    | ChangePasswordInput of ChangePasswordInput
    | ShowChangeImageUrlModal
    | ChangeImageUrlModalInput of ChangeImageUrlModalInput
    | ChangeImageUrlInput of ChangeImageUrlInput
    | SignOut
    | SignOutInput of SignOutInput
    | GetUsersInput of GetUsersInput

type Input =
    | OnTick // note: will only be used when TICK is defined (see webpack.config.js)
    | OnMouseMove // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    | AddMessage of Message
    | DismissMessage of MessageId
    | RemoteUiInput of RemoteUiInput
    | Disconnected
    | PreferencesInput of PreferencesInput
    | ToggleTheme
    | ToggleNavbarBurger
    | AutoSignInInput of AutoSignInInput
    | UnauthInput of UnauthInput
    | AuthInput of AuthInput

type AppState = {
    Ticks : int<tick> // note: will only be updated when TICK is defined (see webpack.config.js)
    AffinityId : AffinityId
    Theme : Theme
    NavbarBurgerIsActive : bool }

type RegisteringConnectionState = {
    Messages : Message list
    AppState : AppState
    LastUser : LastUser option
    LastPage : Page option
    ConnectionId : ConnectionId option }

type ConnectionState = {
    ConnectionId : ConnectionId
    ServerStarted : DateTimeOffset }

type AutomaticallySigningInState = {
    Messages : Message list
    AppState : AppState
    ConnectionState : ConnectionState
    LastUser : LastUser
    LastPage : Page option }

type SignInModalState = {
    UserNameKey : Guid
    UserName : string
    UserNameChanged : bool
    PasswordKey : Guid
    Password : string
    PasswordChanged : bool
    KeepMeSignedInKey : Guid
    KeepMeSignedIn : bool
    FocusPassword : bool
    AutoSignInError : (string * UserName) option
    ForcedSignOutReason : ForcedSignOutReason option
    ModalStatus : ModalStatus<string * UserName> option }

type UnauthState = {
    Messages : Message list
    AppState : AppState
    ConnectionState : ConnectionState
    CurrentPage : UnauthPage
    // Note: No need for AboutState as About page has no state.
    SignInModalState : SignInModalState option }

type ChangePasswordModalState = {
    NewPasswordKey : Guid
    NewPassword : string
    NewPasswordChanged : bool
    ConfirmPasswordKey : Guid
    ConfirmPassword : string
    ConfirmPasswordChanged : bool
    MustChangePasswordReason : MustChangePasswordReason option
    ModalStatus : ModalStatus<string> option }

type ChangeImageUrlModalState = {
    ImageUrlKey : Guid
    ImageUrl : string
    ImageUrlChanged : bool
    ModalStatus : ModalStatus<string> option }

type AuthState = {
    Messages : Message list
    AppState : AppState
    ConnectionState : ConnectionState
    AuthUser : AuthUser
    StaySignedIn : bool
    LastActivity : DateTime
    CurrentPage : Page
    ChatState : Chat.Common.State
    UserAdminState : UserAdmin.Common.State option
    ChangePasswordModalState : ChangePasswordModalState option
    ChangeImageUrlModalState : ChangeImageUrlModalState option
    SigningOut : bool
    UsersData : RemoteData<UserData list, string> }

type State =
    | InitializingConnection of Message list * reconnectingState : State option
    | ReadingPreferences of Message list * reconnectingState : State option
    | RegisteringConnection of RegisteringConnectionState
    | AutomaticallySigningIn of AutomaticallySigningInState
    | Unauth of UnauthState
    | Auth of AuthState

let private addOrUpdateUser user usersRvn shouldExist (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, _) ->
        match users |> exists user.UserId, shouldExist with
        | true, true ->
            let users =
                users
                |> List.map (fun (otherUser, signedIn, lastActivity) ->
                    if otherUser.UserId = user.UserId then user, signedIn, lastActivity
                    else otherUser, signedIn, lastActivity)
            Received(users, usersRvn), None
        | true, false -> usersData, Some (sprintf "addOrUpdateUser: %A already exists" user.UserId)
        | false, true -> usersData, Some (sprintf "addOrUpdateUser: %A not found" user.UserId)
        | false, false ->
            let users = (user, false, None) :: users
            Received(users, usersRvn), None
    | _ -> usersData, Some "addOrUpdateUser: not Received"
let addUser user usersRvn (usersData:RemoteData<UserData list, string>) =
    usersData |> addOrUpdateUser user usersRvn false
let updateUser user usersRvn (usersData:RemoteData<UserData list, string>) =
    usersData |> addOrUpdateUser user usersRvn true

let updateActivity userId (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, rvn) ->
        if users |> exists userId then
            let users =
                users
                |> List.map (fun (user, signedIn, lastActivity) ->
                    if user.UserId = userId then user, signedIn, Some DateTimeOffset.UtcNow
                    else user, signedIn, lastActivity)
            Received(users, rvn), None
        else usersData, Some (sprintf "updateActivity: %A not found" userId)
    | _ -> usersData, Some "updateActivity: not Received"
let updateSignedIn userId signedIn (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, rvn) ->
        if users |> exists userId then
            let users =
                users
                |> List.map (fun (user, otherSignedIn, lastActivity) ->
                    if user.UserId = userId then user, signedIn, lastActivity
                    else user, otherSignedIn, lastActivity)
            Received(users, rvn), None
        else usersData, Some (sprintf "updateSignedIn: %A not found" userId)
    | _ -> usersData, Some "updateSignIn: not Received"
