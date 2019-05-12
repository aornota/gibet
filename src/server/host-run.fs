module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Api.UserApiAgent
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
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

let [<Literal>] private DEFAULT_SERVER_PORT = 8085us

Log.Logger <- defaultLogger "logs/server_{Date}.log"

let logger = Log.Logger |> sourcedLogger "Host.Run"

logger.Information("Starting server...")

let private publicPath =
    let publicPath = Path.GetFullPath(Path.Combine("..", "ui/public")) // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else Path.GetFullPath("public") // e.g. when published/deployed

// #region bridge
let private bridge : HttpFunc -> Http.HttpContext -> HttpFuncResult = // not sure why type annotation is necessary
    Bridge.mkServer BRIDGE_ENDPOINT initialize transition
    |> Bridge.register RemoteServerInput
    |> Bridge.whenDown Disconnected
    |> Bridge.withServerHub hub
#if DEBUG
    |> Bridge.withConsoleTrace
#endif
    |> Bridge.run Giraffe.server
// #endregion

let private hub = {
    new IHub<HubState, RemoteServerInput, RemoteUiInput> with
        member __.GetModels() = hub.GetModels()
        member __.BroadcastClient msg = hub.BroadcastClient msg
        member __.BroadcastServer msg = hub.BroadcastServer msg
        member __.SendClientIf predicate msg = hub.SendClientIf predicate msg
        member __.SendServerIf predicate msg = hub.SendServerIf predicate msg }

let private userApi : HttpFunc -> Http.HttpContext -> HttpFuncResult = // not sure why type annotation is necessary
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromReader userApiReader
    |> Remoting.buildHttpHandler

let private webAppWithLogging =
    choose [
        bridge
        userApi
    ] |> SerilogAdapter.Enable

let private userRepo = InMemoryUserRepoAgent.InMemoryUserRepoAgent(Log.Logger) :> IUserRepo
//#if DEBUG
UserTestData.create userRepo |> Async.RunSynchronously |> ignore
//#endif

let private configureLogging(loggingBuilder:ILoggingBuilder) = loggingBuilder.ClearProviders() |> ignore // to suppress "info: Microsoft.AspNetCore.Hosting.Internal.WebHost" stuff
let private configure(applicationBuilder:IApplicationBuilder) =
    applicationBuilder
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseWebSockets()
        .UseGiraffe(webAppWithLogging)
let private configureServices(services:IServiceCollection) =
    services.AddSingleton(Log.Logger) |> ignore
    services.AddSingleton(hub) |> ignore
    services.AddSingleton(userRepo) |> ignore
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
