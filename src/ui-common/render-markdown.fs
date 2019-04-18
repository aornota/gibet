module Aornota.Gibet.UI.Common.Render.Markdown

open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.UI.Common.Marked

open Fable.React
open Fable.React.Props

open Fulma

// TODO-NMB: Rethink this (e.g. Content.Modifiers &c.)...

type private DangerousInnerHtml = { __html : string }

let [<Literal>] private MARKDOWN_CLASS = "markdown"
let [<Literal>] private SPACE = " " // TEMP-NMB...

let private contentFromMarkdown' inNotification (Markdown markdown) =
    let className = "light" // TEMP-NMB...
    let customClasses = [
        yield MARKDOWN_CLASS
        if inNotification then yield sprintf "%s-in-notification" className else yield className ]
    let customClass = match customClasses with | _ :: _ -> ClassName(String.concat SPACE customClasses) |> Some | [] -> None
    Content.content [] [
        div [
            match customClass with | Some customClass -> yield customClass :> IHTMLProp | None -> ()
            yield DangerouslySetInnerHTML { __html = Globals.marked.parse markdown } :> IHTMLProp
        ] [] ]

let contentFromMarkdown markdown = markdown |> contentFromMarkdown' false

let notificationContentFromMarkdown markdown = markdown |> contentFromMarkdown' true
