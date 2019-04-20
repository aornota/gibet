module Aornota.Gibet.Common.ResilientMailbox

// Based on http://www.fssnip.net/p2/title/MailboxProcessor-with-exception-handling-and-restarting.

(* Probably of limited use: after exception, encapsulated MailboxProcessor will "reinitialize" - so loses its current state. (Might still be useful if this state is effectively
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
    member __.Scan(scanner) = inbox.Scan(scanner)
    member __.Post(message:'T) = inbox.Post(message)
    member __.PostAndReply(buildMessage) = inbox.PostAndReply(buildMessage)
    member __.PostAndAsyncReply(buildMessage) = inbox.PostAndAsyncReply(buildMessage)
    static member Start(f) =
        let mbox = new ResilientMailbox<_>(f)
        mbox.Start()
        mbox
