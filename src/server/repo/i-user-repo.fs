module Aornota.Gibet.Server.Repo.IUserRepo

open Aornota.Gibet.Common
open Aornota.Gibet.Common.Domain.User
open Aornota.Gibet.Common.Revision

open System
open System.Security.Cryptography
open System.Text

let [<Literal>] INVALID_CREDENTIALS = "Invalid credentials"

type IUserRepo =
    abstract SignIn: UserName * Password -> AsyncResult<UserId * MustChangePasswordReason option, string>
    abstract AutoSignIn: UserId -> AsyncResult<UserId * MustChangePasswordReason option, string>
    abstract ChangePassword: UserId * Password * Rvn -> AsyncResult<User, string>
    abstract GetUsers: unit -> AsyncResult<User list, string>
    abstract CreateUser: UserId option * UserName * Password * UserType -> AsyncResult<User, string>
    abstract ResetPassword: UserId * Password * Rvn -> AsyncResult<User, string>
    abstract ChangeUserType: UserId * UserType * Rvn -> AsyncResult<User, string>
    // TODO-NMB: More?...

type Salt = | Salt of string
type Hash = | Hash of string

let private rng = RandomNumberGenerator.Create()
let private sha512 = SHA512.Create()
let private encoding = Encoding.UTF8

let salt() =
    let bytes : byte[] = Array.zeroCreate 32
    rng.GetBytes(bytes)
    bytes |> Convert.ToBase64String |> Salt
let hash(Password password, Salt salt) =
    let bytes = sprintf "%s|%s" password salt |> encoding.GetBytes |> sha512.ComputeHash
    bytes |> Convert.ToBase64String |> Hash
