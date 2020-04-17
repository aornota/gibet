module Aornota.Gibet.Server.Host

open Aornota.Gibet.Server.Startup

open System.IO

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging

open Serilog

let [<Literal>] private DEFAULT_SERVER_PORT = 8088us // see also ..\..\webpack.config.js

let private publicPath =
    let publicPath = Path.GetFullPath(Path.Combine("..", "ui/public")) // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else Path.GetFullPath("public") // e.g. when published/deployed

let private configuration =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", false)
#if DEBUG
        .AddJsonFile("appsettings.development.json", true)
#else
        .AddJsonFile("appsettings.production.json", true)
#endif
        .Build()

WebHost
    .CreateDefaultBuilder()
    .UseWebRoot(publicPath)
    .UseContentRoot(publicPath)
    .UseUrls(sprintf "http://0.0.0.0:%i/" DEFAULT_SERVER_PORT)
    .UseConfiguration(configuration)
    .ConfigureLogging(fun loggingBuilder ->
        loggingBuilder.ClearProviders() |> ignore
        loggingBuilder.AddSerilog() |> ignore)
    .UseStartup<Startup>()
    .Build()
    .Run()
