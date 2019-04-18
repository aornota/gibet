module Aornota.Gibet.Common.Api.Route

let builder typeName methodName =
    sprintf "/api/%s/%s" typeName methodName
