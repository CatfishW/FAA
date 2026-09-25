if (UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Use Edit mode for direct NUnit fixtures.");
var reports = new System.Collections.Generic.List<object>();
foreach (string suite in new[] { "FaaSpatialWorkspaceTests", "FaaRotorcraftConformalTests", "FaaPitchLadderTests", "FaaHudStabilityTests", "FaaViewAlignmentTests", "FaaScreenCueTests", "FaaCueStabilityTests", "XPlaneEngineHudBindingTests", "FaaRadarSettingsTests", "FaaRadarPresentationTests" })
{
    var type = System.Type.GetType("FAA.Customization.Tests." + suite + ", FAA.Customization.EditorTests", true);
    int passed = 0;
    var failed = new System.Collections.Generic.List<string>();
    foreach (var method in type.GetMethods())
    {
        var attributes = method.GetCustomAttributes(false);
        var cases = attributes.Where(a => a.GetType().Name == "TestCaseAttribute")
            .Select(a => (object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToArray();
        if (cases.Length == 0 && attributes.Any(a => a.GetType().Name == "TestAttribute")) cases = new[] { System.Array.Empty<object>() };
        foreach (var arguments in cases)
        {
            var fixture = System.Activator.CreateInstance(type);
            try
            {
                foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "SetUpAttribute"))) setup.Invoke(fixture, null);
                method.Invoke(fixture, arguments); passed++;
            }
            catch (System.Exception e) { failed.Add(method.Name + ": " + (e.InnerException ?? e).Message); }
            finally
            {
                foreach (var teardown in type.GetMethods().Where(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "TearDownAttribute"))) teardown.Invoke(fixture, null);
            }
        }
    }
    reports.Add(new { suite, passed, failed });
}
return reports;
