# ![gibet](https://github.com/aornota/gibet/blob/master/src/resources/gibet-16x16.ico) | gibet (α)

Yes, I know that a _gibet_ (gibbet) is not the same as a scaffold - but I love Ravel's _Gaspard de la nuit_, especially _[Le Gibet](https://www.youtube.com/watch?v=vRQF490yyAY)_.

### Prerequisites

- [Microsoft .NET Core 2.2 SDK](https://dotnet.microsoft.com/download/dotnet-core/2.2): currently using 2.2.202 (x64)
- [FAKE 5](https://fake.build/): _dotnet tool install --global fake-cli_; currently using 5.12.6
- [Paket](https://fsprojects.github.io/Paket/): _dotnet tool install --global paket_; currently using 5.200.4
- [Yarn](https://yarnpkg.com/lang/en/docs/install): currently using 1.12.3
- [Node.js (LTS)](https://nodejs.org/en/download/): currently using 10.15.0

#### Also recommended

- [Microsoft Visual Studio Code](https://code.visualstudio.com/download) with the following extensions:
    - [Microsoft C#](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
    - [Ionide-fsharp](https://marketplace.visualstudio.com/items?itemName=ionide.ionide-fsharp)
    - [Microsoft Debugger for Chrome](https://marketplace.visualstudio.com/items?itemName=msjsdiag.debugger-for-chrome)
    - [EditorConfig for VS Code](https://marketplace.visualstudio.com/items?itemName=editorconfig.editorconfig)
    - [Rainbow Brackets](https://marketplace.visualstudio.com/items?itemName=2gua.rainbow-brackets)
- [Google Chrome](https://www.google.com/chrome/) with the following extensions:
    - [React Developer Tools](https://chrome.google.com/webstore/detail/react-developer-tools/fmkadmapgofadopljbjfkapdkoienihi)
    - [Redux DevTools](https://chrome.google.com/webstore/detail/redux-devtools/lmhkpmbekcpmknklioeibfkpmmfibljd)
- ([Microsoft .NET Framework 4.72 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472): this appeared to resolve problems with Intellisense in _build.fsx_)

### History

- Installed SAFE templates for .NET Core: _dotnet new -i "SAFE.Template::*"_
- Created from template: _dotnet new SAFE -- server giraffe -- layout fulma-basic -- communication remoting -- pattern default -- deploy azure -- js-deps yarn_

### Building / running

- Build for production / release: _fake build --target build_
- Run / watch: _fake build --target run_

## To do

- [ ] will Fable.Remoting and Elmish.Bridge work with pre-release dependencies (Fable.Core &c.)?
- [ ] investigate build warning/s (e.g. _Could not copy the file "ApplicationInsights.config"..._)
- [ ] extend functionality (e.g. User/s repository via ASP.NET Core dependency injection; &c.)
- [ ] more dependencies: bulma-checkradio/bulma-tooltip/Fulma.Extensions?
- [ ] deploy to [Azure](https://gibet.azurewebsites.net/)?
- [ ] automated testing?
- [ ] figure out how the hashing of _.js_ files for production builds works (e.g. _index.html_ &c.)?
- [ ] additional documentation, e.g. [gh-pages branch](https://aornota.github.io/gibet)?
