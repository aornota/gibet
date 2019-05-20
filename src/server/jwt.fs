module Aornota.Gibet.Server.Jwt

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Common.UnitsOfMeasure
open Aornota.Gibet.Server.Common.InvalidCredentials

open System
open System.IO
open System.Security.Cryptography

open Jose

open Thoth.Json.Net

type private TokenExpiry = {
    InvalidBefore : DateTimeOffset option
    ExpiresAfter : float<hour> option }

let [<Literal>] private EXPIRED_CREDENTIALS = "Your cached credentials have expired"

let [<Literal>] private TOKEN_LIFETIME = 168.<hour>

let [<Literal>] private JWT_KEY_FILE = "./secret/jwt.key"
let [<Literal>] private JWE_ALGORITHM = JweAlgorithm.A256KW
let [<Literal>] private JWE_ENCRYPTION = JweEncryption.A256CBC_HS512

let private tokenLifetime = ifDebug (Some 24.<hour>) (Some TOKEN_LIFETIME)

let private jwtKey =
    let file = FileInfo(JWT_KEY_FILE)
    if not file.Exists then
        if not file.Directory.Exists then file.Directory.Create()
        let bytes : byte [] = Array.zeroCreate 32
        RandomNumberGenerator.Create().GetBytes(bytes)
        File.WriteAllBytes(file.FullName, bytes)
    File.ReadAllBytes(file.FullName)

let private encode (Json json) = Jwt(JWT.Encode(json, jwtKey, JWE_ALGORITHM, JWE_ENCRYPTION))
let private decode (Jwt jwt) = Json(JWT.Decode(jwt, jwtKey, JWE_ALGORITHM, JWE_ENCRYPTION))

let private expiry = {
    InvalidBefore = None // note: can use Some DateTimeOffset.UtcNow to invalidate all existing tokens whenever server is restarted (though can also do this by deleting JWT_KEY_FILE and restarting server)
    ExpiresAfter = tokenLifetime }

let private checkExpiry (tokenCreated:DateTimeOffset) =
    match expiry.InvalidBefore, expiry.ExpiresAfter with
    | Some invalidBefore, _ when tokenCreated < invalidBefore -> Error (ifDebug (sprintf "Jwt tokens created before %A are not valid" invalidBefore) EXPIRED_CREDENTIALS)
    | _, Some expiresAfter ->
        let age = (DateTimeOffset.UtcNow - tokenCreated).TotalHours * 1.<hour>
        if age > expiresAfter then
            let ago = float(age - expiresAfter)
            Error (ifDebug (sprintf "Jwt token expired %.2f hour/s ago" ago) EXPIRED_CREDENTIALS)
        else Ok()
    | _ -> Ok()

let toJwt userId userType =
    try let jwt = encode(Json(Encode.Auto.toString<UserId * UserType * DateTimeOffset>(SPACE_COUNT, (userId, userType, DateTimeOffset.UtcNow))))
        Ok jwt
    with | exn -> Error (ifDebug exn.Message UNEXPECTED_ERROR)
let fromJwt (jwt) =
    try let (Json json) = decode jwt
        match Decode.Auto.fromString<UserId * UserType * DateTimeOffset> json with
        | Ok(userId, userType, tokenCreated) ->
            match checkExpiry tokenCreated with
            | Ok _ -> Ok(userId, userType)
            | Error error -> Error error
        | Error error -> Error (ifDebug error INVALID_CREDENTIALS)
    with | exn -> Error (ifDebug exn.Message INVALID_CREDENTIALS)
