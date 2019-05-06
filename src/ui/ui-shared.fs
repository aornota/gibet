module Aornota.Gibet.Ui.Shared

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Ui.Common.RemoteData

open System

type ModalStatus =
    | ModalPending
    | ModalFailed of string

type UserData = User * bool * DateTimeOffset option

let users (usersData:RemoteData<UserData list, string>) =
    match usersData |> receivedData with
    | Some(users, _) -> users
    | None -> []
let findUser userId (usersData:RemoteData<UserData list, string>) =
    match usersData |> receivedData with
    | Some(users, _) -> users |> List.tryFind (fun (user, _, _) -> user.UserId = userId)
    | None -> None
let exists userId (usersData:RemoteData<UserData list, string>) = match usersData |> findUser userId with | Some _ -> true | None -> false
