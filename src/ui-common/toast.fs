module Aornota.Gibet.Ui.Common.Toast

open Elmish
open Elmish.Toastr

let [<Literal>] private DEFAULT_TOAST_TIMEOUT = 3000

let private toastCmd toCmd text : Cmd<_> =
    Toastr.message text
    |> Toastr.position TopRight
    |> Toastr.timeout DEFAULT_TOAST_TIMEOUT
    |> Toastr.hideEasing Easing.Swing
    |> Toastr.showCloseButton
    |> toCmd

let infoToastCmd text = toastCmd Toastr.info text
let successToastCmd text = toastCmd Toastr.success text
let warningToastCmd text = toastCmd Toastr.warning text
let errorToastCmd text = toastCmd Toastr.error text
