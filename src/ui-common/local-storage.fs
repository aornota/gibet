module Aornota.Gibet.UI.Common.LocalStorage

open Aornota.Gibet.Common.Json

open Browser

type Key = | Key of string

let readJson (Key key) = unbox(localStorage.getItem key) |> Option.map (string >> Json)
let writeJson (Key key) (Json json) = localStorage.setItem(key, json)
let delete (Key key) = localStorage.removeItem key
