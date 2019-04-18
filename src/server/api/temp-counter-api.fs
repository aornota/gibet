module Aornota.Gibet.Server.Api.CounterApi

open Aornota.Gibet.Common.Api.CounterApi
open Aornota.Gibet.Common.Domain.Counter

open System.Threading.Tasks

open FSharp.Control.Tasks.V2

let private getInitCounter() : Task<Counter> = task { return { Value = 42 } }

let counterApi = { initialCounter = getInitCounter >> Async.AwaitTask }
