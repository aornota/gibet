module Aornota.Gibet.UI.Common.LocalStorage

open Aornota.Gibet.Common.Json

open Browser

type Key = | Key of key : string

let readJson (Key key) = key |> localStorage.getItem |> unbox |> Option.map(string >> Json)
let writeJson (Key key) (Json json) = (key, json) |> localStorage.setItem
let delete (Key key) = key |> localStorage.removeItem
