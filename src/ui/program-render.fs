module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Domain.User // TEMP-NMB...
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
            a [ Href "https://fable.io/" ] [ str "Fable" ]
            str ", "
            a [ Href "https://elmish.github.io/elmish/" ] [ str "Elmish" ]
            str ", "
            a [ Href "https://fulma.github.io/Fulma/" ] [ str "Fulma" ]
            str ", "
            a [ Href "https://zaid-ajaj.github.io/Fable.Remoting/" ] [ str "Fable.Remoting" ] ]
    p [] [
        strong [] [ str GIBET ]
        str " powered by: "
        components ]

let private button text onClick enabled =
    Button.button [
        yield Button.IsFullWidth
        yield IsLink |> Button.Color
        if enabled then yield onClick |> Button.OnClick
        yield Button.Props [ enabled |> not |> Disabled ]
    ] [ text |> str ]

let render state dispatch =
    div [] [
        Navbar.navbar [ Navbar.Color IsInfo ] [ Navbar.Item.div [] [ Heading.h2 [] [ str GIBET ] ] ]
        Container.container [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ Heading.h3 [] [ "Work-in-progress..." |> str  ] ]
            Columns.columns [] [
                Column.column [] [
                    yield button "Test SignIn" (fun _ -> dispatch SignIn) state.AuthUser.IsNone
                    let signInStatus =
                        match state.SignInError with
                        | Some error -> sprintf "**Error** -> _%s_" error |> Some
                        | None ->
                            match state.AuthUser with
                            | Some authUser -> sprintf "Signed in as **%A**" authUser.User.UserName |> Some
                            | None -> None
                    match signInStatus with
                    | Some signInStatus ->
                        yield Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ signInStatus |> Markdown |> contentFromMarkdown ]
                    | None -> () ]
                Column.column [] [
                    yield button "Test GetUsers" (fun _ -> dispatch GetUsers) state.AuthUser.IsSome
                    let getUsersStatus =
                        match state.GetUsersError with
                        | Some error -> sprintf "**Error** -> _%s_" error |> Some
                        | None ->
                            match state.Users with
                            | Some users ->
                                let users = users |> List.filter (fun user -> user.UserType <> PersonaNonGrata)
                                sprintf "**%i** User/s (excluding _personae non grata_)" users.Length |> Some
                            | None -> None
                    match getUsersStatus with
                    | Some getUsersStatus ->
                        yield Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ getUsersStatus |> Markdown |> contentFromMarkdown ]
                    | None -> () ] ] ]
        Footer.footer [] [ Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ safeComponents ] ] ]
