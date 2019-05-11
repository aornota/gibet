module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Render.Theme
open Aornota.Gibet.Ui.Common.Theme
open Aornota.Gibet.Ui.Common.TimestampHelper
open Aornota.Gibet.Ui.Common.Tooltip
open Aornota.Gibet.Ui.Pages
open Aornota.Gibet.Ui.Program.Common
open Aornota.Gibet.Ui.Shared

open System

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

let [<Literal>] private IMAGE__GIBET = "gibet-24x24.png"

// #region renderHeader
let private renderHeader (headerData, _:int<tick>) dispatch =
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
        let signIn = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowSignInModal |> UnauthInput) ) [ str "Sign in" ] ]
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
            let image, currentlyNone =
                match authUser.User.ImageUrl with
                | Some(ImageUrl imageUrl) -> image imageUrl Image.Is24x24, false
                | None -> iconSmaller ICON__USER, true
            let changePassword = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangePasswordModal |> AuthInput)) [ str "Change password" ] ]
            let changeImageUrl = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangeImageUrlModal |> AuthInput)) [
                    str (sprintf "%s image" (if currentlyNone then "Choose" else "Change (or remove)"))] ]
            let signOut = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (SignOut |> AuthInput) ) [ str "Sign out" ] ]
            Some(navbarDropDownT theme image [
                let userId, userType = authUser.User.UserId, authUser.User.UserType
                match canChangePassword userId (userId, userType) with
                | true -> yield navbarDropDownItemT theme false [ changePassword ]
                | false -> ()
                match canChangeImageUrl userId (userId, userType) with
                | true -> yield navbarDropDownItemT theme false [ changeImageUrl ]
                | false -> ()
                yield navbarDropDownItemT theme false [ signOut ] ])
        | _ -> None
    let adminDropDown =
        match headerData.HeaderState with
        | SignedIn(_, authUser) ->
            let showUserAdminPage = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (UserAdmin |> AuthPage |> ShowPage |> AuthInput) ) [ str "User administration" ] ]
            match canAdministerUsers authUser.User.UserType with
            | true -> Some(navbarDropDownT theme (iconSmaller ICON__ADMIN) [ navbarDropDownItemT theme false [ showUserAdminPage ] ])
            | false -> None
        |  _ -> None
    // TODO-NMB: pageTabs?...
    let toggleThemeInteraction = Clickable(fun _ -> dispatch ToggleTheme)
    let toggleThemeTooltip = tooltip (if burgerIsActive then TooltipRight else TooltipLeft) IsPrimary (sprintf "Switch to %s theme" (match theme with | Light -> "dark" | Dark -> "light"))
    navbarT theme IsLight [
        navbarBrand [
            yield navbarItem [ image IMAGE__GIBET Image.Is24x24 ]
            yield navbarItem [ paraT theme TextSize.Is7 IsBlack TextWeight.Bold [ str GIBET ] ]
            yield ofOption serverStatus
            yield! statusItems |> List.map (fun element -> navbarItem [ element ])
            yield navbarBurger (fun _ -> dispatch ToggleNavbarBurger) burgerIsActive ]
        navbarMenuT theme burgerIsActive [
            navbarStart [
                ofOption authUserDropDown
                // TODO-NMB?...yield navbarItem [ tabs theme { tabsDefault with Tabs = pageTabs } ]
                ofOption adminDropDown ]
            navbarEnd [
#if TICK
                navbarItem [ paraT theme TextSize.Is7 IsGreyDarker TextWeight.Normal [ str (DateTimeOffset.UtcNow.LocalDateTime.ToString("HH:mm:ss")) ] ]
#endif
                navbarItem [ buttonT theme (Some IsSmall) IsDark toggleThemeInteraction false false (Some toggleThemeTooltip) [ iconSmall ICON__THEME ] ] ] ] ]
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
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Sign in" ] ] ]
    let onDismiss, isSigningIn, signInInteraction, onEnter, userNameStatus, passwordStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CancelSignIn), (fun _ -> dispatch SignIn)
        match signInModalState.ModalStatus with
        | Some ModalPending -> None, true, Loading, ignore, None, None
        | _ ->
            let userNameError = validateUserName true (UserName signInModalState.UserName) []
            let passwordError = validatePassword true (Password signInModalState.Password)
            let signInInteration, onEnter =
                match userNameError, passwordError with
                | None, None -> Clickable onEnter, onEnter
                | _ -> NotEnabled, ignore
            let userNameStatus =
                match signInModalState.UserNameChanged, userNameError with
                | true, Some error -> Some(IsWarning, ICON__WARNING, helpTWarning theme [ str error ])
                | _ -> None
            let passwordStatus =
                match signInModalState.PasswordChanged, passwordError with
                | true, Some error -> Some(IsWarning, ICON__WARNING, helpTWarning theme [ str error ])
                | _ -> None
            Some onDismiss, false, signInInteration, onEnter, userNameStatus, passwordStatus
    let extra = ifDebug [] [ str " (e.g. " ; bold EXAMPLE_ADMIN_USER_NAME ; str " | " ; bold EXAMPLE_ADMIN_PASSWORD ; str ")" ]
    let body = [
        match signInModalState.AutoSignInError, signInModalState.ForcedSignOutReason, signInModalState.ModalStatus with
        | Some(error, UserName userName), _, _ ->
            yield notificationT theme IsWarning None [
                contentCentred [ paraTSmaller theme [ str "Unable to automatically sign in as " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | None, Some forcedSignOutReason, _ ->
            let because, UserName byUserName = forcedSignOutBecause forcedSignOutReason
            yield notificationT theme IsWarning None [
                contentCentred [ paraT theme TextSize.Is6 IsBlack TextWeight.SemiBold [ str (sprintf "You have been signed out because %s by " because) ; bold byUserName ] ] ]
            yield br
        | None, None, Some(ModalFailed(error, UserName userName)) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to sign in as " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | None, None, _ -> ()
        yield contentCentred [ paraTSmaller theme [ yield str "Please enter your credentials" ; yield! extra ] ]
        yield fieldDefault [
            labelTSmallest theme [ str "User name" ]
            textTDefault theme signInModalState.UserNameKey signInModalState.UserName userNameStatus ICON__USER (not signInModalState.FocusPassword) isSigningIn
                (UserNameChanged >> dispatch) ignore ]
        yield fieldDefault [
            labelTSmallest theme [ str "Password" ]
            textTPassword theme signInModalState.PasswordKey signInModalState.Password passwordStatus signInModalState.FocusPassword isSigningIn (PasswordChanged >> dispatch) onEnter ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink signInInteraction [ str "Sign in" ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangePasswordModal (theme, UserName userName, changePasswordModalState:ChangePasswordModalState) dispatch =
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Change password for " ; bold userName ] ] ]
    let onDismiss, isChangingPassword, changePasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CancelChangePassword), (fun _ -> dispatch ChangePassword)
        match changePasswordModalState.ModalStatus with
        | Some ModalPending -> None, true, Loading, ignore, None, None
        | _ ->
            let newPassword = Password changePasswordModalState.NewPassword
            let newPasswordError = validatePassword false newPassword
            let confirmPasswordError = validateConfirmationPassword newPassword (Password changePasswordModalState.ConfirmPassword)
            let changePasswordInteraction, onEnter =
                match newPasswordError, confirmPasswordError with
                | None, None -> Clickable onEnter, onEnter
                | _ -> NotEnabled, ignore
            let newPasswordStatus =
                match changePasswordModalState.NewPasswordChanged, newPasswordError with
                | true, Some error -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                | true, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The new password is valid" ])
                | _ -> None
            let confirmPasswordStatus =
                match changePasswordModalState.ConfirmPasswordChanged, confirmPasswordError with
                | true, Some error -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                | true, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The confirmation password is valid" ])
                | _ -> None
            let onDismiss = match changePasswordModalState.MustChangePasswordReason with | Some _ -> None | None -> Some onDismiss
            onDismiss, false, changePasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus
    let body = [
        match changePasswordModalState.MustChangePasswordReason with
        | Some mustChangePasswordReason ->
            let because =
                match mustChangePasswordBecause mustChangePasswordReason with
                | because, Some(UserName byUserName) -> [ str (sprintf "You must change your password because %s by " because) ; bold byUserName ]
                | because, None -> [ str (sprintf "You must change your password because %s" because) ]
            yield notificationT theme IsWarning None [ contentCentred [ paraT theme TextSize.Is6 IsBlack TextWeight.SemiBold because ] ]
            yield br
        | None -> ()
        match changePasswordModalState.ModalStatus with
        | Some(ModalFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to change password for " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentred [ paraTSmaller theme [ str "Please enter and confirm your new password" ] ]
        yield fieldDefault [
            labelTSmallest theme [ str "New password" ]
            textTPassword theme changePasswordModalState.NewPasswordKey changePasswordModalState.NewPassword newPasswordStatus true isChangingPassword (NewPasswordChanged >> dispatch)
                onEnter ]
        yield fieldDefault [
            labelTSmallest theme [ str "Confirm new password" ]
            textTPassword theme changePasswordModalState.ConfirmPasswordKey changePasswordModalState.ConfirmPassword confirmPasswordStatus false isChangingPassword
                (ConfirmPasswordChanged >> dispatch) onEnter ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink changePasswordInteraction [ str "Change password" ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangeImageUrlModal (theme, authUser, changeImageUrlModalState:ChangeImageUrlModalState) dispatch =
    let UserName userName, currentImageUrl, imageUrl = authUser.User.UserName, authUser.User.ImageUrl, changeImageUrlModalState.ImageUrl
    let currentlyNone = match currentImageUrl with | Some _ -> false | None -> true
    let action, extra = if currentlyNone then "Choose", String.Empty else "Change (or remove)", " (or blank to remove image)"
    let buttonAction =
        match currentlyNone, String.IsNullOrWhiteSpace imageUrl with
        | true, _ -> "Choose"
        | false, false -> "Change"
        | false, true -> "Remove"
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str (sprintf "%s image for " action) ; bold userName ] ] ]
    let onDismiss, isChangingImageUrl, changeImageUrlInteraction, onEnter, imageUrlStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CancelChangeImageUrl), (fun _ -> dispatch ChangeImageUrl)
        match changeImageUrlModalState.ModalStatus with
        | Some ModalPending -> None, true, Loading, ignore, None
        | _ ->
            let hasChanged = (if String.IsNullOrWhiteSpace imageUrl then None else Some(ImageUrl imageUrl)) <> currentImageUrl
            let changeImageUrlInteraction, onEnter =
                if hasChanged then Clickable onEnter, onEnter
                else NotEnabled, ignore
            let imageUrlStatus =
                match hasChanged, currentlyNone, String.IsNullOrWhiteSpace imageUrl with
                | true, true, _ | true, false, false -> Some(IsInfo, ICON__INFO, helpTInfo theme [ str "Please check the image preview above" ])
                | true, false, true -> Some(IsWarning, ICON__WARNING, helpTWarning theme [ str "The image will be removed for " ; bold userName ])
                | _ -> None
            Some onDismiss, false, changeImageUrlInteraction, onEnter, imageUrlStatus
    let image = if String.IsNullOrWhiteSpace imageUrl then None else Some(fieldGroupedCentred [ image imageUrl Image.Is128x128 ])
    let body = [
        match changeImageUrlModalState.ModalStatus with
        | Some(ModalFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to change image for " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentred [
            yield paraTSmaller theme [ str (sprintf "Please enter the URL for your image%s" extra) ]
            yield paraT theme TextSize.Is7 IsPrimary TextWeight.Normal [ str "The selected image should preferably have a 1:1 aspect ratio" ]
            yield ofOption image ]
        yield fieldDefault [
            textTDefault theme changeImageUrlModalState.ImageUrlKey changeImageUrlModalState.ImageUrl imageUrlStatus ICON__IMAGE true isChangingImageUrl (ImageUrlChanged >> dispatch)
                ignore ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink changeImageUrlInteraction [ str (sprintf "%s image" buttonAction) ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderSigningOutModal theme =
    cardModalT theme None [ contentCentred [ paraT theme TextSize.Is6 IsInfo TextWeight.Normal [ bold "Signing out... " ; iconSmall ICON__SPINNER_PULSE ] ] ]

// #region render
let render state dispatch =
    let renderMessages theme messages ticks = lazyView2 renderMessages (theme, GIBET, messages, ticks) (DismissMessage >> dispatch)
    let divSpinner theme colour = div [] [ containerFluid [ contentCentred [ paraT theme TextSize.Is7 colour TextWeight.Normal [ iconLarger ICON__SPINNER_PULSE ] ] ] ]
    let state, reconnecting =
        match state with
        | InitializingConnection (_, Some reconnectingState) | ReadingPreferences (_, Some reconnectingState) -> reconnectingState, true
        | _ -> state, false
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> pageLoaderT Light IsLink // note: messages (if any) not rendered
    | RegisteringConnection registeringConnectionState ->
        let theme, ticks = registeringConnectionState.AppState.Theme, registeringConnectionState.AppState.Ticks
        let headerData = {
            AppState = registeringConnectionState.AppState
            HeaderState = Registering }
        div [] [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield divVerticalSpace 25
            yield renderMessages theme registeringConnectionState.Messages ticks
            yield divSpinner theme IsLink
            yield divVerticalSpace 15
            yield lazyView renderFooter theme ]
    | AutomaticallySigningIn automaticallySigningInState ->
        let theme, ticks = automaticallySigningInState.AppState.Theme, automaticallySigningInState.AppState.Ticks
        let headerData = {
            AppState = automaticallySigningInState.AppState
            HeaderState = SigningIn(automaticallySigningInState.ConnectionState, fst automaticallySigningInState.LastUser, true) }
        div [] [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield divVerticalSpace 25
            yield renderMessages theme automaticallySigningInState.Messages ticks
            yield divSpinner theme IsInfo
            yield divVerticalSpace 15
            yield lazyView renderFooter theme ]
    | Unauth unauthState ->
        let theme, ticks = unauthState.AppState.Theme, unauthState.AppState.Ticks
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
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield divVerticalSpace 25
            yield renderMessages theme unauthState.Messages ticks
            match unauthState.CurrentPage with
            | About -> yield About.Render.render theme
            yield divVerticalSpace 15
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme
            else
                match unauthState.SignInModalState with
                | Some signInModalState -> yield lazyView2 renderSignInModal (theme, signInModalState) (SignInModalInput >> UnauthInput >> dispatch)
                | None -> () ]
    // #region Auth
    | Auth authState ->
        let theme, ticks = authState.AppState.Theme, authState.AppState.Ticks
        let authUser, usersData = authState.AuthUser, authState.UsersData
        let headerData = {
            AppState = authState.AppState
            HeaderState =
                if authState.SigningOut then SigningOut authState.ConnectionState
                else SignedIn(authState.ConnectionState, authState.AuthUser) }
        div [] [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield divVerticalSpace 25
            yield renderMessages theme authState.Messages ticks
            match authState.CurrentPage with
            | UnauthPage About -> yield About.Render.render theme
            | AuthPage Chat -> yield Chat.Render.render theme authUser usersData authState.ChatState ticks (ChatInput >> AuthInput >> dispatch)
            | AuthPage UserAdmin ->
                let userType = authUser.User.UserType
                if canAdministerUsers userType then
                    match authState.UserAdminState with
                    | Some userAdminState -> yield UserAdmin.Render.render theme authUser usersData userAdminState (UserAdminInput >> AuthInput >> dispatch)
                    | None -> yield renderDangerMessage theme (ifDebug "CurrentPage is AuthPage UserAdmin but UserAdminState is None" UNEXPECTED_ERROR)
                else yield renderDangerMessage theme (ifDebug (sprintf "CurrentPage is AuthPage UserAdmin but canAdministerUsers returned false for %A" userType) UNEXPECTED_ERROR)
            yield divVerticalSpace 15
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme
            else if authState.SigningOut then yield lazyView renderSigningOutModal theme
            else
                match authState.ChangePasswordModalState, authState.ChangeImageUrlModalState with
                | Some changePasswordModalState, _ ->
                    yield lazyView2 renderChangePasswordModal (theme, authState.AuthUser.User.UserName, changePasswordModalState) (ChangePasswordModalInput >> AuthInput >> dispatch)
                | None, Some changeImageUrlModalState ->
                    yield lazyView2 renderChangeImageUrlModal (theme, authState.AuthUser, changeImageUrlModalState) (ChangeImageUrlModalInput >> AuthInput >> dispatch)
                | None, None -> () ]
    // #endregion
// #endregion
