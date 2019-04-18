module Aornota.UI.Common.Toasts

open Elmish
open Elmish.Toastr

let [<Literal>] private DEFAULT_TOAST_TIMEOUT = 3000

let private toastCmd toCmd toastText : Cmd<_> =
    Toastr.message toastText |> Toastr.position TopRight |> Toastr.timeout DEFAULT_TOAST_TIMEOUT |> Toastr.hideEasing Easing.Swing |> Toastr.showCloseButton |> toCmd

let infoToastCmd toastText = toastText |> toastCmd Toastr.info
let successToastCmd toastText = toastText |> toastCmd Toastr.success
let warningToastCmd toastText = toastText |> toastCmd Toastr.warning
let errorToastCmd toastText = toastText |> toastCmd Toastr.error
