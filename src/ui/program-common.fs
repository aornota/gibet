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
    ForcedSignOutReason : ForcedSignOutReason option
    SigningIn : bool
    SignInError : string option }

type UserData = User * bool * DateTimeOffset option

type AuthState = {
    AppState : AppState
    ConnectionState : ConnectionState
    AuthUser : AuthUser
    MustChangePasswordReason : MustChangePasswordReason option
    ActivityDebouncerState : Debouncer.State // note: will only be used when ACTIVITY is defined (see webpack.config.js)
    SigningOut : bool
    UsersData : RemoteData<UserData list, string> }

type State =
    | InitializingConnection of ConnectionId option
    | ReadingPreferences of ConnectionId option
    | RegisteringConnection of RegisteringConnectionState
    | AutomaticallySigningIn of AutomaticallySigningInState
    | Unauth of UnauthState
    | Auth of AuthState

let [<Literal>] GIBET = "gibet (α)"

let users (usersData:RemoteData<UserData list, string>) =
    match usersData |> receivedData with
    | Some(users, _) -> users
    | None -> []
let findUser userId (usersData:RemoteData<UserData list, string>) =
    match usersData |> receivedData with
    | Some(users, _) -> users |> List.tryFind (fun (user, _, _) -> user.UserId = userId)
    | None -> None
let exists userId (usersData:RemoteData<UserData list, string>) = match usersData |> findUser userId with | Some _ -> true | None -> false

let private addOrUpdateUser user usersRvn shouldExist (usersData:RemoteData<UserData list, string>) =
    match usersData with
    | Received(users, _) ->
        match usersData |> exists user.UserId, shouldExist with
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
        if usersData |> exists userId then
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
        if usersData |> exists userId then
            let users =
                users
                |> List.map (fun (user, otherSignedIn, lastActivity) ->
                    if user.UserId = userId then user, signedIn, lastActivity
                    else user, otherSignedIn, lastActivity)
            Received(users, rvn), None
        else usersData, Some (sprintf "updateSignedIn: %A not found" userId)
    | _ -> usersData, Some "updateSignIn: not Received"
