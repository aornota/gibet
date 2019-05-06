module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Render.Theme
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Common.TimestampHelper
open Aornota.Gibet.Ui.Common.Tooltip
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Shared

open System

open Elmish.React.Common

open Fable.React.Helpers

open Fulma

type private HeaderState =
    | Registering
    | NotSignedIn of ConnectionState
    | SigningIn of ConnectionState * UserName * auto : bool
    | SignInError of ConnectionState * UserName * auto : bool
    | SignedIn of ConnectionState * AuthUser
    | SigningOut of ConnectionState

type private HeaderData = {
    AppState : AppState
    HeaderState : HeaderState }

// #region renderHeader
let private renderHeader headerData dispatch =
    let theme, burgerIsActive = headerData.AppState.Theme, headerData.AppState.NavbarBurgerIsActive
    let spinner = iconSmaller ICON__SPINNER_PULSE
    let paraTStatus colour = paraT theme TextSize.Is7 colour TextWeight.Normal
    // #region serverStatus
    let serverStatus =
        match headerData.HeaderState with
        | Registering -> Some(navbarItem [ paraTStatus IsGreyDark [ str "Registering connection... " ; spinner ] ])
        | NotSignedIn connectionState | SigningIn(connectionState, _, _) | SignInError(connectionState, _, _) | SignedIn(connectionState, _) | SigningOut connectionState ->
            let timestampText =
#if TICK
                ago connectionState.ServerStarted.LocalDateTime
#else
                sprintf "on %s" (connectionState.ServerStarted.LocalDateTime |> dateAndTimeText)
#endif
            Some(navbarItem [ paraTStatus IsGreyDark [ str (sprintf "Server started %s" timestampText) ] ])
    // #endregion
    let statusItems =
        let separator = paraT theme TextSize.Is7 IsBlack TextWeight.SemiBold [ str "|" ]
        let signIn = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowSignInModal |> UnauthInput |> AppInput) ) [ str "Sign in" ] ]
        match headerData.HeaderState with
        | Registering -> []
        | NotSignedIn _ -> [ separator ; paraTStatus IsGreyDarker [ str "Not signed in" ] ; signIn ]
        | SigningIn(_, UserName userName, auto) ->
            let text = if auto then "Automatically signing in as " else "Signing in as "
            [ separator ; paraTStatus IsInfo [ str text ; bold userName ; str "... " ; spinner ] ]
        | SignInError(_, UserName userName, auto) ->
            let text = if auto then "Unable to automatically sign in as " else "Unable to sign in as "
            [ separator ; paraTStatus (if auto then IsWarning else IsDanger) [ str text ; bold userName ] ]
        | SignedIn(_, authUser) ->
            let (UserName userName) = authUser.User.UserName
            [ separator ; paraTStatus IsSuccess [ str "Signed in as " ; bold userName ] ]
        | SigningOut _ -> [ separator ; paraTStatus IsInfo [ str "Signing out... " ; spinner ] ]
    let authUserDropDown =
        match headerData.HeaderState with
        | SignedIn(_, authUser) ->
            let changePassword = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangePasswordModal |> AuthInput |> AppInput) ) [ str "Change password" ] ]
            let signOut = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (SignOut |> AuthInput |> AppInput) ) [ str "Sign out" ] ]
            Some(navbarDropDownT theme (iconSmaller ICON__USER) [
                let userId, userType = authUser.User.UserId, authUser.User.UserType
                match canChangePassword userId (userId, userType) with
                | true -> yield navbarDropDownItemT theme false [ changePassword ]
                | false -> ()
                yield navbarDropDownItemT theme false [ signOut ] ])
        | _ -> None
    let adminDropDown =
        match headerData.HeaderState with
        | SignedIn(_, authUser) ->
            let showUserAdminPage = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (TempShowUserAdminPage |> AuthInput |> AppInput) ) [ str "User administration" ] ]
            match canAdministerUsers authUser.User.UserType with
            | true -> Some(navbarDropDownT theme (iconSmaller ICON__ADMIN) [ navbarDropDownItemT theme false [ showUserAdminPage ] ])
            | false -> None
        |  _ -> None
    // TODO-NMB: pageTabs?...
    let toggleThemeInteraction = Clickable(fun _ -> dispatch ToggleTheme)
    let toggleThemeTooltip = tooltip (if burgerIsActive then TooltipRight else TooltipLeft) IsInfo (sprintf "Switch to %s theme" (match theme with | Light -> "dark" | Dark -> "light"))
    navbarT theme IsLight [
        navbarBrand [
            yield navbarItem [ image "gibet-24x24.png" Image.Is24x24 ]
            yield navbarItem [ paraT theme TextSize.Is7 IsBlack TextWeight.Bold [ str GIBET ] ]
            yield ofOption serverStatus
            yield! statusItems |> List.map (fun element -> navbarItem [ element ])
            yield navbarBurger (fun _ -> dispatch ToggleNavbarBurger) burgerIsActive ]
        navbarMenuT theme burgerIsActive [
            navbarStart [
                ofOption authUserDropDown
                // TODO-NMB...yield navbarItem [ tabs theme { tabsDefault with Tabs = pageTabs } ]
                ofOption adminDropDown ]
            navbarEnd [
#if TICK
                navbarItem [ paraT theme TextSize.Is7 IsGreyDarker TextWeight.Normal [ str (DateTimeOffset.UtcNow.LocalDateTime.ToString("HH:mm:ss")) ] ]
#endif
                navbarItem [ buttonT theme (Some IsSmall) IsDark toggleThemeInteraction true false (Some toggleThemeTooltip) [ iconSmall ICON__THEME ] ] ] ] ]
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

let private renderReconnectingModal theme =
    cardModalT theme None [ contentCentred [
        notificationT theme IsDanger None [ paraTSmaller theme [ bold "The connection to the server has been lost" ] ]
        br
        paraT theme TextSize.Is6 IsInfo TextWeight.Normal [ bold "Attempting to reconnect... " ; iconSmaller ICON__SPINNER_PULSE ] ] ]

let private renderSignInModal (theme, signInModalState:SignInModalState) dispatch =
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.Bold [ str "Sign in" ] ] ]
    let onDismiss, isSigningIn, signInInteraction, onEnter, userNameError, passwordError =
        let onDismiss, onEnter = (fun _ -> dispatch CancelSignIn), (fun _ -> dispatch SignIn)
        match signInModalState.ModalStatus with
        | Some ModalPending -> None, true, Loading, ignore, None, None
        | _ ->
            let userName, password = signInModalState.UserName, signInModalState.Password
            let userNameIsBlank, passwordIsBlank = String.IsNullOrWhiteSpace userName, String.IsNullOrWhiteSpace password
            let userNameError = if signInModalState.UserNameChanged then validateUserName true (UserName userName) [] else None
            let passwordError = if signInModalState.PasswordChanged then validatePassword true (Password password) else None
            match userNameIsBlank, userNameError, passwordIsBlank, passwordError with
            | false, None, false, None -> Some onDismiss, false, Clickable(onEnter), onEnter, userNameError, passwordError
            | _ -> Some onDismiss, false, NotEnabled, onEnter, userNameError, passwordError
    let body = [
        match signInModalState.AutoSignInError, signInModalState.ForcedSignOutReason, signInModalState.ModalStatus with
        | Some(error, UserName userName), _, _ ->
            yield notificationT theme IsWarning None [
                contentCentred [ paraTSmaller theme [ str "Unable to automatically sign in as " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | None, Some forcedSignOutReason, _ ->
            yield notificationT theme IsWarning None [
                contentCentred [ paraTSmaller theme [ str (sprintf "You have been signed out because %s" (forcedSignOutBecause forcedSignOutReason)) ] ] ]
            yield br
        | None, None, Some(ModalFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to sign in as " ; bold signInModalState.UserName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | None, None, _ -> ()
        yield contentCentred [
            paraTSmaller theme [ str "Please enter your user name and password" ]
            fieldGroupedCentred [
                textBoxT theme signInModalState.UserNameKey signInModalState.UserName (Some ICON__USER) false userNameError [] (not signInModalState.FocusPassword) isSigningIn
                    (UserNameChanged >> dispatch) ignore ]
            fieldGroupedCentred [
                textBoxT theme signInModalState.PasswordKey signInModalState.Password (Some ICON__PASSWORD) true passwordError [] signInModalState.FocusPassword isSigningIn
                    (PasswordChanged >> dispatch) onEnter ]
            fieldGroupedCentred [
                paraTSmallest theme [ buttonT theme (Some IsSmall) IsLink signInInteraction false false None [ str "Sign in" ] ] ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

// TODO-NMB...let private renderChangePasswordModal (theme, changePasswordState:ChangePasswordState) dispatch =

let private renderSigningOutModal theme =
    cardModalT theme None [ contentCentred [ paraT theme TextSize.Is6 IsInfo TextWeight.Normal [ bold "Signing out... " ; iconSmall ICON__SPINNER_PULSE ] ] ]

let render state dispatch =
    let state, reconnecting =
        match state with
        | InitializingConnection (Some reconnectingState) | ReadingPreferences (Some reconnectingState) -> reconnectingState, true
        | _ -> state, false
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> pageLoaderT Light IsLink
    | RegisteringConnection registeringConnectionState ->
        let theme = registeringConnectionState.AppState.Theme
        let headerData = {
            AppState = registeringConnectionState.AppState
            HeaderState = Registering }
        div [] [
            yield lazyView2 renderHeader headerData dispatch
            yield divVerticalSpace 25
            yield div [] [ containerFluid [ contentCentred [ paraT theme TextSize.Is7 IsLink TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ] ] ] ]
            yield divVerticalSpace 15
            yield lazyView renderFooter theme ]
    | AutomaticallySigningIn automaticallySigningInState ->
        let theme = automaticallySigningInState.AppState.Theme
        let headerData = {
            AppState = automaticallySigningInState.AppState
            HeaderState = SigningIn(automaticallySigningInState.ConnectionState, fst automaticallySigningInState.LastUser, true) }
        div [] [
            yield lazyView2 renderHeader headerData dispatch
            yield divVerticalSpace 25
            yield div [] [ containerFluid [ contentCentred [ paraT theme TextSize.Is7 IsInfo TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ] ] ] ]
            yield divVerticalSpace 15
            yield lazyView renderFooter theme ]
    | Unauth unauthState ->
        let theme = unauthState.AppState.Theme
        let headerData = {
            AppState = unauthState.AppState
            HeaderState =
                match unauthState.SignInModalState with
                | Some signInModalState ->
                    match signInModalState.AutoSignInError, signInModalState.ModalStatus with
                    | Some(_, userName), _ -> SignInError(unauthState.ConnectionState, userName, true)
                    | None, Some ModalPending -> SigningIn(unauthState.ConnectionState, UserName signInModalState.UserName, false)
                    | None, Some(ModalFailed _) -> SignInError(unauthState.ConnectionState, UserName signInModalState.UserName, false)
                    | None, None -> NotSignedIn unauthState.ConnectionState
                | None -> NotSignedIn unauthState.ConnectionState }
        div [] [
            yield lazyView2 renderHeader headerData dispatch
            yield divVerticalSpace 25
            yield div [] [ containerFluid [ contentCentred [ paraT theme TextSize.Is4 IsBlack TextWeight.SemiBold [ str "Work-in-progress..." ] ] ] ] // TEMP-NMB...
            yield divVerticalSpace 15
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme
            else
                match unauthState.SignInModalState with
                | Some signInModalState -> yield lazyView2 renderSignInModal (theme, signInModalState) (SignInModalInput >> UnauthInput >> AppInput >> dispatch)
                | None -> () ]
    | Auth authState ->
        let theme = authState.AppState.Theme
        let headerData = {
            AppState = authState.AppState
            HeaderState =
                if authState.SigningOut then SigningOut authState.ConnectionState
                else SignedIn(authState.ConnectionState, authState.AuthUser) }
        div [] [
            yield lazyView2 renderHeader headerData dispatch
            yield divVerticalSpace 25
            yield div [] [ containerFluid [ contentCentred [ paraT theme TextSize.Is4 IsBlack TextWeight.SemiBold [ str "Work-in-progress..." ] ] ] ] // TEMP-NMB...
            yield divVerticalSpace 15
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme
            else if authState.SigningOut then yield lazyView renderSigningOutModal theme ]
