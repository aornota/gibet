[<AutoOpen>]
module Aornota.Gibet.Common.ErrorHandling

type AsyncResult<'a, 'b> = Async<Result<'a, 'b>>
type AsyncReplyChannelResult<'a, 'b> = AsyncReplyChannel<Result<'a, 'b>>

let errorIfSome ok opt = match opt with | Some error -> Error error | None -> Ok ok

let ignoreResult result = result |> Result.map ignore
