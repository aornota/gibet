[<AutoOpen>]
module Aornota.Gibet.Common.ErrorHandling

type AsyncResult<'a, 'b> = Async<Result<'a, 'b>>

let errorIfSome ok opt =
    match opt with
    | Some error -> error |> Error
    | None -> ok |> Ok
