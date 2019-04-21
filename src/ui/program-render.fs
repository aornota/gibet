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

let private button text onClick enabled loading =
    Button.button [
        yield Button.IsFullWidth
        yield IsLink |> Button.Color
        if enabled then yield onClick |> Button.OnClick
        yield loading |> Button.IsLoading
        yield Button.Props [ enabled |> not |> Disabled ]
    ] [ text |> str ]

let render state dispatch =
    let authUserData, usersData = state.AuthUserData, state.UsersData
    div [] [
        Navbar.navbar [ Navbar.Color IsInfo ] [ Navbar.Item.div [] [ Heading.h2 [] [ str GIBET ] ] ]
        Container.container [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ Heading.h3 [] [ "Work-in-progress..." |> str  ] ]
            Columns.columns [] [
                Column.column [] [
                    yield button "Test SignIn" (fun _ -> dispatch SignIn) (authUserData |> signedIn |> not) (authUserData |> pending)
                    let signInStatus =
                        match authUserData |> receivedData, authUserData |> failure with
                        | Some (authUser, mustChangePasswordReason), _ ->
                            let extra =
                                match mustChangePasswordReason with
                                | Some mustChangePasswordReason -> sprintf " -> must change password: _%A_" mustChangePasswordReason
                                | None -> ""
                            let (UserName userName) = authUser.User.UserName
                            sprintf "Signed in as **%A** (%A)%s" userName authUser.User.UserType extra |> Some
                        | _, Some error -> sprintf "**Error** -> _%s_" error |> Some
                        | _ -> None
                    match signInStatus with
                    | Some signInStatus ->
                        yield Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ signInStatus |> Markdown |> contentFromMarkdown ]
                    | None -> () ]
                Column.column [] [
                    yield button "Test GetUsers" (fun _ -> dispatch GetUsers) (authUserData |> signedIn) (usersData |> pending)
                    let getUsersStatus =
                        match usersData |> receivedData, usersData |> failure with
                        | Some users, _ ->
                            let users = users |> List.filter (fun user -> user.UserType <> PersonaNonGrata)
                            sprintf "**%i** User/s (excluding _personae non grata_)" users.Length |> Some
                        | _, Some error -> sprintf "**Error** -> _%s_" error |> Some
                        | _ -> None
                    match getUsersStatus with
                    | Some getUsersStatus ->
                        yield Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ getUsersStatus |> Markdown |> contentFromMarkdown ]
                    | None -> () ] ] ]
        Footer.footer [] [ Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ safeComponents ] ] ]
