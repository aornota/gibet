module Aornota.Gibet.Server.Startup

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Api.ChatApiAgent
open Aornota.Gibet.Server.Api.UserApiAgent
open Aornota.Gibet.Server.Authenticator
open Aornota.Gibet.Server.Bridge.State
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.Repo
open Aornota.Gibet.Server.Repo.IUserRepo
open Aornota.Gibet.Server.SourcedLogger

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Giraffe.SerilogExtensions

open Elmish
open Elmish.Bridge

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

open Serilog

// #region bridge
let private bridgeServer (logger:ILogger) =
    Bridge.mkServer BRIDGE_ENDPOINT initialize (transition logger)
    |> Bridge.register RemoteServerInput
    |> Bridge.whenDown Disconnected
    |> Bridge.withServerHub serverHub
#if DEBUG
    |> Bridge.withConsoleTrace
#endif
    |> Bridge.run Giraffe.server
// #endregion

let private userApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromReader userApiReader
    |> Remoting.buildHttpHandler
let private chatApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromReader chatApiReader
    |> Remoting.buildHttpHandler

let private hub = {
    new IHub<HubState, RemoteServerInput, RemoteUiInput> with
        member __.GetModels() = serverHub.GetModels()
        member __.BroadcastClient msg = serverHub.BroadcastClient msg
        member __.BroadcastServer msg = serverHub.BroadcastServer msg
        member __.SendClientIf predicate msg = serverHub.SendClientIf predicate msg
        member __.SendServerIf predicate msg = serverHub.SendServerIf predicate msg }

type Startup(configuration:IConfiguration) =
    do Log.Logger <-
        LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Destructure.FSharpTypes()
            .CreateLogger()
    let sourcedLogger = Log.Logger |> sourcedLogger "Server.Startup"
    do sourcedLogger.Information("Starting...")
    // TEMP-NMB: Note helpful GetConnectionString method...
    do sourcedLogger.Information("TEMP-NMB...Sqlite connection string is {Sqlite}", configuration.GetConnectionString("Sqlite"))
    // ...NMB-TEMP
    let userRepo = configuration.["Repo:IUserRepo"]
    let userRepo, createInitialUsers =
        match userRepo with
        | "InMemoryUserRepoAgent" -> InMemoryUserRepoAgent.InMemoryUserRepoAgent(Log.Logger) :> IUserRepo, true
        | "SqliteUserRepo" ->
            // TODO-NMB: Once SqliteUserRepo implemented, createInitialUsers only if configuration.["Repo:Sqlite:WipeAndCreate"] = "true"?...
            // TEMP-NMB...
            sourcedLogger.Warning("SqliteUserRepo has not yet been implemented; using InMemoryUserRepoAgent instead")
            InMemoryUserRepoAgent.InMemoryUserRepoAgent(Log.Logger) :> IUserRepo, true
            // ...TEMP-NMB
        | _ -> failwithf "\"%s\" is not a valid configuration option for Repo.IUserRepo" userRepo
    do if createInitialUsers then InitialUsers.createInitialUsers userRepo Log.Logger |> Async.RunSynchronously |> ignore
    member __.Configure(applicationBuilder:IApplicationBuilder) =
        let webAppWithLogging = choose [ bridgeServer Log.Logger ; userApi ; chatApi ] |> SerilogAdapter.Enable
        applicationBuilder
            .UseDefaultFiles()
            .UseStaticFiles()
            .UseWebSockets()
            .UseGiraffe(webAppWithLogging)
    member __.ConfigureServices(services:IServiceCollection) =
        services.AddSingleton(Log.Logger) |> ignore
        services.AddSingleton(hub) |> ignore
        // TODO-NMB: Make conditional on configuration, e.g. if want SqliteUserRepo to be transient rather than singleton?...
        services.AddSingleton(userRepo) |> ignore
        services.AddSingleton<Authenticator, Authenticator>() |> ignore
        services.AddSingleton<UserApiAgent, UserApiAgent>() |> ignore
        services.AddSingleton<ChatApiAgent, ChatApiAgent>() |> ignore
        services.AddGiraffe() |> ignore
