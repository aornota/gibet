module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.UI.Common.RemoteData
open Aornota.Gibet.UI.Common.Theme

open System

open Thoth.Elmish

type LastUser = UserName * Jwt

type Preferences = {
    AffinityId : AffinityId
    Theme : Theme
    LastUser : LastUser option }

type PreferencesInput =
    | ReadPreferencesResult of Result<Preferences, string> option
    | ReadPreferencesExn of exn
    | WritePreferencesOk of unit
    | WritePreferencesExn of exn

type SignInInput =
    | AutoSignInResult of Result<AuthUser * MustChangePasswordReason option, string>
    | AutoSignInExn of exn
    | SignInResult of Result<AuthUser * MustChangePasswordReason option, string>
    | SignInExn of exn

type SignOutInput =
    | SignOutResult of Result<unit, string>
    | SignOutExn of exn

type GetUsersInput =
    | GetUsersResult of Result<(User * bool) list * Rvn, string>
    | GetUsersExn of exn

(* type SignInModalInput =
    | UserNameChanged of string
    | PasswordChanged of string
    | SignIn
    | CancelSignIn *)

(* type UnauthInput =
    | ShowSignInModal
    | SignInModalInput of SignInModalInput *)

(* type ChangePasswordModalInput =
    | NewPasswordChanged of string
    | ConfirmPasswordChanged of string
    | ChangePassword
    | CancelChangePassword *)

(* type AuthInput =
    | ShowChangePasswordModal
    | ChangePasswordModalInput of ChangePasswordModalInput
    | SignOut *)

(* type AppInput =
    | UnauthInput of UnauthInput
    | AuthInput of AuthInput *)

type AppState = { // TODO-NMB?...StaticModal : StaticModal option
    Ticks : int<tick> // note: will only be updated when TICK is defined (see webpack.config.js)
    AffinityId : AffinityId
    Theme : Theme
    NavbarBurgerIsActive : bool }

// #region Input
type Input =
    | RegisterConnection of AppState * LastUser option * ConnectionId option
    | RemoteUiInput of RemoteUiInput
    | Disconnected
    | PreferencesInput of PreferencesInput
    | OnTick // note: will only be used when TICK is defined (see webpack.config.js)
    | OnMouseMove // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    | ActivityDebouncerSelfInput of Debouncer.SelfMessage<Input> // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    | OnDebouncedActivity // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    | ToggleTheme
    | ToggleNavbarBurger
    (* TODO-NMB?...
    | ShowStaticModal of staticModal : StaticModal
    | HideStaticModal *)
    // TODO-NMB...| AppInput of AppInput
    | TempSignIn // TEMP-NMB...
    | SignInInput of SignInInput
    | TempSignOut // TEMP-NMB...
    | SignOutInput of SignOutInput
    | TempGetUsers // TEMP-NMB...
    | GetUsersInput of GetUsersInput
// #endregion

type RegisteringConnectionState = {
    AppState : AppState
    LastUser : LastUser option
    ConnectionId : ConnectionId option }

type ConnectionState = {
    ConnectionId : ConnectionId
    ServerStarted : DateTimeOffset }

type AutomaticallySigningInState = {
    AppState : AppState
    ConnectionState : ConnectionState
    LastUser : LastUser }

type UnauthState = {
    AppState : AppState
    ConnectionState : ConnectionState
    SigningIn : bool
    SignInError : string option }

type AuthState = {
    AppState : AppState
    ConnectionState : ConnectionState
    AuthUser : AuthUser
    MustChangePasswordReason : MustChangePasswordReason option
    ActivityDebouncer : Debouncer.State // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    SigningOut : bool
    UsersData : RemoteData<(User * bool * DateTimeOffset option) list, string> }

type State =
    | InitializingConnection of ConnectionId option
    | ReadingPreferences of ConnectionId option
    | RegisteringConnection of RegisteringConnectionState
    | AutomaticallySigningIn of AutomaticallySigningInState
    | Unauth of UnauthState
    | Auth of AuthState

let [<Literal>] GIBET = "gibet (α)"

let users (usersData:RemoteData<(User * bool * DateTimeOffset option) list, string>) = // TEMP-NMB?...
    match usersData |> receivedData with
    | Some(users, _) -> users
    | None -> []
