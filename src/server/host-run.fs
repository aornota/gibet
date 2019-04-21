module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Server.Api.UserApi
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

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

open Serilog

let [<Literal>] private DEFAULT_SERVER_PORT = 8085us

Log.Logger <- createLogger()

Log.Logger.Information("Starting server...")

// TODO-NMB?...let private serverStarted = DateTimeOffset.UtcNow

let private publicPath =
    let publicPath = ("..", "ui/public") |> Path.Combine |> Path.GetFullPath // e.g. when served via webpack-dev-server
    if Directory.Exists publicPath then publicPath else "public" |> Path.GetFullPath // e.g. when published/deployed

// TODO-NMB: Add Elmish.Bridge...

let private userApi : HttpFunc -> Http.HttpContext -> HttpFuncResult = // not sure why type annotation is necessary
    Remoting.createApi()
    // TODO-NMB: Custom error handling?...
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromReader userApiReader
    |> Remoting.buildHttpHandler

let private webAppWithLogging = userApi |> SerilogAdapter.Enable // TODO-NMB: Replace "userApi" with "choose [ userApi ; {xyzApi} ]"...

let private userRepo = Log.Logger |> InMemoryUserRepo.InMemoryUserRepo :> IUserRepo
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
    Log.Logger |> services.AddSingleton |> ignore
    userRepo |> services.AddSingleton |> ignore
    services.AddSingleton<UserApi, UserApi>() |> ignore
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
