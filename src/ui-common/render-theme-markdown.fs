module Aornota.Gibet.Ui.Common.Render.Theme.Markdown

open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Ui.Common.Marked
open Aornota.Gibet.Ui.Common.Render.Shared
open Aornota.Gibet.Ui.Common.Theme

open Fable.React
open Fable.React.Props

open Fulma

type private DangerousInnerHtml = { __html : string }

let [<Literal>] private MARKDOWN_CLASS = "markdown"

let private markdownContentT theme alignment inNotification (Markdown markdown) =
    let themeClass = themeClass theme
    let customClasses = [
        yield MARKDOWN_CLASS
        if inNotification then yield sprintf "%s-in-notification" themeClass else yield themeClass ]
    let customClass = match customClasses with | _ :: _ -> Some(ClassName(String.concat SPACE customClasses)) | [] -> None
    content alignment None [ div [
        match customClass with | Some customClass -> yield customClass | None -> ()
        yield DangerouslySetInnerHTML { __html = Globals.marked.parse markdown } ] [] ]

let markdownContentTLeft theme markdown = markdownContentT theme TextAlignment.Left false markdown
let markdownContentTCentred theme markdown = markdownContentT theme TextAlignment.Centered false markdown
let markdownContentTRight theme markdown = markdownContentT theme TextAlignment.Right false markdown

let markdownNotificationContentTLeft theme markdown = markdownContentT theme TextAlignment.Left true markdown
let markdownNotificationContentTCentred theme markdown = markdownContentT theme TextAlignment.Centered true markdown
let markdownNotificationContentTRight theme markdown = markdownContentT theme TextAlignment.Right true markdown
