module Aornota.Gibet.DevConsole.TestTaggingRegex

open Aornota.Gibet.Server.SourcedLogger

open System
open System.Text.RegularExpressions

let [<Literal>] private SOURCE = "TestTaggingRegex"

let testTaggingRegex logger =
    let sourcedLogger, logger = logger |> sourcedLogger SOURCE, ()
    let userNames = [ "superuser" ; "admin" ; "guest" ; "john.doe" ; "billy-bob" ; "o'mahony" ; "user 1" ]
    let message = "Tagging users: @{superuser} | @admin | @guessed | @john.doe | @billy-bob | @o'mahony | @{user 1} | @{user 666}..."
    sourcedLogger.Information("Original message: {message}", message)
    let tags =
        Regex.Matches(message, "(?<1>@[\w'-.]+)|(?<2>@{[^}]*})")
        |> List.ofSeq
        |> List.choose (fun m ->
            let group1 = m.Groups.[1].Value
            if not (String.IsNullOrWhiteSpace(group1)) then Some(group1, group1.Substring 1)
            else
                let group2 = m.Groups.[2].Value
                if not (String.IsNullOrWhiteSpace(group2)) then Some(group2, group2.Substring(2, group2.Length - 3))
                else None)
    sourcedLogger.Debug("Matches: {matches}", tags)
    let replacer (message:string) (replace, userName) =
        let replacement = if userNames |> List.contains userName then sprintf "**%s**" userName else sprintf "_%s_" replace
        message.Replace(replace, replacement)
    let message = tags |> List.fold replacer message
    sourcedLogger.Information("Modified message: {message}", message)
