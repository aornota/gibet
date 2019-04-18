module Aornota.Gibet.Common.UnitsOfMeasure

type [<Measure>] tick

type [<Measure>] point

type [<Measure>] week
type [<Measure>] day
type [<Measure>] hour
type [<Measure>] minute
type [<Measure>] second
type [<Measure>] millisecond

let [<Literal>] DAYS_PER_WEEK = 7.<day/week>
let [<Literal>] HOURS_PER_DAY = 24.<hour/day>
let [<Literal>] MINUTES_PER_HOUR = 60.<minute/hour>
let [<Literal>] SECONDS_PER_MINUTE = 60.<second/minute>
let [<Literal>] MILLISECONDS_PER_SECOND = 1000.<millisecond/second>

let weeksToDays (weeks:float<week>) = weeks * DAYS_PER_WEEK
let daysToHours (days:float<day>) = days * HOURS_PER_DAY
let hoursToMinutes (hours:float<hour>) = hours * MINUTES_PER_HOUR
let minutesToSeconds (minutes:float<minute>) = minutes * SECONDS_PER_MINUTE
let secondsToMilliseconds (seconds:float<second>) = seconds * MILLISECONDS_PER_SECOND
let minutesToMilliseconds (minutes:float<minute>) = minutes |> minutesToSeconds |> secondsToMilliseconds
let hoursToMilliseconds (hours:float<hour>) = hours |> hoursToMinutes |> minutesToMilliseconds
let daysToMilliseconds (days:float<day>) = days |> daysToHours |> hoursToMilliseconds
let weeksToMilliseconds (weeks:float<week>) = weeks |> weeksToDays |> daysToMilliseconds
let hoursToSeconds (hours:float<hour>) = hours |> hoursToMinutes |> minutesToSeconds
let daysToSeconds (days:float<day>) = days |> daysToHours |> hoursToSeconds
let weeksToSeconds (weeks:float<week>) = weeks |> weeksToDays |> daysToSeconds
let daysToMinutes (days:float<day>) = days |> daysToHours |> hoursToMinutes
let weeksToMinutes (weeks:float<week>) = weeks |> weeksToDays |> daysToMinutes
let weeksToHours (weeks:float<week>) = weeks |> weeksToDays |> daysToHours
let daysToWeeks (days:float<day>) = days / DAYS_PER_WEEK
let hoursToDays (hours:float<hour>) = hours / HOURS_PER_DAY
let minutesToHours (minutes:float<minute>) = minutes / MINUTES_PER_HOUR
let secondsToMinutes (seconds:float<second>) = seconds / SECONDS_PER_MINUTE
let millisecondsToSeconds (milliseconds:float<millisecond>) = milliseconds / MILLISECONDS_PER_SECOND
let millisecondsToMinutes (milliseconds:float<millisecond>) = milliseconds |> millisecondsToSeconds |> secondsToMinutes
let millisecondsToHours (milliseconds:float<millisecond>) = milliseconds |> millisecondsToMinutes |> minutesToHours
let millisecondsToDays (milliseconds:float<millisecond>) = milliseconds |> millisecondsToHours |> hoursToDays
let millisecondsToWeeks (milliseconds:float<millisecond>) = milliseconds |> millisecondsToDays |> daysToWeeks
let secondsToHours (seconds:float<second>) = seconds |> secondsToMinutes |> minutesToHours
let secondsToDays (seconds:float<second>) = seconds |> secondsToHours |> hoursToDays
let secondsToWeeks (seconds:float<second>) = seconds |> secondsToDays |> daysToWeeks
let minutesToDays (minutes:float<minute>) = minutes |> minutesToHours |> hoursToDays
let minutesToWeeks (minutes:float<minute>) = minutes |> minutesToDays |> daysToWeeks
let hoursToWeeks (hours:float<hour>) = hours |> hoursToDays |> daysToWeeks
