module Aornota.Gibet.UI.Common.ShouldNeverHappen

let [<Literal>] SHOULD_NEVER_HAPPEN = "SHOULD NEVER HAPPEN"

let shouldNeverHappen error = sprintf "%s -> %s" SHOULD_NEVER_HAPPEN error
