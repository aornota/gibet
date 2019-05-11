module Aornota.Gibet.Ui.Common.Tooltip

open Aornota.Gibet.Ui.Common.Theme

open System

open Fulma
open Fulma.Extensions.Wikiki

type TooltipPosition =
    | TooltipTop
    | TooltipRight
    | TooltipBottom
    | TooltipLeft

type TooltipData = {
    TooltipPosition : TooltipPosition
    TooltipColour : IColor
    TooltipText : string
    IsMultiLine : bool }

let [<Literal>] private SPACE = " "

let tooltip position colour text = {
    TooltipPosition = position
    TooltipColour = colour
    TooltipText = text
    IsMultiLine = false }
let tooltipMultiLine position colour text = { tooltip position colour text with IsMultiLine = true }

let tooltipClass tooltip =
    let customClasses = [
        yield Tooltip.ClassName
        match tooltip.TooltipPosition with
        | TooltipTop -> yield Tooltip.IsTooltipTop
        | TooltipRight -> yield Tooltip.IsTooltipRight
        | TooltipBottom -> yield Tooltip.IsTooltipBottom
        | TooltipLeft -> yield Tooltip.IsTooltipLeft
        yield sprintf "is-tooltip-%s" (colourText tooltip.TooltipColour)
        if tooltip.IsMultiLine then yield Tooltip.IsMultiline ]
    match customClasses with | _ :: _ -> String.concat SPACE customClasses | [] -> String.Empty
let tooltipProps tooltip = Tooltip.dataTooltip tooltip.TooltipText
