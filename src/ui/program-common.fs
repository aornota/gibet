module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Api.Connection
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.Affinity
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.UI.Common.RemoteData
open Aornota.Gibet.UI.Common.Theme

open System

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
    | GetUsersResult of Result<User list, string>
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
    AffinityId : AffinityId
    Theme : Theme
    NavbarBurgerIsActive : bool }

type Input =
    | RegisterConnection of AppState * LastUser option
    | RemoteUiInput of RemoteUiInput
    | Disconnected
    | PreferencesInput of PreferencesInput
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

type RegisteringConnectionState = {
    AppState : AppState
    LastUser : LastUser option }

type ConnectionState = {
    Connection : Connection
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
    SigningOut : bool
    UsersData : RemoteData<User list, string> }

type State =
    | InitializingConnection of reconnecting : bool
    | ReadingPreferences
    | RegisteringConnection of RegisteringConnectionState
    | AutomaticallySigningIn of AutomaticallySigningInState
    | Unauth of UnauthState
    | Auth of AuthState

let [<Literal>] GIBET = "gibet (α)"

let users (usersData:RemoteData<User list, string>) = // TEMP-NMB?...
    match usersData |> receivedData with
    | Some users -> users
    | None -> []
