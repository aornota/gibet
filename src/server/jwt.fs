module Aornota.Gibet.Server.Jwt

open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Json
//open Aornota.Gibet.Server.Common.JsonConverter

open System.IO
open System.Security.Cryptography

open Jose

open Thoth.Json.Net

// TODO-NMB: Switch to using Thoth.Json (i.e. rather than JsonConverter)?...

let [<Literal>] private JWT_KEY_FILE = "./secret/jwt.txt" // TODO-NMB: .gitignore | .vscode...

let private jwtKey =
    let file = JWT_KEY_FILE |> FileInfo
    if file.Exists |> not then
        if file.Directory.Exists |> not then file.Directory.Create()
        let bytes : byte [] = 32 |> Array.zeroCreate
        RandomNumberGenerator.Create().GetBytes bytes
        File.WriteAllBytes(file.FullName, bytes)
    file.FullName |> File.ReadAllBytes

let private encode(Json json) =
    JWT.Encode(json, jwtKey, JweAlgorithm.A256KW, JweEncryption.A256CBC_HS512)
let private decode text =
    JWT.Decode(text, jwtKey, JweAlgorithm.A256KW, JweEncryption.A256CBC_HS512) |> Json

let toJwt(userId:UserId, userType:UserType) =
    try Encode.Auto.toString<UserId * UserType>(4, (userId, userType)) |> Jwt |> Ok
        (* (userId, userType) |> toJson |> encode |> Jwt |> Ok // using Fable.JsonConverter *)
    with | exn -> exn.Message |> Error

let fromJwt(Jwt jwt) =
    try jwt |> Decode.Auto.fromString<UserId * UserType>
    with | exn -> exn.Message |> Error
    (* try
        let userId, permissions = jwt |> decode |> ofJson<UserId * UserType> // using Fable.JsonConverter
        (userId, permissions) |> Ok
    with | exn -> exn.Message |> Error *)
