module Aornota.Gibet.Ui.Common.Render.Theme.Markdown

open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Ui.Common.Marked
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Theme

open Fable.React
open Fable.React.Props

open Fulma

type private DangerousInnerHtml = { __html : string }

let [<Literal>] private MARKDOWN_CLASS = "markdown"

let private contentFromMarkdown theme alignment inNotification (Markdown markdown) =
    let themeClass = themeClass theme
    let customClasses = [
        yield MARKDOWN_CLASS
        if inNotification then yield sprintf "%s-in-notification" themeClass else yield themeClass ]
    let customClass = match customClasses with | _ :: _ -> Some(ClassName(String.concat SPACE customClasses)) | [] -> None
    content alignment [ div [
        match customClass with | Some customClass -> yield customClass | None -> ()
        yield DangerouslySetInnerHTML { __html = Globals.marked.parse markdown } ] [] ]

let contentFromMarkdownCentred theme markdown = contentFromMarkdown theme TextAlignment.Centered false markdown
let contentFromMarkdownLeft theme markdown = contentFromMarkdown theme TextAlignment.Left false markdown
let contentFromMarkdownRight theme markdown = contentFromMarkdown theme TextAlignment.Right false markdown

let notificationContentFromMarkdownCentred theme markdown = contentFromMarkdown theme TextAlignment.Centered true markdown
let notificationContentFromMarkdownLeft theme markdown = contentFromMarkdown theme TextAlignment.Left true markdown
let notificationContentFromMarkdownRight theme markdown = contentFromMarkdown theme TextAlignment.Right true markdown
