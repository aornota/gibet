# gibet (α) | ![gibet](https://github.com/aornota/gibet/blob/master/src/resources/gibet-16x16.ico)

## Prerequisites

* Microsoft .NET Core 2.2 SDK: currently using 2.2.202 (x64)
* FAKE 5: _dotnet tool install --global fake-cli_; currently using 5.12.6
* Paket: _dotnet tool install --global paket_; currently using 5.200.4
* Yarn: currently using 1.12.3
* Node.js (LTS): currently using 10.15.0

### Also recommended

* Microsoft Visual Studio Code with the following extensions:
    * Microsoft C#
    * Ionide Ionide-fsharp
    * Microsoft Debugger for Chrome
    * EditorConfig for VS Code
    * Rainbow Brackets
* Google Chrome with the following extensions:
    * React Developer Tools
    * Redux DevTools
* (Microsoft .NET Framework 4.72 SDK: this appeared to resolve problems with Intellisense in _build.fsx_)

## History

* Installed SAFE templates for .NET Core: _dotnet new -i "SAFE.Template::*"_
* Created from template: _dotnet new SAFE -- server giraffe -- layout fulma-basic -- communication remoting -- pattern default -- deploy azure -- js-deps yarn_

## Building / running

* Build for production / release: _fake build --target build_
* Run / watch: _fake build --target run_

## TODO...

TODO...
