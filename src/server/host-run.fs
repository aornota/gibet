module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Api.UserApiAgent
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Server.Bridge.State
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo
open Aornota.Gibet.Server.Repo.IUserRepo

open System
open System.IO

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open Giraffe
open Giraffe.SerilogExtensions

open Elmish
open Elmish.Bridge

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

open Serilog
open Aornota.Gibet.Common

let [<Literal>] private DEFAULT_SERVER_PORT = 8085us

Log.Logger <- createLogger()

Log.Logger.Information("Starting server...")

let private publicPath =
    let publicPath = ("..", "ui/public") |> Path.Combine |> Path.GetFullPath // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else "public" |> Path.GetFullPath // e.g. when published/deployed

let private bridge : HttpFunc -> Http.HttpContext -> HttpFuncResult = // not sure why type annotation is necessary
    Bridge.mkServer BRIDGE_ENDPOINT initialize transition
    |> Bridge.register RemoteServerInput
    |> Bridge.whenDown Disconnected
    |> Bridge.withServerHub hub
#if DEBUG
    |> Bridge.withConsoleTrace
#endif
    |> Bridge.run Giraffe.server

let private userApi : HttpFunc -> Http.HttpContext -> HttpFuncResult = // not sure why type annotation is necessary
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    // TODO-NMB?...|> Remoting.withErrorHandler...
    |> Remoting.fromReader userApiReader
    |> Remoting.buildHttpHandler

let private webAppWithLogging =
    choose [
        bridge
        userApi
    ] |> SerilogAdapter.Enable

let private userRepo = Log.Logger |> InMemoryUserRepoAgent.InMemoryUserRepoAgent :> IUserRepo
//#if DEBUG
userRepo |> UserTestData.create Log.Logger |> Async.RunSynchronously |> ignore
//#endif

let private configureLogging(loggingBuilder:ILoggingBuilder) =
    loggingBuilder.ClearProviders() |> ignore // to suppress "info: Microsoft.AspNetCore.Hosting.Internal.WebHost" stuff

let private configure(applicationBuilder:IApplicationBuilder) =
    applicationBuilder
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseWebSockets()
        .UseGiraffe(webAppWithLogging)

let private configureServices(services:IServiceCollection) =
    Log.Logger |> services.AddSingleton |> ignore
    hub |> services.AddSingleton |> ignore
    userRepo |> services.AddSingleton |> ignore
    services.AddSingleton<UserApiAgent, UserApiAgent>() |> ignore
    services.AddGiraffe() |> ignore

let private host =
    WebHost.CreateDefaultBuilder()
        .UseKestrel()
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .ConfigureLogging(Action<ILoggingBuilder> configureLogging)
        .Configure(Action<IApplicationBuilder> configure)
        .ConfigureServices(configureServices)
        .UseUrls(sprintf "http://0.0.0.0:%i/" DEFAULT_SERVER_PORT)
        .Build()

host.Run()
