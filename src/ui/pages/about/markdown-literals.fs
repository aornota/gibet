module Aornota.Gibet.Ui.Pages.About.MarkdownLiterals

// #region READ_ME
let [<Literal>] READ_ME = """# ![gibet](https://raw.githubusercontent.com/aornota/gibet/master/src/ui/public/gibet-24x24.png) | gibet (λ)

An **opinionated** (i.e. decidedly eccentric) "scaffold"/example for [F#](http://fsharp.org/) web-app development using [Fable](http://fable.io/), [Elmish](https://elmish.github.io/),
[Fulma](https://github.com/Fulma/Fulma/) / [Bulma](https://bulma.io/), [Fable.Remoting](https://github.com/Zaid-Ajaj/Fable.Remoting/),
[Elmish.Bridge](https://github.com/Nhowka/Elmish.Bridge/), [Giraffe](https://github.com/giraffe-fsharp/Giraffe/) and [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/).

The [example web-app](https://gibet.azurewebsites.net/) is running on Azure - albeit on a free service plan (F1), which limits the number of concurrent websocket connections to a
paltry 5.

And yes, I know that a _gibet_ (gibbet) is not the same as a scaffold - but I love Ravel's _Gaspard de la nuit_, especially _[Le Gibet](https://www.youtube.com/watch?v=vRQF490yyAY/)_.

### Prerequisites

- [Microsoft .NET Core 2.2 SDK](https://dotnet.microsoft.com/download/dotnet-core/2.2/): I'm currently using 2.2.202 (x64)
- [FAKE 5](https://fake.build/): _dotnet tool install --global fake-cli_: I'm currently using 5.12.6
- [Paket](https://fsprojects.github.io/Paket/): _dotnet tool install --global paket_: I'm currently using 5.200.4
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
- ([Microsoft .NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472/): this appeared to resolve problems with Intellisense in
_[build.fsx](https://github.com/aornota/gibet/blob/master/build.fsx)_)

### History

- Installed SAFE templates for .NET Core: _dotnet new -i "SAFE.Template::*"_
- Created from template: _dotnet new SAFE --server giraffe --layout fulma-basic --communication remoting --pattern default --deploy azure --js-deps yarn_

### Running / building / deploying

- Run/watch for development (debug): _fake build --target run_ (or _fake build -t run_)
- Build for production (release): _fake build --target build_ (or _fake build -t build_)
- Publish for production (release): _fake build --target publish_ (or _fake build -t publish_)
- Deploy to Azure (release): _fake build --target deploy-azure_ (or _fake build -t deploy-azure_);
see [Registering with Azure](https://safe-stack.github.io/docs/template-azure-registration/) and [Deploy to App Service](https://safe-stack.github.io/docs/template-appservice/)
- Run the dev-console (debug): _fake build --target run-dev-console_ (or _fake build -t run-dev-console_)
- Help (lists key targets): _fake build --target help_ (or just _fake build_)

### Unit tests

There are no unit tests yet ;(

However, the repository and web API services have been designed to work with ASP.NET Core dependency injection, which should also facilitate unit testing.

See _[test-user-repo-and-api.fs](https://github.com/aornota/gibet/blob/master/src/dev-console/test-user-repo-and-api.fs)_ for an example of "testing" IUserRepo
(e.g. InMemoryUserRepoAgent) and UserApi (e.g. UserApiAgent) from a console project.

## To do

- [ ] extend functionality, e.g. Sql Server users repository? | Sqlite users repository? | &c.
- [ ] unit tests? AspNetCore.TestHost?
- [ ] additional documentation, e.g. [(currently non-existent) gh-pages branch](https://aornota.github.io/gibet/)?"""
// #endregion
