// Execute NUnit assertion methods in the existing Editor, outside Play mode.
// This is a focused direct-assertion run, not a Unity Test Runner report.
if (UnityEditor.EditorApplication.isPlaying) return "Stop Play mode before running edit-time fixtures.";
var reports = new System.Collections.Generic.List<object>();
foreach (string suite in new[] { "FaaViewAlignmentTests", "FaaScreenCueTests", "FaaCueStabilityTests", "FaaTrafficTypeMetadataTests", "FaaTurbulenceModeTests" })
{
    var type = System.Type.GetType("FAA.Customization.Tests." + suite + ", FAA.Customization.EditorTests", true);
    int passed = 0;
    var failed = new System.Collections.Generic.List<string>();
    foreach (var method in type.GetMethods())
    {
        var attrs = method.GetCustomAttributes(false);
        var cases = attrs.Where(a => a.GetType().Name == "TestCaseAttribute")
            .Select(a => (object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToArray();
        if (cases.Length == 0 && attrs.Any(a => a.GetType().Name == "TestAttribute"))
            cases = new[] { System.Array.Empty<object>() };
        foreach (var arguments in cases)
        {
            var fixture = System.Activator.CreateInstance(type);
            try { method.Invoke(fixture, arguments); passed++; }
            catch (System.Exception exception) { failed.Add(method.Name + ": " + (exception.InnerException ?? exception).Message); }
        }
    }
    reports.Add(new { suite, passed, failed });
}
return reports;
