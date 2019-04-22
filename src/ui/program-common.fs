module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.UI.Common.RemoteData

type State = {
    AuthUserData : RemoteData<AuthUser * MustChangePasswordReason option, string>
    UsersData : RemoteData<User list, string> }

type Input =
    | RemoteUi of RemoteUiInput
    | Disconnected
    | SignIn
    | SignInResult of Result<Result<AuthUser * MustChangePasswordReason option, string>, exn>
    | GetUsers
    | GetUsersResult of Result<Result<User list, string>, exn>

let [<Literal>] GIBET = "gibet (α)"

let signedIn (authUserData:RemoteData<AuthUser * MustChangePasswordReason option, string>) =
    authUserData |> received
let authUser (authUserData:RemoteData<AuthUser * MustChangePasswordReason option, string>) =
    match authUserData |> receivedData with
    | Some (authUser, _) -> authUser |> Some
    | None -> None
let mustChangePasswordReason (authUserData:RemoteData<AuthUser * MustChangePasswordReason option, string>) =
    match authUserData |> receivedData with
    | Some (_, mustChangePasswordReason) -> mustChangePasswordReason
    | None -> None

let users (usersData:RemoteData<User list, string>) =
    match usersData |> receivedData with
    | Some users -> users
    | None -> []
