module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.LazyViewOrHMR
open Aornota.Gibet.Ui.Common.Message
open Aornota.Gibet.Ui.Common.Render
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

type private HeaderPage =
    | Tab of Fable.React.ReactElement
    | UserAdminDropdown of bool

type private HeaderData = {
    AppState : AppState
    HeaderState : HeaderState
    PageData : HeaderPage list }

let private pageData dispatch state =
    let tab text isActive input = tab isActive [ linkInternal (fun _ -> dispatch input) [ str text ] ]
    match state with
    | InitializingConnection _ | ReadingPreferences _ | RegisteringConnection _ | AutomaticallySigningIn _ -> []
    | Unauth unauthState ->
        [
            Tab(tab About.Render.PAGE_TITLE (unauthState.CurrentPage = About) (UnauthInput(ShowUnauthPage About)))
        ]
    | Auth authState ->
        [
            Tab(tab About.Render.PAGE_TITLE (authState.CurrentPage = UnauthPage About) (AuthInput(ShowPage(UnauthPage About))))
            Tab(authState.ChatState |> Chat.Render.renderTab (authState.CurrentPage = AuthPage Chat) (fun _ -> dispatch (AuthInput(ShowPage(AuthPage Chat)))))
            UserAdminDropdown(authState.CurrentPage = AuthPage UserAdmin)
        ]

// #region renderHeader
let private renderHeader (headerData, _:int<tick>) dispatch =
    let theme, burgerIsActive = headerData.AppState.Theme, headerData.AppState.NavbarBurgerIsActive
    let spinner = iconSmaller ICON__SPINNER_PULSE
    // #region serverStatus
    let serverStatus =
        match headerData.HeaderState with
        | Registering -> Some(navbarItem [ paraTSmallest theme IsGreyDarker [ str "Registering connection... " ; spinner ] ])
        | NotSignedIn connectionState | SigningIn(connectionState, _, _) | SignInError(connectionState, _, _) | SignedIn(connectionState, _) | SigningOut connectionState ->
            let timestampText =
#if TICK
                ago connectionState.ServerStarted.LocalDateTime
#else
                sprintf "on %s" (connectionState.ServerStarted.LocalDateTime |> dateAndTimeText)
#endif
            Some(navbarItem [ paraTSmallest theme IsGreyDarker [ str (sprintf "Server started %s" timestampText) ] ])
    // #endregion
    let statusItems =
        let separator = paraTSmallest theme IsGreyDarker [ str "|" ]
        let signIn = paraSmallest [ linkInternal (fun _ -> dispatch (UnauthInput ShowSignInModal) ) [ str "Sign in" ] ]
        match headerData.HeaderState with
        | Registering -> []
        | NotSignedIn _ -> [ separator ; paraTSmallest theme IsPrimary [ str "Not signed in" ] ; signIn ]
        | SigningIn(_, UserName userName, auto) ->
            let text = if auto then "Automatically signing in as " else "Signing in as "
            [ separator ; paraTSmallest theme IsInfo [ str text ; strong userName ; str "... " ; spinner ] ]
        | SignInError(_, UserName userName, auto) ->
            let text = if auto then "Unable to automatically sign in as " else "Unable to sign in as "
            [ separator ; paraTSmallest theme (if auto then IsWarning else IsDanger) [ str text ; strong userName ] ]
        | SignedIn(_, authUser) ->
            let (UserName userName) = authUser.User.UserName
            [ separator ; paraTSmallest theme IsSuccess [ str "Signed in as " ; strong userName ] ]
        | SigningOut _ -> [ separator ; paraTSmallest theme IsInfo [ str "Signing out... " ; spinner ] ]
    let authUserDropDown =
        match headerData.HeaderState with
        | SignedIn(_, authUser) ->
            let image, currentlyNone =
                match authUser.User.ImageUrl with
                | Some(ImageUrl imageUrl) -> image imageUrl Image.Is24x24, false
                | None -> iconSmaller ICON__USER, true
            let changePassword = paraSmallest [ linkInternal (fun _ -> dispatch (AuthInput ShowChangePasswordModal)) [ str "Change password" ] ]
            let changeImageUrl = paraSmallest [ linkInternal (fun _ -> dispatch (AuthInput ShowChangeImageUrlModal)) [
                    str (sprintf "%s image" (if currentlyNone then "Choose" else "Change (or remove)"))] ]
            let signOut = paraSmallest [ linkInternal (fun _ -> dispatch (SignOut |> AuthInput) ) [ str "Sign out" ] ]
            Some(navbarDropDownT theme image [
                let userId, userType = authUser.User.UserId, authUser.User.UserType
                let items = [
                    match canChangePassword userId (userId, userType) with
                    | true -> yield navbarDropDownItemT theme false [ changePassword ]
                    | false -> ()
                    match canChangeImageUrl userId (userId, userType) with
                    | true -> yield navbarDropDownItemT theme false [ changeImageUrl ]
                    | false -> () ]
                yield! items
                match items with | _ :: _ -> yield navbarDividerT theme true | [] -> ()
                yield navbarDropDownItemT theme false [ signOut ] ])
        | _ -> None
    let pageTabs = headerData.PageData |> List.choose (fun page -> match page with | Tab element -> Some element | _ -> None)
    let adminDropDown =
        match headerData.HeaderState with
        | SignedIn(_, authUser) ->
            let showUserAdminPage = paraSmallest [ linkInternal (fun _ -> dispatch (AuthInput(ShowPage(AuthPage UserAdmin))) ) [ str "User administration" ] ]
            match canAdministerUsers authUser.User.UserType with
            | true ->
                let userAdminIsActive = headerData.PageData |> List.exists (fun page -> match page with | UserAdminDropdown true -> true | _ -> false)
                Some(navbarDropDownT theme (iconSmaller ICON__ADMIN) [ navbarDropDownItemT theme userAdminIsActive [ showUserAdminPage ] ])
            | false -> None
        |  _ -> None
    let toggleThemeInteraction = Clickable(fun _ -> dispatch ToggleTheme)
    let toggleThemeTooltip = tooltip (if burgerIsActive then TooltipRight else TooltipLeft) IsInfo (sprintf "Switch to %s theme" (match theme with | Light -> "dark" | Dark -> "light"))
    navbarT theme IsLight [
        navbarBrand [
            yield navbarItem [ image "gibet-24x24.png" Image.Is24x24 ]
            yield navbarItem [ paraTSmallest theme IsBlackTer [ strong GIBET ] ]
            yield ofOption serverStatus
            yield! statusItems |> List.map (fun element -> navbarItem [ element ])
            yield navbarBurger (fun _ -> dispatch ToggleNavbarBurger) burgerIsActive ]
        navbarMenuT theme burgerIsActive [
            navbarStart [
                ofOption authUserDropDown
                navbarItem [ tabsTSmall theme pageTabs ]
                ofOption adminDropDown ]
            navbarEnd [
#if TICK
                navbarItem [ paraTSmallest theme IsGreyDarker [ str (DateTimeOffset.UtcNow.LocalDateTime.ToString("HH:mm:ss")) ] ]
#endif
                navbarItem [ buttonTSmallTooltip theme IsDark toggleThemeInteraction toggleThemeTooltip [ iconSmall ICON__THEME ] ] ] ] ]
// #endregion

let private renderFooter theme =
    let credits = [
        linkNewWindow "https://github.com/aornota/gibet/" [ str "Written" ]
        str " in "
        linkNewWindow "http://fsharp.org/" [ str "F#"]
        str " using "
        linkNewWindow "http://fable.io/" [ str "Fable"]
        str ", "
        linkNewWindow "https://elmish.github.io/" [ str "Elmish"]
        str ", "
        linkNewWindow "https://github.com/Fulma/Fulma/" [ str "Fulma"]
        str " / "
        linkNewWindow "https://bulma.io/" [ str "Bulma"]
        str ", "
        linkNewWindow "https://github.com/Zaid-Ajaj/Fable.Remoting/" [ str "Fable.Remoting"]
        str ", "
        linkNewWindow "https://github.com/Nhowka/Elmish.Bridge/" [ str "Elmish.Bridge"]
        str ", "
        linkNewWindow "https://github.com/giraffe-fsharp/Giraffe/" [ str "Giraffe"]
        str " and "
        linkNewWindow "https://docs.microsoft.com/en-us/aspnet/core/" [ str "ASP.NET Core"]
        str ". Developed in "
        linkNewWindow "https://code.visualstudio.com/" [ str "Visual Studio Code"]
        str " using "
        linkNewWindow "http://ionide.io/docs/" [ str "Ionide-fsharp"]
        str ". Best viewed with "
        linkNewWindow "https://www.google.com/chrome/" [ str "Chrome"]
        str ". Not especially mobile-friendly." ]
    footerT theme true [ contentCentredSmallest credits ]

let private renderReconnectingModal theme =
    cardModalT theme None [ contentCentredSmaller [
        notificationT theme IsDanger None [ strong "The connection to the server has been lost" ]
        br
        paraTSmaller theme IsInfo [ strong "Attempting to reconnect... " ; iconSmaller ICON__SPINNER_PULSE ] ] ]

let private renderSignInModal (theme, signInModalState:SignInModalState) dispatch =
    let title = [ contentCentred None [ paraSmall [ str "Sign in" ] ] ]
    let onDismiss, signingIn, signInInteraction, onEnter, userNameStatus, passwordStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CloseSignInModal), (fun _ -> dispatch SignIn)
        match signInModalState.SignInApiStatus with
        | Some ApiPending -> None, true, Loading, ignore, None, None
        | _ ->
            let userName = UserName signInModalState.UserName
            let userNameError = validateUserName true (UserName signInModalState.UserName) []
            let passwordError = validatePassword true (Password signInModalState.Password) userName
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
    let extra = ifDebug [] [ str " (e.g. " ; strong EXAMPLE_USER_NAME__AE ; str " | " ; strongEm EXAMPLE_PASSWORD__AE ; str ")" ]
    let keepMeSignedIn, onChange = signInModalState.KeepMeSignedIn, (fun _ -> dispatch KeepMeSignedInChanged)
    let body = [
        match signInModalState.AutoSignInError, signInModalState.ForcedSignOutReason, signInModalState.SignInApiStatus with
        | Some(error, UserName userName), _, _ ->
            yield notificationT theme IsWarning None [
                contentCentredSmaller [ str "Unable to automatically sign in as " ; strong userName ]
                contentLeftSmallest [ str error ] ]
            yield br
        | None, Some forcedSignOutReason, _ ->
            let because =
                match forcedSignOutBecause forcedSignOutReason with
                | because, Some(UserName byUserName) -> [ str (sprintf "You have been signed out because %s by " because) ; strong byUserName ]
                | because, None -> [ str (sprintf "You have been signed out because %s" because) ]
            yield notificationT theme IsWarning None [ contentCentredSmaller because ]
            yield br
        | None, None, Some(ApiFailed(error, UserName userName)) ->
            yield notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to sign in as " ; strong userName ]
                contentLeftSmallest [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentredSmaller [ yield str "Please enter your credentials" ; yield! extra ]
        yield fieldDefault [
            labelSmallest [ str "User name" ]
            textTDefault theme signInModalState.UserNameKey signInModalState.UserName userNameStatus ICON__USER (not signInModalState.FocusPassword) signingIn
                (UserNameChanged >> dispatch) onEnter ]
        yield fieldDefault [
            labelSmallest [ str "Password" ]
            textTPassword theme signInModalState.PasswordKey signInModalState.Password passwordStatus signInModalState.FocusPassword signingIn (PasswordChanged >> dispatch) onEnter ]
        yield fieldGroupedCentred [
            checkTSmall theme (if keepMeSignedIn then IsSuccess else IsLink) keepMeSignedIn signInModalState.KeepMeSignedInKey keepMeSignedIn "Keep me signed in" false onChange ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink signInInteraction [ str "Sign in" ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangePasswordModal (theme, UserName userName, changePasswordModalState:ChangePasswordModalState) dispatch =
    let title = [ contentCentred None [ paraSmall [ str "Change password for " ; strong userName ] ] ]
    let onDismiss, changingPassword, changePasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CloseChangePasswordModal), (fun _ -> dispatch ChangePassword)
        match changePasswordModalState.ChangePasswordApiStatus with
        | Some ApiPending -> None, true, Loading, ignore, None, None
        | _ ->
            let newPassword, confirmPassword = Password changePasswordModalState.NewPassword, Password changePasswordModalState.ConfirmPassword
            let newPasswordError = validatePassword false newPassword (UserName userName)
            let confirmPasswordError = validateConfirmPassword false confirmPassword newPassword
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
                match changePasswordModalState.ConfirmPasswordChanged, confirmPasswordError, newPasswordError with
                | true, Some error, _ -> Some(IsDanger, ICON__DANGER, helpTDanger theme [ str error ])
                | true, None, None -> Some(IsSuccess, ICON__SUCCESS, helpTSuccess theme [ str "The confirmation password is valid" ])
                | _ -> None
            let onDismiss = match changePasswordModalState.MustChangePasswordReason with | Some _ -> None | None -> Some onDismiss
            onDismiss, false, changePasswordInteraction, onEnter, newPasswordStatus, confirmPasswordStatus
    let body = [
        match changePasswordModalState.MustChangePasswordReason with
        | Some mustChangePasswordReason ->
            let because =
                match mustChangePasswordBecause mustChangePasswordReason with
                | because, Some(UserName byUserName) -> [ str (sprintf "You must change your password because %s by " because) ; strong byUserName ]
                | because, None -> [ str (sprintf "You must change your password because %s" because) ]
            yield notificationT theme IsWarning None [ contentCentredSmaller because ]
            yield br
        | None -> ()
        match changePasswordModalState.ChangePasswordApiStatus with
        | Some(ApiFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to change password for " ; strong userName ]
                contentLeftSmallest [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentredSmaller [ str "Please enter and confirm your new password" ]
        yield fieldDefault [
            labelSmallest [ str "New password" ]
            textTPassword theme changePasswordModalState.NewPasswordKey changePasswordModalState.NewPassword newPasswordStatus true changingPassword (NewPasswordChanged >> dispatch)
                onEnter ]
        yield fieldDefault [
            labelSmallest [ str "Confirm password" ]
            textTPassword theme changePasswordModalState.ConfirmPasswordKey changePasswordModalState.ConfirmPassword confirmPasswordStatus false changingPassword
                (ConfirmPasswordChanged >> dispatch) onEnter ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink changePasswordInteraction [ str "Change password" ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangeImageUrlModal (theme, authUser, changeImageUrlModalState:ChangeImageUrlModalState) dispatch =
    let UserName userName, currentImageUrl, imageUrl = authUser.User.UserName, authUser.User.ImageUrl, changeImageUrlModalState.ImageUrl
    let currentlyNone = match currentImageUrl with | Some _ -> false | None -> true
    let action, extra = if currentlyNone then "Choose", String.Empty else "Change (or remove)", " (or blank to remove image)"
    let buttonAction =
        match currentlyNone, String.IsNullOrWhiteSpace(imageUrl) with
        | true, _ -> "Choose"
        | false, false -> "Change"
        | false, true -> "Remove"
    let title = [ contentCentred None [ paraSmall [ str (sprintf "%s image for " action) ; strong userName ] ] ]
    let onDismiss, changingImageUrl, changeImageUrlInteraction, onEnter, imageUrlStatus =
        let onDismiss, onEnter = (fun _ -> dispatch CloseChangeImageUrlModal), (fun _ -> dispatch ChangeImageUrl)
        match changeImageUrlModalState.ChangeImageUrlApiStatus with
        | Some ApiPending -> None, true, Loading, ignore, None
        | _ ->
            let hasChanged = (if String.IsNullOrWhiteSpace(imageUrl) then None else Some(ImageUrl imageUrl)) <> currentImageUrl
            let changeImageUrlInteraction, onEnter =
                if hasChanged then Clickable onEnter, onEnter
                else NotEnabled, ignore
            let imageUrlStatus =
                match hasChanged, currentlyNone, String.IsNullOrWhiteSpace(imageUrl) with
                | true, true, _ | true, false, false -> Some(IsInfo, ICON__INFO, helpTInfo theme [ str "Please check the image preview above" ])
                | true, false, true -> Some(IsWarning, ICON__WARNING, helpTWarning theme [ str "The image will be removed for " ; strong userName ])
                | _ -> None
            Some onDismiss, false, changeImageUrlInteraction, onEnter, imageUrlStatus
    let image = if String.IsNullOrWhiteSpace(imageUrl) then None else Some(fieldGroupedCentred [ image imageUrl Image.Is128x128 ])
    let body = [
        match changeImageUrlModalState.ChangeImageUrlApiStatus with
        | Some(ApiFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentredSmaller [ str "Unable to change image for " ; strong userName ]
                contentLeftSmallest [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentred None [
            paraSmaller [ str (sprintf "Please enter the URL for your image%s" extra) ]
            paraTSmallest theme IsPrimary [ str "The selected image should preferably have a 1:1 aspect ratio" ]
            ofOption image ]
        yield fieldDefault [
            labelSmallest [ str "Image URL" ]
            textTDefault theme changeImageUrlModalState.ImageUrlKey changeImageUrlModalState.ImageUrl imageUrlStatus ICON__IMAGE true changingImageUrl (ImageUrlChanged >> dispatch)
                ignore ]
        yield fieldGroupedCentred [ buttonTSmall theme IsLink changeImageUrlInteraction [ str (sprintf "%s image" buttonAction) ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderSigningOutModal theme =
    cardModalT theme None [ contentTCentred theme (Some smaller) (Some IsInfo) [ strong "Signing out... " ; iconSmall ICON__SPINNER_PULSE ] ]

let render state dispatch =
    let lazyRenderMessages theme messages ticks = [
        divVerticalSpace 25
        lazyView2 renderMessages (theme, GIBET, messages, ticks) (DismissMessage >> dispatch) ]
    let spinner theme colour = contentTCentred theme None (Some colour) [ divVerticalSpace 15 ; iconLarge ICON__SPINNER_PULSE ]
    let state, reconnecting =
        match state with
        | InitializingConnection (_, Some reconnectingState) | ReadingPreferences (_, Some reconnectingState) -> reconnectingState, true
        | _ -> state, false
    let pageData = state |> pageData dispatch
    match state with
    | InitializingConnection _ | ReadingPreferences _ -> pageLoaderT Light IsLink // note: Messages (if any) not rendered
    | RegisteringConnection registeringConnectionState ->
        let theme, ticks = registeringConnectionState.AppState.Theme, registeringConnectionState.AppState.Ticks
        let headerData = {
            AppState = registeringConnectionState.AppState
            HeaderState = Registering
            PageData = pageData }
        divDefault [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield! lazyRenderMessages theme registeringConnectionState.Messages ticks
            yield spinner theme IsLink
            yield lazyView renderFooter theme ]
    | AutomaticallySigningIn automaticallySigningInState ->
        let theme, ticks = automaticallySigningInState.AppState.Theme, automaticallySigningInState.AppState.Ticks
        let headerData = {
            AppState = automaticallySigningInState.AppState
            HeaderState = SigningIn(automaticallySigningInState.ConnectionState, fst automaticallySigningInState.LastUser, true)
            PageData = pageData }
        divDefault [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield! lazyRenderMessages theme automaticallySigningInState.Messages ticks
            yield spinner theme IsInfo
            yield lazyView renderFooter theme ]
    | Unauth unauthState ->
        let theme, ticks = unauthState.AppState.Theme, unauthState.AppState.Ticks
        let headerData = {
            AppState = unauthState.AppState
            HeaderState =
                match unauthState.SignInModalState with
                | Some signInModalState ->
                    match signInModalState.AutoSignInError, signInModalState.SignInApiStatus with
                    | Some(_, userName), _ -> SignInError(unauthState.ConnectionState, userName, true)
                    | None, Some ApiPending -> SigningIn(unauthState.ConnectionState, UserName signInModalState.UserName, false)
                    | None, Some(ApiFailed _) -> SignInError(unauthState.ConnectionState, UserName signInModalState.UserName, false)
                    | _ -> NotSignedIn unauthState.ConnectionState
                | None -> NotSignedIn unauthState.ConnectionState
            PageData = pageData }
        divDefault [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield! lazyRenderMessages theme unauthState.Messages ticks
            match unauthState.CurrentPage with
            | About -> yield About.Render.render theme
            yield lazyView renderFooter theme
            // Note: Render Modals last so will appear "on top" of any CurrentPage ones.
            if reconnecting then yield lazyView renderReconnectingModal theme
            else
                match unauthState.SignInModalState with
                | Some signInModalState -> yield lazyView2 renderSignInModal (theme, signInModalState) (SignInModalInput >> UnauthInput >> dispatch)
                | None -> () ]
    | Auth authState ->
        let theme, ticks = authState.AppState.Theme, authState.AppState.Ticks
        let authUser, usersData = authState.AuthUser, authState.UsersData
        let headerData = {
            AppState = authState.AppState
            HeaderState =
                if authState.SigningOut then SigningOut authState.ConnectionState
                else SignedIn(authState.ConnectionState, authState.AuthUser)
            PageData = pageData }
        let hasModal =
            match reconnecting, authState.SigningOut, authState.ChangePasswordModalState, authState.ChangeImageUrlModalState with
            | true, _, _, _ -> true
            | _, true, _, _ -> true
            | _, _, Some _, _ -> true
            | _, _, _, Some _ -> true
            | _ -> false
        divDefault [
            yield lazyView2 renderHeader (headerData, ticks) dispatch
            yield! lazyRenderMessages theme authState.Messages ticks
            match authState.CurrentPage with
            | UnauthPage About -> yield About.Render.render theme
            | AuthPage Chat -> yield Chat.Render.render theme authUser usersData authState.ChatState hasModal ticks (ChatInput >> AuthInput >> dispatch)
            | AuthPage UserAdmin ->
                let userType = authUser.User.UserType
                if canAdministerUsers userType then
                    match authState.UserAdminState with
                    | Some userAdminState -> yield UserAdmin.Render.render theme authUser usersData userAdminState ticks (UserAdminInput >> AuthInput >> dispatch)
                    | None -> yield renderDangerMessage theme (ifDebug "CurrentPage is AuthPage UserAdmin but UserAdminState is None" UNEXPECTED_ERROR)
                else yield renderDangerMessage theme (ifDebug (sprintf "CurrentPage is AuthPage UserAdmin but canAdministerUsers returned false for %A" userType) UNEXPECTED_ERROR)
            yield lazyView renderFooter theme
            // Note: Render Modals last so will appear "on top" of any CurrentPage ones.
            if reconnecting then yield lazyView renderReconnectingModal theme
            else if authState.SigningOut then yield lazyView renderSigningOutModal theme
            else
                match authState.ChangePasswordModalState, authState.ChangeImageUrlModalState with
                | Some changePasswordModalState, _ ->
                    yield lazyView2 renderChangePasswordModal (theme, authState.AuthUser.User.UserName, changePasswordModalState) (ChangePasswordModalInput >> AuthInput >> dispatch)
                | None, Some changeImageUrlModalState ->
                    yield lazyView2 renderChangeImageUrlModal (theme, authState.AuthUser, changeImageUrlModalState) (ChangeImageUrlModalInput >> AuthInput >> dispatch)
                | _ -> () ]
