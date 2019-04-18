module Aornota.Gibet.Server.Jwt

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Json
open Aornota.Gibet.Server.Common.JsonConverter

open System.IO
open System.Security.Cryptography

open Jose

let [<Literal>] private JWT_KEY_FILE = "./secret/jwt.txt"

let private jwtKey =
    let file = FileInfo JWT_KEY_FILE
    if file.Exists |> not then
        if file.Directory.Exists |> not then file.Directory.Create()
        let bytes : byte [] = Array.zeroCreate 32
        RandomNumberGenerator.Create().GetBytes bytes
        File.WriteAllBytes(file.FullName, bytes)
    File.ReadAllBytes file.FullName

let private encode (Json json) = JWT.Encode(json, jwtKey, JweAlgorithm.A256KW, JweEncryption.A256CBC_HS512)
let private decode text = JWT.Decode(text, jwtKey, JweAlgorithm.A256KW, JweEncryption.A256CBC_HS512) |> Json

let toJwt (userId:UserId, permissions:Permissions) =
    try
        (userId, permissions) |> toJson |> encode |> Jwt |> Ok
    with | exn -> exn.Message |> Error

let fromJwt (Jwt jwt) =
    try
        let userId, permissions = jwt |> decode |> ofJson<UserId * Permissions>
        (userId, permissions) |> Ok
    with | exn -> exn.Message |> Error
