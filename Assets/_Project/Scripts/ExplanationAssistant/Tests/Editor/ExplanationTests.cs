using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FAA.Explanations.Tests
{
    public sealed class ExplanationTests
    {
        private static string Chunk(JObject delta, string finish = null) => "data: " + new JObject
        {
            ["choices"] = new JArray(new JObject { ["index"] = 0, ["delta"] = delta,
                ["finish_reason"] = finish == null ? JValue.CreateNull() : (JToken)finish })
        }.ToString(Newtonsoft.Json.Formatting.None) + "\r\n\r\n";

        [TestCase(1)] [TestCase(2)] [TestCase(7)] [TestCase(47)] [TestCase(4096)]
        public void Stream_ReassemblesArbitrarySseBoundaries(int width)
        {
            var parser = new ExplanationStream();
            string text = ": keepalive\r\n\r\n" + Chunk(new JObject { ["content"] = "Altitude +500 ft ↑ [E1]" }) + Chunk(new JObject(), "stop") + "data: [DONE]\r\n\r\n";
            for (int i = 0; i < text.Length; i += width) parser.Feed(text.Substring(i, Math.Min(width, text.Length - i)));
            Assert.That(parser.IsComplete, Is.True, parser.Error);
            Assert.That(parser.Content, Is.EqualTo("Altitude +500 ft ↑ [E1]"));
        }

        [Test]
        public void Utf8Decoder_PreservesSplitMultibyteSymbols()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("↑ traffic · 雨");
            var decoder = Encoding.UTF8.GetDecoder(); var result = new StringBuilder(); var chars = new char[4];
            foreach (byte value in bytes) result.Append(chars, 0, decoder.GetChars(new[] { value }, 0, 1, chars, 0));
            Assert.That(result.ToString(), Is.EqualTo("↑ traffic · 雨"));
        }

        [Test]
        public void Stream_AssemblesMultipleFragmentedToolCallsByIndex()
        {
            var parser = new ExplanationStream();
            parser.Feed(Chunk(new JObject { ["tool_calls"] = new JArray(
                new JObject { ["index"] = 1, ["id"] = "b", ["function"] = new JObject { ["name"] = "read_display_status", ["arguments"] = "{" } },
                new JObject { ["index"] = 0, ["id"] = "a", ["function"] = new JObject { ["name"] = "read_traffic", ["arguments"] = "{" } }) }));
            parser.Feed(Chunk(new JObject { ["tool_calls"] = new JArray(
                new JObject { ["index"] = 0, ["function"] = new JObject { ["arguments"] = "}" } },
                new JObject { ["index"] = 1, ["function"] = new JObject { ["arguments"] = "\"reason\":\"Check status\"}" } }) }, "tool_calls"));
            parser.Feed("data: [DONE]\n\n");
            Assert.That(parser.IsComplete, Is.True);
            Assert.That(parser.Calls.Select(c => c.Id), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(parser.Calls[0].Arguments, Is.EqualTo("{}"));
            Assert.That(ExplanationTools.Validate(parser.Calls[1].Name, parser.Calls[1].Arguments, out _), Is.True);
        }

        [TestCase("stop", false)] [TestCase("length", true)] [TestCase("content_filter", true)] [TestCase(null, true)]
        public void Stream_TruncationAndNonSuccessFinishesAreNotComplete(string finish, bool done)
        {
            var parser = new ExplanationStream(); parser.Feed(Chunk(new JObject { ["content"] = "Partial text" }, finish));
            if (done) parser.Feed("data: [DONE]\n\n");
            Assert.That(parser.IsComplete, Is.False);
        }

        [Test]
        public void Stream_ProviderErrorCannotBeMistakenForACompletedAnswer()
        {
            var parser = new ExplanationStream(); parser.Feed(Chunk(new JObject { ["content"] = "Partial" }, "stop"));
            parser.Feed("data: {\"error\":{\"message\":\"sensitive provider diagnostics\"}}\n\ndata: [DONE]\n\n");
            Assert.That(parser.IsComplete, Is.False); Assert.That(parser.Error, Does.Not.Contain("sensitive"));
        }

        [Test]
        public void Stream_RejectsMalformedAndOversizedPayloads()
        {
            var malformed = new ExplanationStream(); malformed.Feed("data: {broken}\n\n"); Assert.That(malformed.Error, Is.Not.Null);
            var oversized = new ExplanationStream(); oversized.Feed(new string('x', 1024 * 1024 + 1)); Assert.That(oversized.Error, Is.Not.Null);
            var tools = new ExplanationStream(); tools.Feed(Chunk(new JObject { ["tool_calls"] = new JArray(new JObject { ["index"] = 8 }) })); Assert.That(tools.Error, Is.Not.Null);
        }

        [TestCase("exec", "{\"command\":\"whoami\"}")]
        [TestCase("set_altitude", "{}")]
        [TestCase("read_chart_status", "{\"url\":\"https://example.com\"}")]
        [TestCase("inspect_chart_image", "{\"path\":\"/private/file\"}")]
        [TestCase("read_traffic", "[]")]
        [TestCase("read_traffic", "{\"reason\": 3}")]
        [TestCase("read_traffic", "{\"_\":\"command\"}")]
        [TestCase("read_traffic", "{\"_\":{\"path\":\"/file\"}}")]
        [TestCase("read_traffic", "invalid")]
        public void ToolAllowlist_RejectsExpansionAndMalformedArguments(string name, string args)
        { Assert.That(ExplanationTools.Validate(name, args, out var error), Is.False); Assert.That(error, Is.Not.Empty); }

        [Test]
        public void ToolAllowlist_AllSevenToolsAcceptOnlyOptionalShortReason()
        {
            Assert.That(ExplanationTools.Descriptions.Count, Is.EqualTo(7));
            foreach (string name in ExplanationTools.Descriptions.Keys)
            {
                Assert.That(ExplanationTools.Validate(name, "{}", out _), Is.True);
                Assert.That(ExplanationTools.Validate(name, "{\"reason\":\"Explain this source\"}", out _), Is.True);
                Assert.That(ExplanationTools.Validate(name, "{\"_\":true,\"reason\":\"Read snapshot\"}", out _), Is.True);
                Assert.That(ExplanationTools.Validate(name, new JObject { ["reason"] = new string('x', 201) }.ToString(), out _), Is.False);
            }
        }

        [Test]
        public void Evidence_DeliversOnlyRequestedSourcesAndReturnsClones()
        {
            var snapshot = new ExplanationSnapshot(); snapshot.Add("read_traffic", "Traffic", "test source", "STALE", new JObject { ["count"] = 2 }, 8);
            var result = ExplanationTools.Execute(snapshot, "read_traffic", "{}", out string image);
            Assert.That(image, Is.Null); Assert.That(snapshot.DeliveredIds, Is.EquivalentTo(new[] { "E1" }));
            Assert.That((double)result["source_age_seconds_at_capture"], Is.EqualTo(8));
            result["data"]["count"] = 999;
            Assert.That((int)snapshot.Records["read_traffic"].Data["count"], Is.EqualTo(2));
        }

        [Test]
        public void Evidence_MissingSourcesReturnUnavailableNotSyntheticData()
        {
            var snapshot = new ExplanationSnapshot();
            var result = ExplanationTools.Execute(snapshot, "read_flight_telemetry", "{}", out _);
            Assert.That(result["error"], Is.Not.Null); Assert.That(snapshot.DeliveredIds, Is.Empty);
        }

        [Test]
        public void ImageSharingOff_PreventsImageAndEvidenceTransmission()
        {
            var snapshot = new ExplanationSnapshot { ImagesAllowed = false };
            snapshot.Add("inspect_chart_image", "Image", "test", "READY", new JObject()); snapshot.ImageDataUrls["inspect_chart_image"] = "data:image/jpeg;base64,test";
            var result = ExplanationTools.Execute(snapshot, "inspect_chart_image", "{}", out string image);
            Assert.That(image, Is.Null); Assert.That(result["error"], Is.Not.Null); Assert.That(snapshot.DeliveredIds, Is.Empty);
            snapshot.ImagesAllowed = true; ExplanationTools.Execute(snapshot, "inspect_chart_image", "{}", out image);
            Assert.That(image, Is.Not.Null); Assert.That(snapshot.DeliveredIds, Does.Contain("E1"));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
        public void NonfiniteTelemetry_StaysNullNotZero(double value)
        { Assert.That(ExplanationEvidence.Finite(value).Type, Is.EqualTo(JTokenType.Null)); }

        [TestCase("IAS 80 kt [E1]", "Source links checked")]
        [TestCase("IAS 80 kt [E9]", "Unresolved")]
        [TestCase("It is safe.", "Uncited")]
        [TestCase("IAS [E1] chart [E2]", "Unresolved")]
        public void CitationAudit_DistinguishesLinksFromUnverifiedClaims(string answer, string prefix)
        { Assert.That(ExplanationTools.AuditCitations(answer, new[] { "E1" }), Does.StartWith(prefix)); }

        [Test]
        public void PilotActions_AreFourDeliberateShortReadOnlyBriefs()
        {
            Assert.That(ExplanationPilotActions.Names, Is.EqualTo(new[] { "Traffic", "Weather", "Chart", "Status" }));
            foreach (string action in ExplanationPilotActions.Names)
            {
                Assert.That(ExplanationPilotActions.TryGetPrompt(action, out string prompt), Is.True);
                Assert.That(prompt, Does.Contain("three short bullets"));
                Assert.That(prompt, Does.Contain("at most 60 words"));
                Assert.That(prompt, Does.Contain("source citations"));
                Assert.That(prompt, Does.Contain("No introduction"));
                Assert.That(prompt, Does.Contain("flight-control advice"));
                Assert.That(prompt.Length, Is.LessThan(2000));
            }
        }

        [TestCase(null)] [TestCase("")] [TestCase("Land")] [TestCase("Execute")]
        public void PilotActions_UnknownActionsCannotStartRequests(string action)
        {
            Assert.That(ExplanationPilotActions.TryGetPrompt(action, out string prompt), Is.False);
            Assert.That(prompt, Is.Null);
        }

        [Test]
        public void PilotBriefPolicy_DoesNotRequestLongEssays()
        {
            Assert.That(ExplanationTools.SystemPrompt, Does.Contain("at most 60 words"));
            Assert.That(ExplanationTools.SystemPrompt, Does.Not.Contain("150-250"));
            Assert.That(ExplanationTools.SystemPrompt, Does.Contain("main uncertainty last"));
        }

        [Test]
        public void PilotBrief_ExplainsMeaningNotDiagnosticsOrUnverifiedOperationalNumbers()
        {
            ExplanationPilotActions.TryGetPrompt("Chart", out string chart);
            Assert.That(chart, Does.Contain("Do not transcribe or decode frequencies"));
            Assert.That(chart, Does.Contain("Do not claim a feature is ahead"));
            Assert.That(chart, Does.Contain("'Picture:'"));
            Assert.That(chart, Does.Contain("'Context:'"));
            Assert.That(chart, Does.Contain("'Limit:'"));
            Assert.That(chart, Does.Contain("Do not recite tile counts"));
            ExplanationPilotActions.TryGetPrompt("Weather", out string weather);
            Assert.That(weather, Does.Contain("Rain is not proof of turbulence"));
        }

        [Test]
        public void LongDraftGetsOneBoundedShorteningRequestWithoutLosingUncertainty()
        {
            Assert.That(ExplanationPilotActions.NeedsShortening(string.Join(" ", Enumerable.Repeat("word", 71))), Is.True);
            Assert.That(ExplanationPilotActions.NeedsShortening(string.Join(" ", Enumerable.Repeat("word", 60))), Is.False);
            Assert.That(ExplanationPilotActions.NeedsShortening(null), Is.False);
            Assert.That(ExplanationPilotActions.ShortenRequest, Does.Contain("source citations"));
            Assert.That(ExplanationPilotActions.ShortenRequest, Does.Contain("Do not add claims or turn uncertainty into certainty"));
        }

        [TestCase(1920f, 1080f, 560f, 20f, 800f, 800f)]
        [TestCase(1600f, 900f, 430f, 20f, 650f, 650f)]
        [TestCase(2560f, 1080f, 880f, 20f, 800f, 800f)]
        public void BriefPlacement_KeepsFullMapAndControlsClear(float width, float height, float x, float y, float w, float h)
        {
            var blockers = new[]
            {
                new BriefPlacement.Box(x, y, w, h),
                new BriefPlacement.Box(18, 100, Math.Max(0, x - 40), 450)
            };
            bool found = BriefPlacement.TryPlace(width, height, 440, 286, true, blockers, out var position);
            Assert.That(found, Is.True);
            foreach (var box in blockers) Assert.That(position.Overlaps(box), Is.False);
            Assert.That(position.X, Is.GreaterThan(x + w));
        }

        [Test]
        public void BriefPlacement_HudDockStaysBelowInstruments()
        {
            var obstacles = new[] { new BriefPlacement.Box(480, 324, 960, 756), new BriefPlacement.Box(20, 20, 400, 400),
                new BriefPlacement.Box(1500, 20, 400, 400) };
            Assert.That(BriefPlacement.TryPlace(1920, 1080, 520, 286, false, obstacles, out var rect), Is.True);
            Assert.That(rect.X, Is.EqualTo(700));
            Assert.That(rect.Top, Is.EqualTo(304));
        }

        [Test]
        public void BriefPlacement_NoRoomNeverChoosesAnOverlap()
        {
            var obstacles = new[] { new BriefPlacement.Box(0, 0, 900, 700) };
            Assert.That(BriefPlacement.TryPlace(900, 700, 440, 286, true, obstacles, out _), Is.False);
            Assert.That(BriefPlacement.TryPlace(900, 700, 145, 34, true, obstacles, out _), Is.False);
        }

        [Test]
        public void Policy_PreservesCriticalDataAndOperationalBoundaries()
        {
            Assert.That(ExplanationTools.SystemPrompt, Does.Contain("untrusted DATA"));
            Assert.That(ExplanationTools.SystemPrompt, Does.Contain("not a flight director"));
            Assert.That(ExplanationTools.SystemPrompt, Does.Contain("not its edition/effective date"));
            Assert.That(ExplanationTools.SystemPrompt, Does.Contain("spatially synthesized"));
            Assert.That(ExplanationTools.MaxCalls, Is.EqualTo(8)); Assert.That(ExplanationTools.MaxRounds, Is.EqualTo(4));
        }
    }
}
