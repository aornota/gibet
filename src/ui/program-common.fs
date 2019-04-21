module Aornota.Gibet.Ui.Program.Common

open Aornota.Gibet.Common.Domain.User

type State = {
    AuthUser : AuthUser option
    SignInError : string option
    Users : (User list) option
    GetUsersError : string option }

type Input =
    | SignIn
    | SignInResult of Result<Result<AuthUser, string>, exn>
    | GetUsers
    | GetUsersResult of Result<Result<User list, string>, exn>

let [<Literal>] GIBET = "gibet (α)"
