module Aornota.Gibet.Ui.Pages.UserAdmin.State

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.RemoteData
open Aornota.Gibet.Ui.Common.ShouldNeverHappen
open Aornota.Gibet.Ui.Common.Toast
open Aornota.Gibet.Ui.Pages.UserAdmin.Common
open Aornota.Gibet.Ui.Shared
open Aornota.Gibet.Ui.User.Shared
open Aornota.Gibet.Ui.UserApi

open Elmish

let private addMessageCmd messageType text = AddMessage(messageDismissable messageType text) |> Cmd.ofMsg

let private addDebugErrorCmd error = addMessageCmd Debug (sprintf "ERROR -> %s" error)
// #region shouldNeverHappenCmd
let private shouldNeverHappenCmd error =
#if DEBUG
    addDebugErrorCmd (shouldNeverHappen error)
#else
    addMessageCmd Danger SOMETHING_HAS_GONE_WRONG
#endif
// #endregion

let initialize (_:AuthUser) : State * Cmd<Input> =
    let state = {
        CreateUsersModalState = None
        ResetPasswordModalState = None
        ChangeUserTypeModalState = None }
    state, Cmd.none

let transition (authUser:AuthUser) (usersData:RemoteData<UserData list, string>) input (state:State) : State * Cmd<Input> =
    match input, usersData with
    // Note: AddMessage will have been handled by Program.State.transition.
    | ShowCreateUsersModal, Received(users, _) -> state, "ShowCreateUsersModal: NYI" |> warningToastCmd
    | ShowResetPasswordModal userId, Received(users, _) -> state, "ShowResetPasswordModal: NYI" |> warningToastCmd
    | ShowChangeUserTypeModal userId, Received(users, _) -> state, "ShowChangeUserTypeModal: NYI" |> warningToastCmd
    | _ -> state, shouldNeverHappenCmd (unexpectedInputWhenState input state)
