module Aornota.Gibet.Ui.Pages.About.Render

open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Render.Theme.Markdown
open Aornota.Gibet.Ui.Pages.About.MarkdownLiterals

let render theme = div [] [ containerFluid [ contentFromMarkdownLeft theme (Markdown READ_ME) ] ]
