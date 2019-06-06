module Aornota.Gibet.Common.UnitsOfMeasure

type [<Measure>] tick

type [<Measure>] millisecond
type [<Measure>] second
type [<Measure>] minute
type [<Measure>] hour
type [<Measure>] day
type [<Measure>] week

let [<Literal>] MILLISECONDS_PER_SECOND = 1_000.<millisecond/second>
let [<Literal>] SECONDS_PER_MINUTE = 60.<second/minute>
let [<Literal>] MINUTES_PER_HOUR = 60.<minute/hour>
let [<Literal>] HOURS_PER_DAY = 24.<hour/day>
let [<Literal>] DAYS_PER_WEEK = 7.<day/week>

let millisecondsToSeconds (milliseconds:float<millisecond>) = milliseconds / MILLISECONDS_PER_SECOND
let secondsToMinutes (seconds:float<second>) = seconds / SECONDS_PER_MINUTE
let minutesToHours (minutes:float<minute>) = minutes / MINUTES_PER_HOUR
let hoursToDays (hours:float<hour>) = hours / HOURS_PER_DAY
let daysToWeeks (days:float<day>) = days / DAYS_PER_WEEK
let millisecondsToMinutes = millisecondsToSeconds >> secondsToMinutes
let millisecondsToHours = millisecondsToMinutes >> minutesToHours
let millisecondsToDays = millisecondsToHours >> hoursToDays
let millisecondsToWeeks = millisecondsToDays >> daysToWeeks
let secondsToHours = secondsToMinutes >> minutesToHours
let secondsToDays = secondsToHours >> hoursToDays
let secondsToWeeks = secondsToDays >> daysToWeeks
let minutesToDays = minutesToHours >> hoursToDays
let minutesToWeeks = minutesToDays >> daysToWeeks
let hoursToWeeks = hoursToDays >> daysToWeeks
let weeksToDays (weeks:float<week>) = weeks * DAYS_PER_WEEK
let daysToHours (days:float<day>) = days * HOURS_PER_DAY
let hoursToMinutes (hours:float<hour>) = hours * MINUTES_PER_HOUR
let minutesToSeconds (minutes:float<minute>) = minutes * SECONDS_PER_MINUTE
let secondsToMilliseconds (seconds:float<second>) = seconds * MILLISECONDS_PER_SECOND
let minutesToMilliseconds = minutesToSeconds >> secondsToMilliseconds
let hoursToMilliseconds = hoursToMinutes >> minutesToMilliseconds
let daysToMilliseconds = daysToHours >> hoursToMilliseconds
let weeksToMilliseconds = weeksToDays >> daysToMilliseconds
let hoursToSeconds = hoursToMinutes >> minutesToSeconds
let daysToSeconds = daysToHours >> hoursToSeconds
let weeksToSeconds = weeksToDays >> daysToSeconds
let daysToMinutes = daysToHours >> hoursToMinutes
let weeksToMinutes = weeksToDays >> daysToMinutes
let weeksToHours = weeksToDays >> daysToHours
