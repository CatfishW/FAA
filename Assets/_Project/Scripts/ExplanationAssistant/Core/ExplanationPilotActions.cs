using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FAA.Explanations
{
    /// <summary>Deliberate one-tap requests. Opening the UI never sends a request.</summary>
    public static class ExplanationPilotActions
    {
        public static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[] { "Traffic", "Weather", "Chart", "Status" });
        private const string Brief = " Give exactly three short bullets, at most 60 words total. " +
            "Start the bullets with 'Picture:', 'Context:', and 'Limit:' respectively. " +
            "Lead with the most relevant observation, put the main uncertainty last, and include source citations. " +
            "Use plain pilot-facing units and language. No introduction, repeated question, or flight-control advice. " +
            "Do not recite tile counts, zoom levels, raw coordinates, opacity percentages, JSON field names or null values. " +
            "Say 'not available' or 'not verified' instead. Mention a setting only when it explains an actual display problem.";
        public static bool NeedsShortening(string answer) =>
            Regex.Matches(answer ?? "", @"\S+").Count > 70;

        public const string ShortenRequest = "The draft is too long for a pilot brief. Rewrite it as exactly three bullets " +
            "labeled Picture, Context, Limit, with at most 60 words TOTAL. Preserve the most relevant supported observation, " +
            "the main uncertainty and source citations. Remove secondary details, repeated numbers and software diagnostics. " +
            "Do not add claims or turn uncertainty into certainty. Return only the rewritten brief.";

        public static bool TryGetPrompt(string action, out string prompt)
        {
            switch (action)
            {
                case "Traffic":
                    prompt = "Read traffic and flight telemetry. Picture: identify up to two nearest relevant contacts, " +
                        "bearing relative to the nose when explicitly available (otherwise true bearing), range NM, and feet above/below ownship. " +
                        "Context: add vertical trend and reported aircraft type only when available and useful. Do not infer closing speed, conflict, a maneuver or safe separation. " +
                        "Limit: give target/feed age or filtering limitations; never equate no contacts with clear airspace." + Brief;
                    return true;
                case "Weather":
                    prompt = "Read display status and flight telemetry. Picture: prioritize simulator precipitation at ownship and visibility in SM if available; " +
                        "Context: wind in knots and turbulence availability or a mode/power/feed issue that explains the picture. " +
                        "Inspect the weather image only if enabled and needed to explain the picture. Limit: distinguish illustrative SIM WX returns from measured weather. " +
                        "Rain is not proof of turbulence; absent samples are not zero turbulence. No weather avoidance or route guidance." + Brief;
                    return true;
                case "Chart":
                    prompt = "Read chart status and flight telemetry for ownship context. Inspect the chart image only if image sharing is enabled. " +
                        "Picture: identify at most two prominent readable chart features useful for orientation (named airport, airspace label, terrain or landmark). " +
                        "Context: explain the map orientation/range or coverage issue, not software diagnostics. The image is north-up but the display may be track-up. " +
                        "Do not claim a feature is ahead, beneath, nearby or intersects the aircraft from the crop alone. " +
                        "Do not infer airspace class from a colored region, a ring or proximity to an airport; only report clearly readable explicit labels as unverified. " +
                        "Do not transcribe or decode frequencies, airspace vertical limits, obstacle heights or clearance requirements from this unverified raster. " +
                        "Limit: state that chart currency and image interpretation need verification; if unreadable, say so instead of guessing." + Brief;
                    return true;
                case "Status":
                    prompt = "Read display status. Picture: report weather/traffic visibility and feed health in plain language. " +
                        "Context: explain why markers or returns are absent using actual switches, mode, declutter or freshness evidence. " +
                        "Mention an exact existing display control only if the evidence establishes its effect, never invent an action or automatically change settings. " +
                        "Limit: distinguish local display ON/OFF from known or unknown system power and unmeasured turbulence." + Brief;
                    return true;
                default:
                    prompt = null;
                    return false;
            }
        }
    }
}
