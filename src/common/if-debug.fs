module Aornota.Gibet.Common.IfDebug

open System

#if DEBUG
let [<Literal>] private FAKE_ERROR_FREQUENCY = 0.0
#endif

#if DEBUG
let private random = Random()
#endif

// #region ifDebug
let ifDebug (debugThing:'a) (notDebugThing:'a) =
#if DEBUG
    debugThing
#else
    notDebugThing
#endif
// #endregion

// #region ifDebugSleepAsync
let ifDebugSleepAsync min max = async {
#if DEBUG
    do! Async.Sleep(random.Next(min, max)) }
#else
    return() }
#endif
// #endregion

// #region debugFakeError
let debugFakeError() =
#if DEBUG
    random.NextDouble() < FAKE_ERROR_FREQUENCY
#else
    false
#endif
// #endregion

let ifDebugFakeErrorFailWith error = if debugFakeError() then failwith error
