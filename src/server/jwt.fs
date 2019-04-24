module Aornota.Gibet.Server.Jwt

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Json

open System.IO
open System.Security.Cryptography

open Jose

open Thoth.Json.Net

let [<Literal>] private JWT_KEY_FILE = "./secret/jwt.key"
let [<Literal>] private JWE_ALGORITHM = JweAlgorithm.A256KW
let [<Literal>] private JWE_ENCRYPTION = JweEncryption.A256CBC_HS512

let private jwtKey =
    let file = JWT_KEY_FILE |> FileInfo
    if file.Exists |> not then
        if file.Directory.Exists |> not then file.Directory.Create()
        let bytes : byte [] = 32 |> Array.zeroCreate
        RandomNumberGenerator.Create().GetBytes bytes
        File.WriteAllBytes(file.FullName, bytes)
    file.FullName |> File.ReadAllBytes

let private encode(Json json) =
    JWT.Encode(json, jwtKey, JWE_ALGORITHM, JWE_ENCRYPTION) |> Jwt
let private decode(Jwt jwt) =
    JWT.Decode(jwt, jwtKey, JWE_ALGORITHM, JWE_ENCRYPTION) |> Json

let toJwt(userId:UserId, userType:UserType) =
    try Encode.Auto.toString<UserId * UserType>(4, (userId, userType)) |> Json |> encode |> Ok
    with | exn -> exn.Message |> Error
let fromJwt(jwt) =
    try let (Json json) = jwt |> decode
        json |> Decode.Auto.fromString<UserId * UserType>
    with | exn -> exn.Message |> Error
