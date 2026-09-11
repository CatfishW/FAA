// Run with unity command eval_file in the existing Editor, outside Play mode.
// Executes NUnit assertion methods directly; does not impersonate a Unity Test Runner report.
if (UnityEditor.EditorApplication.isPlaying) return "Stop Play mode before running edit-time fixtures.";
var suites = new[]
{
    "FAA.Explanations.Tests.ExplanationTests, FAA.Explanations.EditorTests",
    "FAA.Explanations.Tests.ExplanationPanelTests, FAA.Explanations.EditorTests",
    "FAA.Customization.Tests.FaaTurbulenceModeTests, FAA.Customization.EditorTests",
    "FAA.Customization.Tests.FaaWeatherRangeGainTests, FAA.Customization.EditorTests"
};
var reports = new System.Collections.Generic.List<object>();
foreach (string suite in suites)
{
    var type = System.Type.GetType(suite, true);
    int passed = 0;
    var failed = new System.Collections.Generic.List<string>();
    var methods = type.GetMethods();
    var setup = methods.FirstOrDefault(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "SetUpAttribute"));
    var teardown = methods.FirstOrDefault(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TearDownAttribute"));
    foreach (var method in methods)
    {
        var attributes = method.GetCustomAttributes(false);
        var cases = attributes.Where(a => a.GetType().Name == "TestCaseAttribute")
            .Select(a => (object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToArray();
        if (cases.Length == 0 && attributes.Any(a => a.GetType().Name == "TestAttribute"))
            cases = new[] { System.Array.Empty<object>() };
        foreach (var arguments in cases)
        {
            var fixture = System.Activator.CreateInstance(type);
            try { setup?.Invoke(fixture, null); method.Invoke(fixture, arguments); passed++; }
            catch (System.Exception exception) { failed.Add(method.Name + ": " + (exception.InnerException ?? exception).Message); }
            finally { teardown?.Invoke(fixture, null); }
        }
    }
    reports.Add(new { suite = type.Name, passed, failed });
}
return reports;
