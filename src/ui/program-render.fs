module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Common.Domain.User // TEMP-NMB...
open Aornota.Gibet.Common.Markdown // TEMP-NMB...
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.UI.Common.Icon
open Aornota.Gibet.UI.Common.RemoteData // TEMP-NMB...
open Aornota.Gibet.UI.Common.Render
open Aornota.Gibet.UI.Common.Render.Markdown // TEMP-NMB...
open Aornota.Gibet.UI.Common.Render.Theme
open Aornota.Gibet.UI.Common.Theme
open Aornota.Gibet.UI.Common.Tooltip
open Aornota.Gibet.Ui.Program.Common

open System

open Elmish.React.Common

open Fulma

type private HeaderData = | TODO_NMB

// #region renderHeader
let private renderHeader (theme, headerData, _:int<tick>) dispatch =
    // TODO-NMB...
    let navbarBurgerIsActive = false // TEMP-NMB...
    let toggleThemeInteraction = Clickable(fun _ -> dispatch ToggleTheme)
    let toggleThemeTooltip = tooltip (if navbarBurgerIsActive then TooltipRight else TooltipLeft) IsInfo (sprintf "Switch to %s theme" (match theme with | Light -> "dark" | Dark -> "light"))
    navbar theme IsLight [
        navbarBrand [
            yield navbarItem [ image "gibet-24x24.png" Image.Is24x24 ]
            yield navbarItem [ paraT theme TextSize.Is7 IsBlack TextWeight.Bold [ str GIBET ] ]
            // TODO-NMB...yield Rct.ofOption serverStarted
            // TODO-NMB...yield! statusInfo |> List.map (fun element -> navbarItem [ element ])
            // TODO-NMB...yield navbarBurger (fun _ -> ToggleNavbarBurger |> dispatch) navbarBurgerIsActive
        ]
        // TODO-NMB: navbarMenu (i.e. for BurgerIsActive stuff)?...
        // TODO-NMB: navbarStart (i.e. for authUserDropDown &c.)?...
        navbarEnd [
#if TICK
            navbarItem [ paraT theme TextSize.Is7 IsGreyDarker TextWeight.Normal [ str (DateTimeOffset.UtcNow.LocalDateTime.ToString("HH:mm:ss")) ] ]
#endif
            navbarItem [ buttonT theme (Some IsSmall) IsDark toggleThemeInteraction true false (Some toggleThemeTooltip) [ iconSmall ICON__THEME ] ] ] ]
// #endregion

let private renderFooter theme =
    let credits =
        paraTSmallest theme [
            linkTNewWindow theme "https://github.com/aornota/gibet/" [ str "Written" ]
            str " in "
            linkTNewWindow theme "http://fsharp.org/" [ str "F#"]
            str " using "
            linkTNewWindow theme "http://fable.io/" [ str "Fable"]
            str ", "
            linkTNewWindow theme "https://elmish.github.io/" [ str "Elmish"]
            str ", "
            linkTNewWindow theme "https://github.com/Fulma/Fulma/" [ str "Fulma"]
            str " / "
            linkTNewWindow theme "https://bulma.io/" [ str "Bulma"]
            str ", "
            linkTNewWindow theme "https://github.com/Zaid-Ajaj/Fable.Remoting/" [ str "Fable.Remoting"]
            str ", "
            linkTNewWindow theme "https://github.com/Nhowka/Elmish.Bridge/" [ str "Elmish.Bridge"]
            str ", "
            linkTNewWindow theme "https://github.com/giraffe-fsharp/Giraffe/" [ str "Giraffe"]
            str " and "
            linkTNewWindow theme "https://docs.microsoft.com/en-us/aspnet/core/" [ str "ASP.NET Core"]
            str ". Developed in "
            linkTNewWindow theme "https://code.visualstudio.com/" [ str "Visual Studio Code"]
            str " using "
            linkTNewWindow theme "http://ionide.io/docs/" [ str "Ionide-fsharp"]
            str ". Best viewed with "
            linkTNewWindow theme "https://www.google.com/chrome/" [ str "Chrome"]
            str ". Not especially mobile-friendly." ]
    footerT theme true [ contentCentred [ credits ] ]

let private renderReconnectingModal theme = // TODO-NMB: Improve this...
    cardModalT theme None [ contentCentred [
        paraT theme TextSize.Is6 IsDanger TextWeight.Bold [ str "The connection to the server has been lost. Reconnecting..." ]
        iconSmaller ICON__SPINNER_PULSE ] ]

// #region TEMP-NMB...
let private tempSignIn theme enabled pending status dispatch =
    let tempSignInInteraction =
        match enabled, pending with
        | true, true -> Loading
        | true, false -> Clickable(fun _ -> dispatch TempSignIn)
        | false, _ -> NotEnabled
    column [
        yield buttonT theme None IsLink tempSignInInteraction false false None [ str "TempSignIn" ]
        match status with | Some status -> yield contentCentred [ contentFromMarkdown (Markdown status) ] | None -> () ]
let private tempSignOut theme enabled pending dispatch =
    let tempSignOutInteraction =
        match enabled, pending with
        | true, true -> Loading
        | true, false -> Clickable(fun _ -> dispatch TempSignOut)
        | false, _ -> NotEnabled
    column [ buttonT theme None IsLink tempSignOutInteraction false false None [ str "TempSignOut" ] ]
let private tempGetUsers theme enabled pending status dispatch =
    let tempGetUsersInteraction =
        match enabled, pending with
        | true, true -> Loading
        | true, false -> Clickable(fun _ -> dispatch TempGetUsers)
        | false, _ -> NotEnabled
    column [
        yield buttonT theme None IsLink tempGetUsersInteraction false false None [ str "TempGetUsers" ]
        match status with | Some status -> yield contentCentred [ contentFromMarkdown (Markdown status) ] | None -> () ]
let private tempContent theme tempSignIn tempSignOut tempGetUsers =
    div [] [
        containerFluid [ contentCentred [
            paraT theme TextSize.Is4 IsBlack TextWeight.SemiBold [ str "Work-in-progress..." ]
            columns [
                tempSignIn
                tempSignOut
                tempGetUsers ] ] ] ]
// #endregion

let render state dispatch =
    // TODO-NMB: Rework (cf. sweepstake-2018 &c.)...
    // TODO-NMB: Is "has-navbar-fixed-top" working properly?...
    let state, reconnecting =
        match state with
        | InitializingConnection (Some reconnectingState) | ReadingPreferences (Some reconnectingState) -> reconnectingState, true
        | _ -> state, false
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> pageLoader Light IsLink
    | RegisteringConnection registeringConnectionState ->
        let theme = registeringConnectionState.AppState.Theme
        div [] [
            yield lazyView2 renderHeader (theme, TODO_NMB, registeringConnectionState.AppState.Ticks) dispatch
            yield divVerticalSpace 40
            yield contentCentred [ iconLarger ICON__SPINNER_PULSE ]
            yield lazyView renderFooter theme ]
    | AutomaticallySigningIn automaticallySigningInState ->
        let theme = automaticallySigningInState.AppState.Theme
        div [] [
            yield lazyView2 renderHeader (theme, TODO_NMB, automaticallySigningInState.AppState.Ticks) dispatch
            yield divVerticalSpace 40
            yield contentCentred [ iconLarger ICON__SPINNER_PULSE ]
            yield lazyView renderFooter theme ]
    | Unauth unauthState ->
        let theme = unauthState.AppState.Theme
        // #region TEMP-NMB...
        let signInStatus =
            match unauthState.SigningIn, unauthState.SignInError with
            | true, _ -> None
            | false, Some signInError -> Some(sprintf "**Error** -> _%s_" signInError)
            | false, None -> None
        let tempSignIn = tempSignIn theme true unauthState.SigningIn signInStatus dispatch
        let tempSignOut = tempSignOut theme false false dispatch
        let tempGetUsers = tempGetUsers theme false false None dispatch
        // #endregion
        div [] [
            yield lazyView2 renderHeader (theme, TODO_NMB, unauthState.AppState.Ticks) dispatch
            yield divVerticalSpace 20
            yield tempContent theme tempSignIn tempSignOut tempGetUsers // TEMP-NMB...
            yield divVerticalSpace 20
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme ]
    | Auth authState ->
        let theme = authState.AppState.Theme
        // #region TEMP-NMB...
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
        let tempSignIn = tempSignIn theme false false (Some signInStatus) dispatch
        let tempSignOut = tempSignOut theme (not getUsersPending) signingOut dispatch
        let tempGetUsers = tempGetUsers theme (not signingOut) getUsersPending getUsersStatus dispatch
        // #endregion
        div [] [
            yield lazyView2 renderHeader (theme, TODO_NMB, authState.AppState.Ticks) dispatch
            yield divVerticalSpace 20
            yield tempContent theme tempSignIn tempSignOut tempGetUsers // TEMP-NMB...
            yield divVerticalSpace 20
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme ]
