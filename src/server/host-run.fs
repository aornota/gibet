module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Server.Api.CounterApi

open System
open System.IO

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.WindowsAzure.Storage

open Giraffe

open Elmish.Bridge

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

let [<Literal>] private DEFAULT_SERVER_PORT = 8085us

// TODO-NMB?...let private serverStarted = DateTimeOffset.UtcNow

let private publicPath =
    let publicPath = Path.Combine("..", "ui/public") |> Path.GetFullPath // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else "public" |> Path.GetFullPath // e.g. when published/deployed

let private webApp =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue counterApi
    |> Remoting.buildHttpHandler

let private configureApp (app:IApplicationBuilder) =
    app.UseDefaultFiles()
        .UseStaticFiles()
        .UseGiraffe webApp

let private configureServices (services:IServiceCollection) =
    services.AddGiraffe() |> ignore

let private host =
    WebHost.CreateDefaultBuilder()
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .UseUrls(sprintf "http://0.0.0.0:%i/" DEFAULT_SERVER_PORT)
        .Build()

host.Run()
