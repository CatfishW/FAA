// Focused direct NUnit checks, not a full Unity Test Runner report.
if (Application.isPlaying) throw new Exception("Run Edit-mode fixtures before Play mode.");
var reports=new System.Collections.Generic.List<object>();
foreach(string suite in new[]{"FaaSourceDiscoveryTests","FaaReferenceVsiTests","FaaHudTerrainReleaseTests","FaaAnalogSymbologyTests","FaaRadarBriefDragTests","FaaPeripheralStabilityTests","FaaMultiFingerTests","FaaCameraPermissionTests","FaaWebcamGestureTests","FaaSpatialWorkspaceTests","FaaRotorcraftConformalTests","FaaPitchLadderTests","FaaHudStabilityTests","FaaViewAlignmentTests","FaaScreenCueTests","FaaCueStabilityTests","XPlaneEngineHudBindingTests","FaaRadarSettingsTests","FaaRadarPresentationTests","FaaXPlaneTerrainTests"})
{
    var type=Type.GetType("FAA.Customization.Tests."+suite+", FAA.Customization.EditorTests",true);
    int passed=0;var failed=new System.Collections.Generic.List<string>();
    foreach(var method in type.GetMethods())
    {
        var attrs=method.GetCustomAttributes(false);
        var cases=attrs.Where(a=>a.GetType().Name=="TestCaseAttribute").Select(a=>(object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToArray();
        if(cases.Length==0&&attrs.Any(a=>a.GetType().Name=="TestAttribute"))cases=new[]{Array.Empty<object>()};
        foreach(var args in cases)
        {
            object fixture=Activator.CreateInstance(type);
            try
            {
                foreach(var setup in type.GetMethods().Where(m=>m.GetCustomAttributes(false).Any(a=>a.GetType().Name=="SetUpAttribute")))setup.Invoke(fixture,null);
                method.Invoke(fixture,args);passed++;
            }
            catch(Exception e){failed.Add(method.Name+": "+(e.InnerException??e).Message);}
            finally{foreach(var teardown in type.GetMethods().Where(m=>m.GetCustomAttributes(false).Any(a=>a.GetType().Name=="TearDownAttribute")))teardown.Invoke(fixture,null);}
        }
    }
    reports.Add(new{suite,passed,failed});
}
return reports;
