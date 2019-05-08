module Aornota.Gibet.Ui.Program.Render

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Markdown
open Aornota.Gibet.Ui.Common.Icon
open Aornota.Gibet.Ui.Common.Render
open Aornota.Gibet.Ui.Common.Render.Theme
open Aornota.Gibet.Ui.Common.Render.Theme.Markdown
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

let [<Literal>] private IMAGE__GIBET = "gibet-24x24.png"

// #region TEMP-NMB: READ_ME...
let [<Literal>] private READ_ME = """# ![gibet](gibet-24x24.png) | gibet (β)

An opinionated (i.e. decidedly eccentric) "scaffold" for [F#](http://fsharp.org/) web development using [Fable](http://fable.io/), [Elmish](https://elmish.github.io/),
[Fulma](https://github.com/Fulma/Fulma/) / [Bulma](https://bulma.io/), [Fable.Remoting](https://github.com/Zaid-Ajaj/Fable.Remoting/),
[Elmish.Bridge](https://github.com/Nhowka/Elmish.Bridge/), [Giraffe](https://github.com/giraffe-fsharp/Giraffe/) and [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/).
Comes with just enough rope for developers to hang themselves with...

And yes, I know that a _gibet_ (gibbet) is not the same as a scaffold - but I love Ravel's _Gaspard de la nuit_, especially _[Le Gibet](https://www.youtube.com/watch?v=vRQF490yyAY/)_.

### Prerequisites

- [Microsoft .NET Core 2.2 SDK](https://dotnet.microsoft.com/download/dotnet-core/2.2/): I'm currently using 2.2.202 (x64)
- [FAKE 5](https://fake.build/): _dotnet tool install --global fake-cli_; I'm currently using 5.12.6
- [Paket](https://fsprojects.github.io/Paket/): _dotnet tool install --global paket_; I'm currently using 5.200.4
- [Yarn](https://yarnpkg.com/lang/en/docs/install/): I'm currently using 1.15.2
- [Node.js (LTS)](https://nodejs.org/en/download/): I'm currently using 10.15.0

#### Also recommended

- [Microsoft Visual Studio Code](https://code.visualstudio.com/download/) with the following extensions:
    - [Microsoft C#](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
    - [Ionide-fsharp](https://marketplace.visualstudio.com/items?itemName=ionide.ionide-fsharp)
    - [Microsoft Debugger for Chrome](https://marketplace.visualstudio.com/items?itemName=msjsdiag.debugger-for-chrome)
    - [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=editorconfig.editorconfig)
    - [Rainbow Brackets](https://marketplace.visualstudio.com/items?itemName=2gua.rainbow-brackets)
- [Google Chrome](https://www.google.com/chrome/) with the following extensions:
    - [React Developer Tools](https://chrome.google.com/webstore/detail/react-developer-tools/fmkadmapgofadopljbjfkapdkoienihi/)
    - [Redux DevTools](https://chrome.google.com/webstore/detail/redux-devtools/lmhkpmbekcpmknklioeibfkpmmfibljd/)
- ([Microsoft .NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472/): this appeared to resolve problems with Intellisense in _build.fsx_)

### History

- Installed SAFE templates for .NET Core: _dotnet new -i "SAFE.Template::*"_
- Created from template: _dotnet new SAFE --server giraffe --layout fulma-basic --communication remoting --pattern default --deploy azure --js-deps yarn_

### Running / building / deploying

- Run/watch for development (debug): _fake build --target run_ (or _fake build -t run_)
- Build for production (release): _fake build --target build_ (or _fake build -t build_)
- Deploy to Azure (release): _fake build --target deploy-azure_ (or _fake build -t deploy-azure_); see [Registering with Azure](https://safe-stack.github.io/docs/template-azure-registration/) and [Deploy to App Service](https://safe-stack.github.io/docs/template-appservice/)
- Run the dev-console (debug): _fake build --target run-dev-console_ (or _fake build -t run-dev-console_)
- Help (lists key targets): _fake build --target help_ (or just _fake build_)

### Unit tests

There are no unit tests yet ;(

However, the repository and web API services have been designed both to work with ASP.NET Core dependency injection - and to facilitate unit testing.

See [here](https://github.com/aornota/gibet/blob/master/src/dev-console/test-user-repo-and-api.fs) for an example of "testing" IUserRepo (e.g. InMemoryUserRepoAgent) and UserApi
(e.g. UserApiAgent) from a console project.

## To do

- [ ] extend functionality, e.g. User administation page | Chat page? | &c.
- [ ] unit tests? AspNetCore.TestHost?
- [ ] additional documentation, e.g. [(currently non-existent) gh-pages branch](https://aornota.github.io/gibet/)?"""
// #endregion

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
            let image, currentlyNone =
                match authUser.User.ImageUrl with
                | Some(ImageUrl imageUrl) -> image imageUrl Image.Is24x24, false
                | None -> iconSmaller ICON__USER, true
            let changePassword = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangePasswordModal |> AuthInput |> AppInput)) [ str "Change password" ] ]
            let changeImageUrl = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (ShowChangeImageUrlModal |> AuthInput |> AppInput)) [
                    str (sprintf "%s image" (if currentlyNone then "Choose" else "Change"))] ]
            let signOut = paraTSmallest theme [ linkTInternal theme (fun _ -> dispatch (SignOut |> AuthInput |> AppInput) ) [ str "Sign out" ] ]
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
            yield navbarItem [ image IMAGE__GIBET Image.Is24x24 ]
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
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Sign in" ] ] ]
    let onDismiss, isSigningIn, signInInteraction, onEnter, userNameError, passwordError =
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
            let userNameError = if signInModalState.UserNameChanged then userNameError else None
            let passwordError = if signInModalState.PasswordChanged then passwordError else None
            Some onDismiss, false, signInInteration, onEnter, userNameError, passwordError
    let body = [
        match signInModalState.AutoSignInError, signInModalState.ForcedSignOutReason, signInModalState.ModalStatus with
        | Some(error, UserName userName), _, _ ->
            yield notificationT theme IsWarning None [
                contentCentred [ paraTSmaller theme [ str "Unable to automatically sign in as " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | None, Some forcedSignOutReason, _ ->
            yield notificationT theme IsWarning None [
                contentCentred [ paraT theme TextSize.Is6 IsBlack TextWeight.SemiBold [
                    str (sprintf "You have been signed out because %s" (forcedSignOutBecause forcedSignOutReason)) ] ] ]
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

let private renderChangePasswordModal (theme, UserName userName, changePasswordModalState:ChangePasswordModalState) dispatch =
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str "Change password for " ; bold userName ] ] ]
    let onDismiss, isChangingPassword, changePasswordInteraction, onEnter, newPasswordError, confirmPasswordError =
        let onDismiss, onEnter = (fun _ -> dispatch CancelChangePassword), (fun _ -> dispatch ChangePassword)
        match changePasswordModalState.ModalStatus with
        | Some ModalPending -> None, true, Loading, ignore, None, None
        | _ ->
            let newPassword = Password changePasswordModalState.NewPassword
            let newPasswordError = validatePassword false newPassword
            let confirmPasswordError = validateConfirmationPassword newPassword (Password changePasswordModalState.ConfirmPassword)
            let changePasswordInteraction, onEnter =
                match newPasswordError, newPasswordError with
                | None, None -> Clickable onEnter, onEnter
                | _ -> NotEnabled, ignore
            let newPasswordError = if changePasswordModalState.NewPasswordChanged then newPasswordError else None
            let confirmPasswordError = if changePasswordModalState.ConfirmPasswordChanged then confirmPasswordError else None
            let onDismiss = match changePasswordModalState.MustChangePasswordReason with | Some _ -> None | None -> Some onDismiss
            onDismiss, false, changePasswordInteraction, onEnter, newPasswordError, confirmPasswordError
    let body = [
        match changePasswordModalState.MustChangePasswordReason with
        | Some mustChangePasswordReason ->
            yield notificationT theme IsWarning None [
                contentCentred [ paraT theme TextSize.Is6 IsBlack TextWeight.SemiBold [
                    str (sprintf "You must change your password because %s" (mustChangePasswordBecause mustChangePasswordReason)) ] ] ]
            yield br
        | None -> ()
        match changePasswordModalState.ModalStatus with
        | Some(ModalFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to change password for " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentred [
            paraTSmaller theme [ str "Please enter your new password (twice)" ]
            fieldGroupedCentred [
                textBoxT theme changePasswordModalState.NewPasswordKey changePasswordModalState.NewPassword (Some ICON__PASSWORD) true newPasswordError [] true isChangingPassword
                    (NewPasswordChanged >> dispatch) ignore ]
            fieldGroupedCentred [
                textBoxT theme changePasswordModalState.ConfirmPasswordKey changePasswordModalState.ConfirmPassword (Some ICON__PASSWORD) true confirmPasswordError [] false isChangingPassword
                    (ConfirmPasswordChanged >> dispatch) onEnter ]
            fieldGroupedCentred [
                paraTSmallest theme [ buttonT theme (Some IsSmall) IsLink changePasswordInteraction false false None [ str "Change password" ] ] ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

let private renderChangeImageUrlModal (theme, UserName userName, changeImageUrlModalState:ChangeImageUrlModalState) dispatch =
    let currentlyNone = match changeImageUrlModalState.CurrentImageUrl with | Some _ -> false | None -> true
    let imageUrl = changeImageUrlModalState.ImageUrl
    let chooseOrChange = if currentlyNone then "Choose" else "Change"
    let title = [ contentCentred [ paraT theme TextSize.Is5 IsBlack TextWeight.SemiBold [ str (sprintf "%s image for " chooseOrChange) ; bold userName ] ] ]
    let hasChanged, onDismiss, isChangingImageUrl, changeImageUrlInteraction, onEnter =
        let onDismiss, onEnter = (fun _ -> dispatch CancelChangeImageUrl), (fun _ -> dispatch ChangeImageUrl)
        match changeImageUrlModalState.ModalStatus with
        | Some ModalPending -> false, None, true, Loading, ignore
        | _ ->
            let imageUrl = if String.IsNullOrWhiteSpace imageUrl then None else Some(ImageUrl imageUrl)
            if imageUrl <> changeImageUrlModalState.CurrentImageUrl then true, Some onDismiss, false, Clickable onEnter, onEnter
            else false, Some onDismiss, false, NotEnabled, ignore
    let info =
        match hasChanged, currentlyNone, String.IsNullOrWhiteSpace imageUrl with
        | true, true, _ | true, false, false -> [ str "Please check the image preview above" ]
        | true, false, true -> [ str "The image will be removed for " ; bold userName ]
        | _ -> []
    let image = if String.IsNullOrWhiteSpace imageUrl then iconSmall ICON__USER else image imageUrl Image.Is128x128
    let body = [
        match changeImageUrlModalState.ModalStatus with
        | Some(ModalFailed error) ->
            yield notificationT theme IsDanger None [
                contentCentred [ paraTSmaller theme [ str "Unable to change image for " ; bold userName ] ]
                paraTSmallest theme [ str error ] ]
            yield br
        | _ -> ()
        yield contentCentred [
            paraTSmaller theme [ str "Please enter the URL for your (preferably square) image" ]
            fieldGroupedCentred [ image ]
            fieldExpanded [
                textBoxT theme changeImageUrlModalState.ImageUrlKey changeImageUrlModalState.ImageUrl (Some ICON__IMAGE) false None info true isChangingImageUrl
                    (ImageUrlChanged >> dispatch) onEnter ]
            fieldGroupedCentred [
                paraTSmallest theme [ buttonT theme (Some IsSmall) IsLink changeImageUrlInteraction false false None [ str (sprintf "%s image" chooseOrChange) ] ] ] ] ]
    cardModalT theme (Some(title, onDismiss)) body

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
            yield div [] [ containerFluid [ contentFromMarkdownLeft theme (Markdown READ_ME) ] ] // TEMP-NMB...
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
            yield div [] [ containerFluid [ contentFromMarkdownLeft theme (Markdown READ_ME) ] ] // TEMP-NMB...
            yield divVerticalSpace 15
            yield lazyView renderFooter theme
            if reconnecting then yield lazyView renderReconnectingModal theme
            else if authState.SigningOut then yield lazyView renderSigningOutModal theme
            else
                match authState.ChangePasswordModalState, authState.ChangeImageUrlModalState with
                | Some changePasswordModalState, _ ->
                    yield lazyView2 renderChangePasswordModal (theme, authState.AuthUser.User.UserName, changePasswordModalState)
                        (ChangePasswordModalInput >> AuthInput >> AppInput >> dispatch)
                | None, Some changeImageUrlModalState ->
                    yield lazyView2 renderChangeImageUrlModal (theme, authState.AuthUser.User.UserName, changeImageUrlModalState)
                        (ChangeImageUrlModalInput >> AuthInput >> AppInput >> dispatch)
                | None, None -> () ]
