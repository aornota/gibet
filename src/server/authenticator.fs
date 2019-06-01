module Aornota.Gibet.Server.Authenticator

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.Jwt
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Server.AppSettingsLiterals
open Aornota.Gibet.Server.InvalidCredentials
open Aornota.Gibet.Server.SourcedLogger

open System
open System.IO
open System.Security.Cryptography

open Microsoft.Extensions.Configuration

open Jose

open Thoth.Json.Net

(* Note: Can invalidate existing tokens by deleting JWT_KEY_FILE and restarting server (though can also do this by setting InvalidateExistingTokens to true in appsettings.json [or
   appsettings.[development|production].json] and restarting server). *)

type private TokenExpiry = {
    InvalidBefore : DateTimeOffset option
    TokenLifetime : float<hour> option }

let [<Literal>] private SOURCE = "Authenticator"

let [<Literal>] private EXPIRED_CREDENTIALS = "Your cached credentials have expired"

let [<Literal>] private DEFAULT_TOKEN_LIFETIME = 24.<hour>
let [<Literal>] private INFINITE = "infinite"

let [<Literal>] private JWT_KEY_FILE = "./secret/jwt.key"
let [<Literal>] private JWE_ALGORITHM = JweAlgorithm.A256KW
let [<Literal>] private JWE_ENCRYPTION = JweEncryption.A256CBC_HS512

let private jwtKey =
    let file = FileInfo(JWT_KEY_FILE)
    if not file.Exists then
        if not file.Directory.Exists then file.Directory.Create()
        let bytes : Core.byte [] = Array.zeroCreate 32
        RandomNumberGenerator.Create().GetBytes(bytes)
        File.WriteAllBytes(file.FullName, bytes)
    File.ReadAllBytes(file.FullName)

let private encode (Json json) = Jwt(JWT.Encode(json, jwtKey, JWE_ALGORITHM, JWE_ENCRYPTION))
let private decode (Jwt jwt) = Json(JWT.Decode(jwt, jwtKey, JWE_ALGORITHM, JWE_ENCRYPTION))

let private dateAndTime (timestamp:DateTimeOffset) = sprintf "%s" (timestamp.LocalDateTime.ToString("dd-MMM-yyyy HH:mm:ss"))

let private checkExpiry tokenExpiry tokenCreated =
    match tokenExpiry.InvalidBefore, tokenExpiry.TokenLifetime with
    | Some invalidBefore, _ when tokenCreated < invalidBefore -> Error(ifDebug (sprintf "Tokens created before %s are not valid" (dateAndTime invalidBefore)) EXPIRED_CREDENTIALS)
    | _, Some expiresAfter ->
        let age = (DateTimeOffset.UtcNow - tokenCreated).TotalHours * 1.<hour>
        if age > expiresAfter then Error(ifDebug (sprintf "Token expired %.2f hour/s ago" (age - expiresAfter)) EXPIRED_CREDENTIALS)
        else Ok()
    | _ -> Ok()

type Authenticator(configuration:IConfiguration, logger) =
    let sourcedLogger, logger = logger |> sourcedLogger SOURCE, ()
    do sourcedLogger.Information("Starting...")
    let invalidBefore = try if configuration.[AUTHENTICATOR__INVALIDATE_EXISTING_TOKENS] = "true" then Some DateTimeOffset.UtcNow else None with _ -> None
    let tokenLifetime =
        try let tokenLifetimeInHours = configuration.[AUTHENTICATOR__TOKEN_LIFETIME_IN_HOURS]
            if tokenLifetimeInHours = INFINITE then None
            else if String.IsNullOrWhiteSpace(tokenLifetimeInHours) then Some DEFAULT_TOKEN_LIFETIME
            else
                match Double.TryParse(tokenLifetimeInHours) with
                | true, tokenLifetime -> Some(tokenLifetime * 1.<hour>)
                | false, _ ->
                    sourcedLogger.Warning("{setting} \"{tokenLifetimeInHours}\" is not valid; defaulting to DEFAULT_TOKEN_LIFETIME", AUTHENTICATOR__TOKEN_LIFETIME_IN_HOURS, tokenLifetimeInHours)
                    Some DEFAULT_TOKEN_LIFETIME
        with _ -> Some DEFAULT_TOKEN_LIFETIME
    do
        match invalidBefore with
        | Some invalidBefore -> sourcedLogger.Warning("Existing tokens (created before {invalidBefore}) will be invalidated", dateAndTime invalidBefore)
        | None -> sourcedLogger.Information("Existing tokens will be honoured (unless expired)")
    do
        match tokenLifetime with
        | Some tokenLifetime ->
            sourcedLogger.Information("Tokens will expire after {tokenLifetime} hours (or earlier if server restarted and {setting} is \"true\")", tokenLifetime, AUTHENTICATOR__INVALIDATE_EXISTING_TOKENS)
        | None -> sourcedLogger.Warning("Tokens will never expire (unless server restarted and {setting} is \"true\")", AUTHENTICATOR__INVALIDATE_EXISTING_TOKENS)
    let tokenExpiry = { InvalidBefore = invalidBefore ; TokenLifetime = tokenLifetime }
    member __.ToJwt(userId, userType) =
        try let jwt = encode(Json(Encode.Auto.toString<UserId * UserType * DateTimeOffset>(SPACE_COUNT, (userId, userType, DateTimeOffset.UtcNow))))
            Ok jwt
        with | exn -> Error(ifDebug exn.Message UNEXPECTED_ERROR)
    member __.FromJwt(jwt) =
        try let (Json json) = decode jwt
            match Decode.Auto.fromString<UserId * UserType * DateTimeOffset> json with
            | Ok(userId, userType, tokenCreated) ->
                match checkExpiry tokenExpiry tokenCreated with
                | Ok _ -> Ok(userId, userType)
                | Error error -> Error error
            | Error error -> Error(ifDebug error INVALID_CREDENTIALS)
        with | exn -> Error(ifDebug exn.Message INVALID_CREDENTIALS)
