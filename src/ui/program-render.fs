module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Markdown // TEMP-NMB...
open Aornota.Gibet.UI.Common.Render.Markdown // TEMP-NMB...

open Fable.React
open Fable.React.Props

open Fulma

let private safeComponents =
    let components =
        span [] [
            a [ Href "https://github.com/giraffe-fsharp/Giraffe/" ] [ str "Giraffe" ]
            str ", "
            a [ Href "http://fable.io/" ] [ str "Fable" ]
            str ", "
            a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
            str ", "
            a [ Href "https://fulma.github.io/Fulma/" ] [ str "Fulma" ]
            str ", "
            a [ Href "https://zaid-ajaj.github.io/Fable.Remoting/" ] [ str "Fable.Remoting" ]
        ]
    p  [] [
        strong [] [ str GIBET ]
        str " powered by: "
        components
    ]

let private show = function
    | { Counter = Some counter } -> string counter.Value
    | { Counter = None } -> "Loading..."

let private button txt onClick =
    Button.button [
        Button.IsFullWidth
        Button.Color IsPrimary
        Button.OnClick onClick
    ] [ str txt ]

let render (state : State) (dispatch : Input -> unit) =
    div [] [
        Navbar.navbar [ Navbar.Color IsPrimary ] [
            Navbar.Item.div [] [
                Heading.h2 [] [ str GIBET ]
            ]
        ]
        Container.container [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                Heading.h3 [] [ str ("Press buttons to manipulate counter: " + show state) ]
            ]
            Columns.columns [] [
                Column.column [] [ button "-" (fun _ -> dispatch Decrement) ]
                Column.column [] [ button "+" (fun _ -> dispatch Increment) ]
            ]
        ]
        Footer.footer [] [
            Markdown "Testing _**Markdown**_: <i>sanitized</i>..." |> contentFromMarkdown // TEMP-MNB...
            Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                safeComponents
            ]
        ]
    ]
