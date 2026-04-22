using System.Collections.Generic;

namespace FormulaSim.Audio
{
    public static class CommentaryLines
    {
        static readonly Dictionary<string, string[]> Lines = new()
        {
            ["OVERTAKE"] = new[]
            {
                "{driver} goes around the outside — BRILLIANT! That's a pass!",
                "AND {driver} IS THROUGH! What a move at {corner}!",
                "Late on the brakes — {driver} dives to the inside and makes it stick!",
                "The DRS is open and {driver} sweeps past — that looked effortless.",
            },
            ["FASTEST_LAP"] = new[]
            {
                "FASTEST LAP! {driver} just went {lap_time} — purple!",
                "{driver} with a stunning lap — fastest of the race so far!",
                "Purple! Absolute purple for {driver} — {lap_time}!",
                "The bonus point is {driver}'s — fastest lap at {circuit}!",
            },
            ["LAP_COMPLETE"] = new[]
            {
                "That's lap {lap} of {total} completed.",
                "Into lap {lap}. {driver} looking comfortable out there.",
                "Lap {lap} done. The gap at the front is {gap} seconds.",
            },
            ["WEATHER_RAIN_START"] = new[]
            {
                "HERE COMES THE RAIN! This is going to change everything!",
                "The heavens have opened at {circuit} — strategy goes out the window!",
                "Rain! Proper rain now — the slick runners are in serious trouble.",
            },
            ["WEATHER_HEAVY"] = new[]
            {
                "This is treacherous out there — HEAVY rain at {circuit}.",
                "Visibility is down to almost nothing.",
                "Full wets mandatory — slicks would be suicidal.",
            },
            ["WEATHER_DRYING"] = new[]
            {
                "The rain is easing off. Could we see a switch to slicks soon?",
                "A dry line is appearing. Who's going to blink first on strategy?",
                "The track is starting to dry... the question is — when do you pit?",
            },
            ["WEATHER_DRIZZLE"] = new[]
            {
                "There's a spot of drizzle — nothing serious yet.",
                "Just a few drops on the visor. Conditions are changing.",
            },
            ["AQUAPLANING"] = new[]
            {
                "AQUAPLANING! {driver} has lost it momentarily — back in control, JUST!",
                "{driver} goes sideways through the puddle — phenomenal car control!",
                "That was scary — aquaplaning for {driver} through the high-speed section!",
            },
            ["PIT_IN"] = new[]
            {
                "{driver} peeling into the pits — let's see what stop time they get.",
                "Box box box — {driver} is in! The crew are ready.",
                "{driver} makes for the pit lane. Strategy call from the wall.",
            },
            ["PIT_FAST"] = new[]
            {
                "STUNNING! {stop_time} seconds — one of the fastest stops you'll ever see!",
                "{stop_time}s — near perfection from the {team} crew!",
                "World-class pit stop — {driver} fires back out!",
            },
            ["PIT_SLOW"] = new[]
            {
                "A problem for {driver} — struggled with that stop. {stop_time} seconds.",
                "Cross-thread nut? Whatever it was, that cost {driver} several seconds.",
            },
            ["CRASH_HEAVY"] = new[]
            {
                "BIG ACCIDENT! {driver} has hit the barrier HARD at {corner}!",
                "MASSIVE shunt — that car is NOT going to run again.",
                "Oh my — {driver} has gone into the wall. Safety car WILL be deployed.",
            },
            ["CRASH_LIGHT"] = new[]
            {
                "{driver} has clipped the barrier — carries on but will check for damage.",
                "Light contact with the wall for {driver}.",
            },
            ["SAFETY_CAR"] = new[]
            {
                "SAFETY CAR DEPLOYED. The field is bunching up.",
                "Safety car out on track — this resets the race.",
            },
            ["SAFETY_CAR_END"] = new[]
            {
                "Safety car is IN this lap — we're going racing again!",
                "GREEN GREEN GREEN! The safety car is in, stand by for a SPRINT to the line!",
            },
            ["TIRES_DEGRADING"] = new[]
            {
                "{driver} reporting the tyres going off over the radio.",
                "You can see the sliding — {driver} is struggling for rear grip.",
                "The {compound} compound on {driver}'s car is coming to the end of its life.",
            },
            ["WIN"] = new[]
            {
                "{driver} WINS! {driver} WINS THE {circuit} GRAND PRIX!",
                "FIRST PLACE! {driver} crosses the line — what a drive!",
                "VICTORY for {driver} and {team}! Absolutely sensational!",
            },
            ["CHAMPIONSHIP_FIGHT"] = new[]
            {
                "Only {gap} points separate the title contenders. This is going to the wire!",
                "The championship is alive! {driver} cuts the gap to {gap} points.",
            },
            ["FILLER"] = new[]
            {
                "A fascinating tactical race developing here at {circuit}.",
                "The strategy will define this race — lots still to play for.",
                "{driver} keeping it clean and consistent through the mid-field.",
                "Tyre conservation key in the middle phase of this grand prix.",
                "The engineers are crunching numbers on that pit wall.",
            },
        };

        public static string[] Get(string key)
            => Lines.TryGetValue(key, out var pool) ? pool : null;
    }
}
