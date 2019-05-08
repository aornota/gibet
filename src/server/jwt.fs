module Aornota.Gibet.Server.Jwt

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.IfDebug
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Common.UnexpectedError
open Aornota.Gibet.Server.Common.InvalidCredentials

open System.IO
open System.Security.Cryptography

open Jose

open Thoth.Json.Net

let [<Literal>] private JWT_KEY_FILE = "./secret/jwt.key"
let [<Literal>] private JWE_ALGORITHM = JweAlgorithm.A256KW
let [<Literal>] private JWE_ENCRYPTION = JweEncryption.A256CBC_HS512

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

let toJwt userId userType =
    try let jwt = encode(Json(Encode.Auto.toString<UserId * UserType>(4, (userId, userType))))
        Ok jwt
    with | exn -> Error (ifDebug exn.Message UNEXPECTED_ERROR)
let fromJwt (jwt) =
    try let (Json json) = decode jwt
        Decode.Auto.fromString<UserId * UserType> json
    with | exn -> Error (ifDebug exn.Message INVALID_CREDENTIALS)
