module Aornota.Gibet.Common.UnexpectedError

let [<Literal>] UNEXPECTED_ERROR = "An unexpected error has occurred"

let unexpectedErrorWhen text = sprintf "%s when %s" UNEXPECTED_ERROR text
