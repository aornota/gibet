module Aornota.Gibet.Server.Startup

open Aornota.Gibet.Common.Api
open Aornota.Gibet.Common.Bridge
open Aornota.Gibet.Server.Api.ChatApiAgent
open Aornota.Gibet.Server.Api.UsersApiAgent
open Aornota.Gibet.Server.Authenticator
open Aornota.Gibet.Server.Bridge.State
open Aornota.Gibet.Server.Bridge.Hub
open Aornota.Gibet.Server.Bridge.HubState
open Aornota.Gibet.Server.Bridge.IHub
open Aornota.Gibet.Server.InitialUsers
open Aornota.Gibet.Server.SourcedLogger

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

open Elmish

open Fable.Remoting.Giraffe
open Fable.Remoting.Server

open FsToolkit.ErrorHandling

open Giraffe
open Giraffe.SerilogExtensions

open Elmish.Bridge // note: needs to be after open Giraffe (else tries to open Elmish.Bridge.Giraffe)

open Serilog

let [<Literal>] private SOURCE = "Server.Startup"

// #region bridge
let private bridgeServer logger =
    Bridge.mkServer BRIDGE_ENDPOINT initialize (transition logger)
    |> Bridge.register RemoteServerInput
    |> Bridge.whenDown Disconnected
    |> Bridge.withServerHub serverHub
#if DEBUG
    |> Bridge.withConsoleTrace
#endif
    |> Bridge.run Giraffe.server
// #endregion

let private usersApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromReader usersApiReader
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
    let sourcedLogger = Log.Logger |> sourcedLogger SOURCE
    do sourcedLogger.Information("Starting...")
    let authenticator = Authenticator(configuration, Log.Logger)
    let usersRepo, usersApiAgent =
        match createInitialUsers hub authenticator Log.Logger |> Async.RunSynchronously with
        | Ok(usersRepo, usersApiAgent) -> usersRepo, usersApiAgent
        | Error error -> failwithf "Unable to create initial Users -> %s" error
    member __.Configure(applicationBuilder:IApplicationBuilder) =
        let webAppWithLogging = choose [ bridgeServer Log.Logger ; usersApi ; chatApi ] |> SerilogAdapter.Enable
        applicationBuilder
            .UseDefaultFiles()
            .UseStaticFiles()
            .UseWebSockets()
            .UseGiraffe(webAppWithLogging)
    member __.ConfigureServices(services:IServiceCollection) =
        services.AddSingleton(Log.Logger) |> ignore
        services.AddSingleton(hub) |> ignore
        services.AddSingleton(usersRepo) |> ignore
        services.AddSingleton(authenticator) |> ignore
        services.AddSingleton(usersApiAgent) |> ignore
        services.AddSingleton<ChatApiAgent, ChatApiAgent>() |> ignore
        services.AddGiraffe() |> ignore
