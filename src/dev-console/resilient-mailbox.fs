module Aornota.Gibet.DevConsole.ResilientMailbox

// Based on http://www.fssnip.net/p2/title/MailboxProcessor-with-exception-handling-and-restarting.

(* Probably of limited use: after exception, encapsulated MailboxProcessor will "reinitialize" - so losts its current state. (Might still be useful if this state is effectively
   a "cache", e.g. if it can be reconstructed from an external source?) *)

type ResilientMailbox<'T> private(f:ResilientMailbox<'T> -> Async<unit>) as self =
    let event = Event<_>()
    let inbox = new MailboxProcessor<_>(fun _ ->
        let rec loop() = async {
            try return! f self
            with exn ->
                event.Trigger(exn)
                return! loop()
            }
        loop()
        )
    member __.OnError = event.Publish
    member __.Start() = inbox.Start()
    member __.Receive() = inbox.Receive()
    member __.Post(v:'T) = inbox.Post(v)
    static member Start(f) =
        let mbox = new ResilientMailbox<_>(f)
        mbox.Start()
        mbox
