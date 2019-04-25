module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Common.Domain.User // TEMP-NMB...
open Aornota.Gibet.Common.Markdown // TEMP-NMB...
open Aornota.Gibet.UI.Common.RemoteData // TEMP-NMB...
open Aornota.Gibet.UI.Common.Render.Markdown // TEMP-NMB...
open Aornota.Gibet.UI.Common.Theme
open Aornota.Gibet.Ui.Program.Common

open System

open Fable.React
open Fable.React.Props

open Fulma
open Fulma.Extensions.Wikiki

let private button text onClick enabled loading = // TEMP-NMB...
    Button.button [
        yield Button.IsFullWidth
        yield Button.Color IsLink
        if enabled then yield Button.OnClick onClick
        yield Button.IsLoading loading
        yield Button.Props [ Disabled(not enabled) ]
    ] [ str text ]
let private pageLoader semantic = // TEMP-NMB...
    PageLoader.pageLoader [
        PageLoader.Color semantic
        PageLoader.IsActive true ] []

let private safeComponents = // TEMP-NMB...
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

let private signIn enabled pending status dispatch = // TEMP-NMB...
    Column.column [] [
        yield button "TempSignIn" (fun _ -> dispatch TempSignIn) enabled pending
        match status with
        | Some status ->
            yield Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ contentFromMarkdown (Markdown status) ]
        | None -> () ]
let private signOut enabled pending dispatch = // TEMP-NMB...
    Column.column [] [
        button "TempSignOut" (fun _ -> dispatch TempSignOut) enabled pending ]
let private getUsers enabled pending status dispatch = // TEMP-NMB...
    Column.column [] [
        yield button "TempGetUsers" (fun _ -> dispatch TempGetUsers) enabled pending
        match status with
        | Some status ->
            yield Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ contentFromMarkdown (Markdown status) ]
        | None -> () ]

// #region content
let private content tempSignIn tempSignOut tempGetUsers = // TEMP-NMB...
#if TICK
    let extra = sprintf " %s" (DateTimeOffset.UtcNow.LocalDateTime.ToString("HH:mm:ss"))
#else
    let extra = String.Empty
#endif
    div [] [
        Navbar.navbar [ Navbar.Color IsLight ] [ Navbar.Item.div [] [ Heading.h3 [] [ str GIBET ] ] ]
        Container.container [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ Heading.h4 [] [ str (sprintf "Work-in-progress...%s" extra) ] ]
            Columns.columns [] [
                tempSignIn
                tempSignOut
                tempGetUsers ] ]
        Footer.footer [] [ Content.content [ Content.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [ safeComponents ] ] ]
// #endregion

let render state dispatch = // TODO-NMB: Rework (cf. sweepstake-2018 &c.)...
    match state with
    | InitializingConnection None | ReadingPreferences _ -> pageLoader IsLink
    | InitializingConnection (Some _) -> pageLoader IsDanger // TODO-NMB: Use something other than pageLoader...
    | RegisteringConnection registeringConnectionState -> pageLoader (registeringConnectionState.AppState.Theme |> transformColour IsLight) // TODO-NMB: Use something other than pageLoader...
    | AutomaticallySigningIn _ -> pageLoader IsPrimary // TODO-NMB: Use something other than pageLoader...
    | Unauth unauthState -> // TEMP-NMB...
        let signInStatus =
            match unauthState.SigningIn, unauthState.SignInError with
            | true, _ -> None
            | false, Some signInError -> Some(sprintf "**Error** -> _%s_" signInError)
            | false, None -> None
        let signIn = signIn true unauthState.SigningIn signInStatus dispatch
        let signOut = signOut false false dispatch
        let getUsers = getUsers false false None dispatch
        content signIn signOut getUsers
    | Auth authState -> // TEMP-NMB...
        let signInStatus =
            let extra =
                match authState.MustChangePasswordReason with
                | Some mustChangePasswordReason -> sprintf " -> must change password: _%A_" mustChangePasswordReason
                | None -> String.Empty
            let authUser = authState.AuthUser
            let (UserName userName) = authUser.User.UserName
            sprintf "Signed in as **%A** (%A)%s" userName authUser.User.UserType extra
        let signingOut = authState.SigningOut
        let getUsersPending, getUsersStatus =
            let usersData = authState.UsersData
            let pending = usersData |> pending
            let error = usersData |> error
            let allowedUsers = usersData |> users |> List.filter (fun (user, _, _) -> user.UserType <> PersonaNonGrata)
            let signedInUserCount = usersData |> users |> List.filter (fun (_, signedIn, _) -> signedIn) |> List.length
            match pending, error, allowedUsers with
            | true, _, _ -> pending, None
            | false, Some error, _ -> pending, Some(sprintf "**Error** -> _%s_" error)
            | false, None, h :: t -> pending, Some(sprintf "**%i** User/s (excluding _personae non grata_) -> %i signed in" (h :: t).Length signedInUserCount)
            | false, None, [] -> pending, None
        let signIn = signIn false false (Some signInStatus) dispatch
        let signOut = signOut (not getUsersPending) signingOut dispatch
        let getUsers = getUsers (not signingOut) getUsersPending getUsersStatus dispatch
        content signIn signOut getUsers
