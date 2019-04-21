module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.UI.Common.RemoteData

type State = {
    AuthUserData : RemoteData<AuthUser * MustChangePasswordReason option, string>
    UsersData : RemoteData<User list, string> }

type Input =
    | SignIn
    | SignInResult of Result<Result<AuthUser * MustChangePasswordReason option, string>, exn>
    | GetUsers
    | GetUsersResult of Result<Result<User list, string>, exn>

let [<Literal>] GIBET = "gibet (α)"

let pending remoteData =
    match remoteData with | Pending -> true | _ -> false
let received remoteData =
    match remoteData with | Received _ -> true | _ -> false
let receivedData remoteData =
    match remoteData with | Received data -> data |> Some | _ -> None
let failed remoteData =
    match remoteData with | Failed _ -> true | _ -> false
let failure remoteData =
    match remoteData with | Failed failure -> failure |> Some | _ -> None

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
