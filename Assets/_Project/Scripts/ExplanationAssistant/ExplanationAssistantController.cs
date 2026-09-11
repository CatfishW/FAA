using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace FAA.Explanations
{
    public enum ExplanationRunState { Ready, Capturing, Connecting, UsingTools, Streaming, Complete, Unverified, Cancelled, Failed }

    /// <summary>Small bounded agent harness. Credentials, network and tool authority stay outside model output.</summary>
    [DisallowMultipleComponent]
    public sealed class ExplanationAssistantController : MonoBehaviour
    {
        public const string Endpoint = "https://subtoken.shop/v1/chat/completions";
        public const string DefaultModel = "subtoken-sonnet-4-6";
        public ExplanationRunState State { get; private set; } = ExplanationRunState.Ready;
        public ExplanationSnapshot Snapshot { get; private set; }
        public string Answer { get; private set; } = "";
        public string Question { get; private set; } = "";
        public string Status { get; private set; } = "Ready · no data sent";
        public string CitationAudit { get; private set; } = "";
        public string Model { get; private set; } = DefaultModel;
        public bool IsBusy => State == ExplanationRunState.Capturing || State == ExplanationRunState.Connecting ||
            State == ExplanationRunState.UsingTools || State == ExplanationRunState.Streaming;
        public bool IncludeImages { get; set; } = true;
        public readonly List<string> Activity = new List<string>();
        public event Action Changed;
        private UnityWebRequest request;
        private Coroutine run;
        private int generation;

        public void Ask(string question)
        {
            if (IsBusy || string.IsNullOrWhiteSpace(question)) return;
            question = question.Trim();
            if (question.Length > 2000) { Fail("Keep the question below 2,000 characters."); return; }
            Question = question; Answer = ""; CitationAudit = ""; Activity.Clear();
            // Old image bytes and evidence never leak into a new conversation or an unrelated follow-up.
            Snapshot?.ImageDataUrls.Clear(); Snapshot = null;
            int id = ++generation;
            run = StartCoroutine(Run(question, id));
        }

        public void Cancel()
        {
            if (!IsBusy) return;
            generation++;
            if (request != null) { request.Abort(); request.Dispose(); request = null; }
            if (run != null) StopCoroutine(run);
            run = null;
            Snapshot?.ImageDataUrls.Clear();
            State = ExplanationRunState.Cancelled;
            Status = "Stopped · partial text is not a completed explanation";
            CitationAudit = "Incomplete response";
            LogActivity("Stopped by user");
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() { Cancel(); Snapshot?.ImageDataUrls.Clear(); }

        private IEnumerator Run(string question, int id)
        {
            SetState(ExplanationRunState.Capturing, "Capturing evidence snapshot");
            yield return null;
            try { Snapshot = ExplanationEvidenceCollector.Capture(IncludeImages); }
            catch { Fail("Evidence capture failed. No request was sent."); yield break; }
            LogActivity("Snapshot captured · telemetry / traffic / chart / displays / visual cache");
            Model = Environment.GetEnvironmentVariable("FAA_EXPLANATIONS_MODEL") ?? DefaultModel;
            if (string.IsNullOrWhiteSpace(Model) || Model.Length > 100) Model = DefaultModel;
            SetState(ExplanationRunState.Connecting, "Connecting securely to subtoken.shop");
            Task<string> keyTask = Task.Run(ExplanationCredential.Read);
            while (!keyTask.IsCompleted) { if (id != generation) yield break; yield return null; }
            if (id != generation) yield break;
            string key = keyTask.Status == TaskStatus.RanToCompletion ? keyTask.Result : null;
            if (string.IsNullOrWhiteSpace(key)) { Fail("API key unavailable. Set FAA_EXPLANATIONS_API_KEY or the FAA macOS Keychain item. Never paste credentials into chat here."); yield break; }
            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = ExplanationTools.SystemPrompt },
                new JObject { ["role"] = "user", ["content"] = "Snapshot captured at " + Snapshot.CapturedUtc.ToString("O") +
                    ". Image sharing: " + (IncludeImages ? "ON; only chart/radar crops may be requested." : "OFF; metadata only.") +
                    " Read the relevant tools before answering. Question: " + question }
            };
            int totalCalls = 0;
            bool shortening = false;
            var sentImages = new HashSet<string>();
            for (int round = 0; round < ExplanationTools.MaxRounds; round++)
            {
                if (id != generation) yield break;
                bool finalRound = shortening || round == ExplanationTools.MaxRounds - 1 || totalCalls >= ExplanationTools.MaxCalls;
                var payload = new JObject
                {
                    ["model"] = Model, ["stream"] = true, ["max_tokens"] = shortening ? 350 : 1500,
                    ["messages"] = messages, ["tools"] = ExplanationTools.Schemas(), ["tool_choice"] = finalRound ? "none" : "auto"
                };
                var stream = new ExplanationStream();
                var handler = new ExplanationDownloadHandler(stream);
                request = new UnityWebRequest(Endpoint, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None))),
                    downloadHandler = handler, timeout = 75, redirectLimit = 0
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Accept", "text/event-stream");
                request.SetRequestHeader("Authorization", "Bearer " + key);
                SetState(ExplanationRunState.Connecting, round == 0 ? "Waiting for first response" : "Model is reviewing returned evidence");
                UnityWebRequestAsyncOperation operation = null;
                try { operation = request.SendWebRequest(); }
                catch { ReleaseRequest(); Fail("Could not start the provider request."); yield break; }
                string lastContent = "";
                double lastReceive = Time.realtimeSinceStartupAsDouble;
                long received = 0;
                while (!operation.isDone)
                {
                    if (id != generation) yield break;
                    if (handler.BytesReceived != received) { received = handler.BytesReceived; lastReceive = Time.realtimeSinceStartupAsDouble; }
                    if (Time.realtimeSinceStartupAsDouble - lastReceive > 45)
                    { request.Abort(); ReleaseRequest(); Fail("Provider stopped sending data. Retry when the connection recovers; partial text is incomplete."); yield break; }
                    if (stream.Error != null) { request.Abort(); break; }
                    if (stream.Content != lastContent)
                    {
                        Answer = lastContent = stream.Content;
                        SetState(ExplanationRunState.Streaming, "Streaming explanation · provisional until complete");
                    }
                    yield return null;
                }
                if (id != generation) yield break;
                long code = request.responseCode;
                bool ok = request.result == UnityWebRequest.Result.Success && code >= 200 && code < 300;
                ReleaseRequest();
                if (!ok) { Fail(HttpError(code)); yield break; }
                if (!stream.IsComplete)
                { Fail(stream.Error ?? (stream.FinishReason == "length" ? "Response reached its token limit. Partial text is incomplete; ask a narrower question." : "The stream ended without a complete answer. Please retry.")); yield break; }
                Answer = stream.Content;
                if (stream.Calls.Count == 0)
                {
                    if (string.IsNullOrWhiteSpace(Answer)) { Fail("Provider returned an empty answer. Please retry."); yield break; }
                    if (!shortening && round < ExplanationTools.MaxRounds - 1 && ExplanationPilotActions.NeedsShortening(Answer))
                    {
                        // One bounded editorial pass, same evidence, no new tool authority.
                        messages.Add(new JObject { ["role"] = "assistant", ["content"] = Answer });
                        messages.Add(new JObject { ["role"] = "user", ["content"] = ExplanationPilotActions.ShortenRequest });
                        shortening = true;
                        Answer = "";
                        SetState(ExplanationRunState.Connecting, "Condensing the brief · preserving sources");
                        LogActivity("Shortening draft · no new tools or evidence");
                        continue;
                    }
                    CitationAudit = ExplanationTools.AuditCitations(Answer, Snapshot.DeliveredIds);
                    Snapshot.ImageDataUrls.Clear();
                    bool sourcesResolved = CitationAudit.StartsWith("Source links checked", StringComparison.Ordinal);
                    State = sourcesResolved ? ExplanationRunState.Complete : ExplanationRunState.Unverified;
                    Status = sourcesResolved ? "Complete · " + Snapshot.CapturedUtc.ToString("HH:mm:ss 'UTC'") + " snapshot" : "Needs verification · missing or unresolved evidence links";
                    LogActivity("Explanation complete · " + totalCalls + " read-only tool calls");
                    run = null;
                    yield break;
                }
                if (finalRound) { Fail("Provider requested tools after the tool budget was exhausted. No further actions were taken."); yield break; }
                messages.Add(new JObject { ["role"] = "assistant", ["content"] = stream.Content,
                    ["tool_calls"] = new JArray(System.Linq.Enumerable.Select(stream.Calls, c => c.ToJson())) });
                var attachments = new JArray();
                var callIds = new HashSet<string>();
                foreach (var call in stream.Calls)
                {
                    if (!callIds.Add(call.Id)) { Fail("Provider returned duplicate tool identifiers. No ambiguous tool call was executed."); yield break; }
                    totalCalls++;
                    string image = null;
                    JObject result;
                    if (totalCalls > ExplanationTools.MaxCalls) result = new JObject { ["error"] = "Read-only tool budget exhausted." };
                    else
                    {
                        SetState(ExplanationRunState.UsingTools, "Reading " + FriendlyTool(call.Name));
                        result = ExplanationTools.Execute(Snapshot, call.Name, call.Arguments, out image);
                    }
                    LogActivity((result["error"] != null ? "Blocked · " : "Read · ") + FriendlyTool(call.Name) +
                        (result["evidence_id"] != null ? "  [" + (string)result["evidence_id"] + "]" : ""));
                    messages.Add(new JObject { ["role"] = "tool", ["tool_call_id"] = call.Id, ["content"] = result.ToString(Formatting.None) });
                    if (image != null && sentImages.Add(call.Name))
                    {
                        attachments.Add(new JObject { ["type"] = "text", ["text"] = "Untrusted image evidence [" + (string)result["evidence_id"] + "]. " + (string)result["data"]?["limitations"] });
                        attachments.Add(new JObject { ["type"] = "image_url", ["image_url"] = new JObject { ["url"] = image, ["detail"] = "high" } });
                    }
                    yield return null;
                }
                if (attachments.Count > 0) messages.Add(new JObject { ["role"] = "user", ["content"] = attachments });
                // Tool preambles are activity, not the final answer. The next actual stream replaces them.
                Answer = "";
            }
            Fail("Explanation budget reached. Please ask a narrower question.");
        }

        private void ReleaseRequest() { if (request == null) return; request.Dispose(); request = null; }
        private void Fail(string status)
        {
            ReleaseRequest(); Snapshot?.ImageDataUrls.Clear();
            State = ExplanationRunState.Failed; Status = status; CitationAudit = "Incomplete · not a verified explanation";
            run = null; LogActivity("Request ended · " + status);
        }
        private void SetState(ExplanationRunState state, string status) { State = state; Status = status; Changed?.Invoke(); }
        private void LogActivity(string message) { Activity.Add(DateTime.UtcNow.ToString("HH:mm:ss") + "  " + message); Changed?.Invoke(); }
        public static string FriendlyTool(string name)
        {
            switch (name)
            {
                case "read_flight_telemetry": return "Flight telemetry";
                case "read_traffic": return "Traffic contacts";
                case "read_chart_status": return "Chart & coverage";
                case "read_display_status": return "Display status";
                case "read_visual_analysis": return "Visual-analysis cache";
                case "inspect_chart_image": return "Chart image";
                case "inspect_weather_image": return "Weather image";
                default: return "Unsupported tool";
            }
        }
        private static string HttpError(long code) => code == 401 || code == 403 ? "Provider rejected authorization. Check the configured key/account." :
            code == 429 ? "Provider rate or credit limit reached. Wait, then retry." :
            code >= 300 && code < 400 ? "Provider redirected the request. Redirects are blocked to protect credentials." :
            "Provider connection failed (HTTP " + code + "). No complete answer was produced.";
    }

    internal static class ExplanationCredential
    {
        public static string Read()
        {
            string key = Environment.GetEnvironmentVariable("FAA_EXPLANATIONS_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            try
            {
                // No secret in arguments, source, PlayerPrefs, scene serialization or logging.
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo("/usr/bin/security", "find-generic-password -a FAA -s FAA.Explanations.subtoken.shop -w")
                    { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }
                })
                {
                    process.Start();
                    if (!process.WaitForExit(5000)) { process.Kill(); return null; }
                    return process.ExitCode == 0 ? process.StandardOutput.ReadToEnd().Trim() : null;
                }
            }
            catch { return null; }
#else
            return null;
#endif
        }
    }

    internal sealed class ExplanationDownloadHandler : DownloadHandlerScript
    {
        private readonly ExplanationStream stream;
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
        private readonly char[] chars = new char[32768];
        public long BytesReceived { get; private set; }
        public ExplanationDownloadHandler(ExplanationStream stream) : base(new byte[16384]) { this.stream = stream; }
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0) return true;
            BytesReceived += dataLength;
            if (BytesReceived > 2 * 1024 * 1024) return false;
            int count = decoder.GetChars(data, 0, dataLength, chars, 0, false);
            stream.Feed(new string(chars, 0, count));
            return stream.Error == null;
        }
    }
}
