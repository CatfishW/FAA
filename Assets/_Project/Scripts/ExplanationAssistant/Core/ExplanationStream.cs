using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FAA.Explanations
{
    public sealed class ExplanationToolCall
    {
        public string Id = "";
        public string Name = "";
        public string Arguments = "";
        public JObject ToJson() => new JObject
        {
            ["id"] = Id, ["type"] = "function",
            ["function"] = new JObject { ["name"] = Name, ["arguments"] = Arguments }
        };
    }

    /// <summary>Bounded SSE framing + fragmented tool-call assembly. Never treats a truncated stream as success.</summary>
    public sealed class ExplanationStream
    {
        private readonly StringBuilder pending = new StringBuilder();
        private readonly StringBuilder eventData = new StringBuilder();
        private readonly StringBuilder content = new StringBuilder();
        private readonly SortedDictionary<int, ExplanationToolCall> calls = new SortedDictionary<int, ExplanationToolCall>();
        private int received;
        public string Content => content.ToString();
        public IReadOnlyList<ExplanationToolCall> Calls => calls.Values.ToList();
        public string FinishReason { get; private set; }
        public string Error { get; private set; }
        public bool SawDone { get; private set; }
        public bool IsComplete => Error == null && SawDone &&
            (FinishReason == "stop" || FinishReason == "tool_calls") &&
            (calls.Count == 0 || calls.Values.All(c => c.Id.Length > 0 && c.Name.Length > 0));

        public void Feed(string text)
        {
            if (Error != null || string.IsNullOrEmpty(text)) return;
            received += text.Length;
            if (received > 1024 * 1024) { Error = "Response exceeded the stream limit."; return; }
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    string line = pending.ToString().TrimEnd('\r'); pending.Clear();
                    if (line.Length == 0) Dispatch();
                    else if (line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        if (eventData.Length > 0) eventData.Append('\n');
                        eventData.Append(line.Substring(5).TrimStart(' '));
                    }
                }
                else pending.Append(c);
            }
        }

        private void Dispatch()
        {
            string data = eventData.ToString(); eventData.Clear();
            if (data.Length == 0 || Error != null) return;
            if (data == "[DONE]") { SawDone = true; return; }
            try
            {
                var chunk = JObject.Parse(data);
                if (chunk["error"] != null) { Error = "The model provider reported a stream error."; return; }
                var choice = (chunk["choices"] as JArray)?.FirstOrDefault(c => (int?)c["index"] == 0);
                if (choice == null) return; // Usage-only or keepalive chunk.
                var delta = choice["delta"];
                if (delta?["content"]?.Type == JTokenType.String) content.Append((string)delta["content"]);
                if (content.Length > 48000) { Error = "Answer exceeded the display limit."; return; }
                if (choice["finish_reason"]?.Type == JTokenType.String) FinishReason = (string)choice["finish_reason"];
                if (!(delta?["tool_calls"] is JArray fragments)) return;
                foreach (var fragment in fragments)
                {
                    int index = (int?)fragment["index"] ?? -1;
                    if (index < 0 || index >= ExplanationTools.MaxCalls) { Error = "Tool-call limit exceeded."; return; }
                    if (!calls.TryGetValue(index, out var call)) { call = new ExplanationToolCall(); calls.Add(index, call); }
                    string id = (string)fragment["id"];
                    if (!string.IsNullOrEmpty(id) && call.Id != id) call.Id += id;
                    call.Name += (string)fragment["function"]?["name"] ?? "";
                    call.Arguments += (string)fragment["function"]?["arguments"] ?? "";
                    if (call.Id.Length > 256 || call.Name.Length > 100 || call.Arguments.Length > 4096)
                    { Error = "Tool-call payload exceeded its limit."; return; }
                }
            }
            catch { Error = "Could not decode the provider's stream. No completed answer is available."; }
        }
    }
}
