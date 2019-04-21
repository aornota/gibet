module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Server.Api.CounterApi // TEMP-NMB...
open Aornota.Gibet.Server.Api.UserApi
open Aornota.Gibet.Server.Logger
open Aornota.Gibet.Server.Repo
open Aornota.Gibet.Server.Repo.InMemoryUserRepo
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

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

open Serilog

let [<Literal>] private DEFAULT_SERVER_PORT = 8085us

Log.Logger <- createLogger()

// TODO-NMB?...let private serverStarted = DateTimeOffset.UtcNow

let private publicPath =
    let publicPath = Path.Combine("..", "ui/public") |> Path.GetFullPath // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else "public" |> Path.GetFullPath // e.g. when published/deployed

// TODO-NMB: Add Elmish.Bridge...

let private counterApi =
    Remoting.createApi()
    // TODO-NMB: Custom error handling?...
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromReader counterApiReader
    |> Remoting.buildHttpHandler

let private webApp = counterApi // TODO-NMB: Use choose [ counterApi ; ... ] for multiple APIs...
let private webAppWithLogging = SerilogAdapter.Enable webApp

let private userRepo = InMemoryUserRepo Log.Logger :> IUserRepo
//#if DEBUG
userRepo |> UserTestData.create Log.Logger |> Async.RunSynchronously |> ignore
//#endif

let private configureLogging(loggingBuilder:ILoggingBuilder) =
    loggingBuilder.ClearProviders() |> ignore // to suppress "info: Microsoft.AspNetCore.Hosting.Internal.WebHost" stuff

let private configure(applicationBuilder:IApplicationBuilder) =
    applicationBuilder
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseGiraffe(webAppWithLogging)

let private configureServices(services:IServiceCollection) =
    services.AddSingleton Log.Logger |> ignore // seems to be necessary for "let! logger = resolve<ILogger>()" stuff (e.g. in TEMP-NMB...counterApiReader)
    services.AddSingleton userRepo |> ignore
    services.AddGiraffe() |> ignore

let private host =
    WebHost.CreateDefaultBuilder()
        .UseWebRoot(publicPath)
        .UseContentRoot(publicPath)
        .ConfigureLogging(Action<ILoggingBuilder> configureLogging)
        .Configure(Action<IApplicationBuilder> configure)
        .ConfigureServices(configureServices)
        .UseUrls(sprintf "http://0.0.0.0:%i/" DEFAULT_SERVER_PORT)
        .Build()

host.Run()
