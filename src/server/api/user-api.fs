module Aornota.Gibet.Server.Api.UserApi

open Aornota.Gibet.Common.Api.IUserApi
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Server.Repo.IUserRepo

open Serilog

// TODO-NMB: Implement (via agent?)...

(* let private initialCounter (logger:ILogger) =
    fun () -> async {
        logger.Debug("Retrieving initial counter...")
        let value = { Value = 42 }
        logger.Debug("...retrieved initial counter: {value}", value)
        return value }

let counterApiReader =
    reader {
        let! logger = resolve<ILogger>()
        return {
            initialCounter = initialCounter logger
        } } *)
