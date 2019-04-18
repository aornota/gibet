module Aornota.Gibet.UI.Common.Marked

// Note: Adapted from https://github.com/MangelMaxime/Fulma/blob/master/docs/src/Libs/Fable.Import.Marked.fs.

open Fable.Core
open Fable.Core.JsInterop

[<AllowNullLiteral>]
type MarkedStatic =
    abstract Renderer : obj with get, set
    abstract Parser : obj with get, set
    [<Emit("$0($1...)")>] abstract Invoke : string * JsFunc -> string
    [<Emit("$0($1...)")>] abstract Invoke : string * MarkedOptions * JsFunc -> string
    abstract lexer : string * MarkedOptions -> ResizeArray<obj>
    abstract parse : string * JsFunc -> string
    abstract parse : string * ?options: MarkedOptions * ?callback: JsFunc -> string
    abstract parser : ResizeArray<obj> * MarkedOptions -> string
    abstract setOptions : MarkedOptions -> MarkedStatic

and [<AllowNullLiteral>] MarkedRenderer =
    abstract code : string * string -> string
    abstract blockquote : string -> string
    abstract html : string -> string
    abstract heading : string * float -> string
    abstract hr : unit -> string
    abstract list : string * bool -> string
    abstract listitem : string -> string
    abstract paragraph : string -> string
    abstract table : string * string -> string
    abstract tablerow : string -> string
    abstract tablecell : string * obj -> string
    abstract strong : string -> string
    abstract em : string -> string
    abstract codespan : string -> string
    abstract br : unit -> string
    abstract del : string -> string
    abstract link : string * string * string -> string
    abstract image : string * string * string -> string
    abstract text : string -> string

and [<AllowNullLiteral>] MarkedParser =
    abstract parse : ResizeArray<obj> -> string

and [<AllowNullLiteral>] MarkedOptions =
    abstract renderer : MarkedRenderer option with get, set
    abstract gfm : bool option with get, set
    abstract tables : bool option with get, set
    abstract breaks : bool option with get, set
    abstract pedantic : bool option with get, set
    abstract sanitize : bool option with get, set
    abstract smartLists : bool option with get, set
    abstract silent : bool option with get, set
    abstract langPrefix : string option with get, set
    abstract smartypants : bool option with get, set
    abstract highlight : string * string * JsFunc -> string

[<Erase>]
type Globals =
    [<Global>]
    static member marked : MarkedStatic = jsNative
