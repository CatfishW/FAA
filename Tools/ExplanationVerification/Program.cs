using System;
using System.Linq;
using System.Reflection;
using FAA.Explanations.Tests;
using NUnit.Framework;

// GUI-independent execution of the same pure NUnit assertions used by the Unity
// EditMode suite. Does not substitute for Play mode or native UI verification.
if (args.Length > 0 && args[0] == "--provider-smoke") return await ProviderSmoke.Run(args.Skip(1).FirstOrDefault());
int passed = 0, failed = 0;
foreach (var method in typeof(ExplanationTests).GetMethods(BindingFlags.Public | BindingFlags.Instance))
{
    var cases = method.GetCustomAttributes<TestCaseAttribute>().Select(a => a.Arguments).ToArray();
    if (cases.Length == 0 && method.IsDefined(typeof(TestAttribute))) cases = new[] { Array.Empty<object>() };
    foreach (var arguments in cases)
    {
        string label = method.Name + "(" + string.Join(", ", arguments.Select(a => a?.ToString() ?? "null")) + ")";
        try { method.Invoke(new ExplanationTests(), arguments); passed++; Console.WriteLine("PASS " + label); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + label + ": " + (ex.InnerException ?? ex).Message); }
    }
}
Console.WriteLine($"Pure explanation checks: {passed} passed, {failed} failed.");
return failed == 0 && passed > 0 ? 0 : 1;
