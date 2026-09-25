// Direct NUnit assertions in the existing Editor. Not a Test Runner XML report.
if (UnityEditor.EditorApplication.isPlaying) return "Stop Play mode before running edit-time fixtures.";
var type = System.Type.GetType("FAA.Customization.Tests.FaaXPlaneTerrainTests, FAA.Customization.EditorTests", true);
int passed = 0;
var failed = new System.Collections.Generic.List<string>();
foreach (var method in type.GetMethods())
{
    var attrs = method.GetCustomAttributes(false);
    var cases = attrs.Where(a => a.GetType().Name == "TestCaseAttribute")
        .Select(a => (object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToArray();
    if (cases.Length == 0 && attrs.Any(a => a.GetType().Name == "TestAttribute")) cases = new[] { System.Array.Empty<object>() };
    foreach (var arguments in cases)
    {
        try { method.Invoke(System.Activator.CreateInstance(type), arguments); passed++; }
        catch (System.Exception error) { failed.Add(method.Name + ": " + (error.InnerException ?? error).Message); }
    }
}
return Newtonsoft.Json.JsonConvert.SerializeObject(new { suite = type.Name, passed, failed });
