module Aornota.Gibet.UI.Common.ShouldNeverHappen

let [<Literal>] SHOULD_NEVER_HAPPEN = "SHOULD NEVER HAPPEN"

let shouldNeverHappenText text = sprintf "%s -> %s" SHOULD_NEVER_HAPPEN text
