module Aornota.Gibet.Common.IfDebug

open System

#if DEBUG
let [<Literal>] private FAKE_ERROR_FREQUENCY = 0.0 // TODO-AS-REQUIRED: Revert to, e.g., 0.02...
#endif

#if DEBUG
let private random = Random()
#endif

let ifDebug (debugThing:'a) (notDebugThing:'a) =
#if DEBUG
    debugThing
#else
    notDebugThing
#endif

let ifDebugSource source text =
#if DEBUG
    sprintf "%s: %s" source text
#else
    text
#endif

let ifDebugSleepAsync min max = async {
#if DEBUG
    do! Async.Sleep(random.Next(min, max)) }
#else
    return() }
#endif

let debugFakeError() =
#if DEBUG
    random.NextDouble() < FAKE_ERROR_FREQUENCY
#else
    false
#endif

let ifDebugFakeErrorFailWith errorText = if debugFakeError() then failwith errorText
