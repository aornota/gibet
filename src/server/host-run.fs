module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Server.Api.CounterApi
open Aornota.Gibet.Server.Logger

open System
open System.IO

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open Giraffe
open Giraffe.SerilogExtensions

// TODO-NMB...open Elmish.Bridge

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

open Serilog

let [<Literal>] private DEFAULT_SERVER_PORT = 8085us

// TODO-NMB?...let private serverStarted = DateTimeOffset.UtcNow

let private publicPath =
    let publicPath = Path.Combine("..", "ui/public") |> Path.GetFullPath // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else "public" |> Path.GetFullPath // e.g. when published/deployed

// TODO-NMB: Use choose [ ... ] for multiple APIs...
// TODO-NMB: Add Elmish.Bridge...

let private webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromContext counterApi
    |> Remoting.buildHttpHandler

let private webAppWithLogging = SerilogAdapter.Enable(webApp)

Log.Logger <- createLogger()

let private configureApp (app:IApplicationBuilder) =
    app.UseDefaultFiles()
        .UseStaticFiles()
        .UseGiraffe webAppWithLogging

let private configureServices (services:IServiceCollection) =
    services.AddGiraffe() |> ignore

let private configureLogging (builder:ILoggingBuilder) =
    builder.ClearProviders() |> ignore // to suppress "info: Microsoft.AspNetCore.Hosting.Internal.WebHost" stuff

let private host =
    WebHost.CreateDefaultBuilder()
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .ConfigureLogging(Action<ILoggingBuilder> configureLogging)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .UseUrls(sprintf "http://0.0.0.0:%i/" DEFAULT_SERVER_PORT)
        .Build()

host.Run()
