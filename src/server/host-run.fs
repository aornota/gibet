module Aornota.Gibet.Server.Host.Run

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision
open Aornota.Gibet.Server.Api.CounterApi // TEMP-NMB...
open Aornota.Gibet.Server.Api.UserApi
open Aornota.Gibet.Server.Logger
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
// #region Add initial Users
Log.Logger.Debug("Adding initial users...")
async {
    let nephId = Guid "00000000-0001-0000-0000-000000000000" |> UserId
    let rosieId = Guid "00000000-0000-0001-0000-000000000000" |> UserId
    let hughId = Guid "00000000-0000-0002-0000-000000000000" |> UserId
    let willId = Guid "00000000-0000-0000-0001-000000000000" |> UserId
    let! _ = userRepo.CreateUser(nephId |> Some, "neph" |> UserName, "password" |> Password, BenevolentDictatorForLife)
    let! _ = userRepo.CreateUser(rosieId |> Some, "rosie" |> UserName, "password" |> Password, Administrator)
    let! _ = userRepo.CreateUser(hughId |> Some, "hugh" |> UserName, "password" |> Password, Pleb)
    let! _ = userRepo.CreateUser(willId |> Some, "will" |> UserName, "password" |> Password, Pleb)
    let! _ = userRepo.CreateUser(None, "satan" |> UserName, "password" |> Password, PersonaNonGrata)
    let! _ = userRepo.SignIn("neph" |> UserName, "password" |> Password)
    let! _ = userRepo.SignIn("rosie" |> UserName, "drowssap" |> Password)
    let! _ = userRepo.SignIn("hguh" |> UserName, "password" |> Password)
    let! _ = userRepo.SignIn("satan" |> UserName, "password" |> Password)
    let! _ = userRepo.ChangePassword(rosieId, "drowssap" |> Password, initialRvn)
    let! _ = userRepo.SignIn("rosie" |> UserName, "drowssap" |> Password)
    let! _ = userRepo.ChangeUserType(hughId, Administrator, initialRvn)
    return ()
} |> Async.RunSynchronously
// #endregion

let private configureLogging(loggingBuilder:ILoggingBuilder) =
    loggingBuilder.ClearProviders() |> ignore // to suppress "info: Microsoft.AspNetCore.Hosting.Internal.WebHost" stuff

let private configure(applicationBuilder:IApplicationBuilder) =
    applicationBuilder
        .UseDefaultFiles()
        .UseStaticFiles()
        .UseGiraffe(webAppWithLogging)

let private configureServices(services:IServiceCollection) =
    services.AddSingleton Log.Logger |> ignore // seems to be necessary for "let! logger = resolve<ILogger>()" stuff
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
