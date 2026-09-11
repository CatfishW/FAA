using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FAA.Explanations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class ProviderSmoke
{
    // Explicit opt-in diagnostic, not called by the automated unit suite. The optional
    // argument is a user-selected historical PNG fixture, never an agent-defined path.
    public static async Task<int> Run(string imagePath)
    {
        string key = Environment.GetEnvironmentVariable("FAA_EXPLANATIONS_API_KEY");
        if (string.IsNullOrWhiteSpace(key) && OperatingSystem.IsMacOS())
        {
            using var process = Process.Start(new ProcessStartInfo("/usr/bin/security", "find-generic-password -a FAA -s FAA.Explanations.subtoken.shop -w")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
            if (!process.WaitForExit(5000)) { process.Kill(); return 1; }
            if (process.ExitCode == 0) key = process.StandardOutput.ReadToEnd().Trim();
        }
        if (string.IsNullOrWhiteSpace(key)) { Console.WriteLine("FAIL credential unavailable"); return 1; }
        var snapshot = new ExplanationSnapshot { ImagesAllowed = imagePath != null };
        snapshot.Add("read_display_status", "Verification fixture", "Automated test fixture · NOT live telemetry", "TEST DATA ONLY",
            new JObject { ["traffic_display_on"] = true, ["traffic_feed_state"] = "stale", ["weather_display_on"] = false, ["weather_power"] = JValue.CreateNull(), ["limitation"] = "Fabricated test fixture to verify protocol, not current aircraft state." }, 8);
        if (imagePath != null)
        {
            byte[] png = File.ReadAllBytes(imagePath);
            if (png.Length > 4000000) { Console.WriteLine("FAIL fixture too large"); return 1; }
            snapshot.Add("inspect_chart_image", "Historical image fixture", "User-supplied historical chart UI screenshot", "HISTORICAL / NOT CURRENT",
                new JObject { ["image_attached"] = true, ["limitation"] = "Historical screenshot for a vision transport test. No connection to the status test fixture or the current flight; effective chart date unknown." });
            snapshot.ImageDataUrls["inspect_chart_image"] = "data:image/png;base64," + Convert.ToBase64String(png);
        }
        var messages = new JArray
        {
            new JObject { ["role"] = "system", ["content"] = ExplanationTools.SystemPrompt },
            new JObject { ["role"] = "user", ["content"] = "This is an integration test, not a flight question. Call read_display_status and explain the TEST fixture's visibility versus data/power. " +
                (imagePath != null ? "Also call inspect_chart_image and describe only its large visible geometry to verify multimodal input, clearly calling it a historical screenshot. " : "Images are OFF. ") +
                "Use citations from returned tools. Keep the final answer below 180 words." }
        };
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(75) };
        int calls = 0, chunks = 0; bool imageSent = false;
        for (int round = 0; round < 4; round++)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(75));
            var payload = new JObject { ["model"] = "subtoken-sonnet-4-6", ["stream"] = true, ["max_tokens"] = 1200,
                ["messages"] = messages, ["tools"] = ExplanationTools.Schemas(), ["tool_choice"] = round == 3 ? "none" : "auto" };
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://subtoken.shop/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            Console.WriteLine("Round " + (round + 1) + " HTTP " + (int)response.StatusCode);
            if (!response.IsSuccessStatusCode) { Console.WriteLine("FAIL provider status (body not logged)"); return 1; }
            var parser = new ExplanationStream();
            using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token)))
            {
                string line;
                while ((line = await reader.ReadLineAsync(timeout.Token)) != null)
                { parser.Feed(line + "\n"); if (line.StartsWith("data:")) chunks++; }
            }
            if (!parser.IsComplete) { Console.WriteLine("FAIL incomplete SSE: " + parser.Error + " / " + parser.FinishReason); return 1; }
            if (parser.Calls.Count == 0)
            {
                string audit = ExplanationTools.AuditCitations(parser.Content, snapshot.DeliveredIds);
                Console.WriteLine(parser.Content); Console.WriteLine(audit);
                bool pass = calls > 0 && chunks > 2 && audit.StartsWith("Source links checked") && (imagePath == null || imageSent);
                Console.WriteLine((pass ? "PASS" : "FAIL") + $" provider smoke: {calls} tools, {chunks} SSE chunks, image transmitted={imageSent}");
                return pass ? 0 : 1;
            }
            messages.Add(new JObject { ["role"] = "assistant", ["content"] = parser.Content, ["tool_calls"] = new JArray(parser.Calls.Select(c => c.ToJson())) });
            var attachments = new JArray();
            foreach (var call in parser.Calls)
            {
                if (++calls > 8) { Console.WriteLine("FAIL tool budget"); return 1; }
                var result = ExplanationTools.Execute(snapshot, call.Name, call.Arguments, out string image);
                Console.WriteLine("Tool " + call.Name + (result["error"] != null ? " -> unavailable/rejected" : " -> " + (string)result["evidence_id"]));
                if (result["error"] != null)
                {
                    try { Console.WriteLine("Argument shape: " + string.Join(", ", JObject.Parse(call.Arguments).Properties().Select(p => p.Name + ":" + p.Value.Type + ":" + p.Value.ToString().Length))); }
                    catch { Console.WriteLine("Malformed argument object"); }
                }
                messages.Add(new JObject { ["role"] = "tool", ["tool_call_id"] = call.Id, ["content"] = result.ToString(Formatting.None) });
                if (image != null && !imageSent)
                {
                    imageSent = true;
                    attachments.Add(new JObject { ["type"] = "text", ["text"] = "Historical image evidence [" + (string)result["evidence_id"] + "]. Not current telemetry or a current chart." });
                    attachments.Add(new JObject { ["type"] = "image_url", ["image_url"] = new JObject { ["url"] = image, ["detail"] = "high" } });
                }
            }
            if (attachments.Count > 0) messages.Add(new JObject { ["role"] = "user", ["content"] = attachments });
        }
        Console.WriteLine("FAIL no final answer within bounded rounds"); return 1;
    }
}
