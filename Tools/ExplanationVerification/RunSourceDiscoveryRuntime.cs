// Actual bridge methods with an inactive owned fixture; no synthetic values enter the live HUD.
if(!Application.isPlaying)throw new Exception("Use Play mode.");
var checks=new System.Collections.Generic.List<object>();Action<string,bool> check=(name,passed)=>checks.Add(new{name,passed});
var source=FAA.XPlaneIntegration.Runtime.XPlaneSourceDiscovery.Active;
check("Discovery coordinator is active",source!=null);
var w=FAA.Customization.FaaSpatialWorkspace.Current;
check("Source controls are on the existing side settings canvas",w.SettingsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.name=="Data Sources Tab"));
check("Source discovery does not activate laptop camera",w.LaptopCamera!=null&&!w.LaptopCamera.CameraActive);
var go=new GameObject("Owned inactive source bridge fixture");go.SetActive(false);go.hideFlags=HideFlags.DontSave;
try
{
    var bridge=go.AddComponent<FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge>();
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    foreach(string field in new[]{"autoStartOnPlay","autoDiscoverSources","applyToAviationHud","applyToLegacyHud","applyToAircraftController","applyToTrafficRadar","applyToWeatherRadar","pollRenderAssets"})
        bridge.GetType().GetField(field,flags).SetValue(bridge,false);
    const string p="sim/flightmodel/position/";
    var data=new System.Collections.Generic.Dictionary<string,double>
    {
        [p+"latitude"]=33,[p+"longitude"]=-83,[p+"elevation"]=1200,[p+"theta"]=2,[p+"phi"]=3,[p+"psi"]=90,
        [p+"indicated_airspeed"]=100,[p+"groundspeed"]=45,[p+"vh_ind"]=1,[p+"hpath"]=95,
        ["sim/weather/aircraft/ambient_temperature_c"]=10,["sim/flightmodel/engine/ENGN_driv_TRQ[0]"]=100,
        ["sim/flightmodel/engine/POINT_max_TRQ[0]"]=200,["sim/aircraft/engine/acf_num_engines"]=1
    };
    var a=new FAA.XPlaneIntegration.Runtime.XPlaneDiscoveredFrame{Id="fixture-a",Label="ISOLATED FIXTURE A",Values=data,Received=FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryData.Now};
    bridge.ApplyDiscoveredFrame(a);
    check("Complete normalized frame reaches existing data adapter",bridge.IsDiscoveryDriven&&bridge.IsFeedHealthy&&bridge.LatestRawFlightData.attitudeValid);
    check("Declared vertical-speed units reach live instrument model",Mathf.Abs(bridge.LatestRawFlightData.verticalSpeed-196.8504f)<.01f);
    check("Known optional engine channel can be valid",bridge.LatestRawFlightData.engine1TorqueValid);
    var second=data.Where(k=>k.Key.StartsWith(p)).ToDictionary(k=>k.Key,k=>k.Value);second[p+"elevation"]=1800;
    var b=new FAA.XPlaneIntegration.Runtime.XPlaneDiscoveredFrame{Id="fixture-b",Label="ISOLATED FIXTURE B",Values=second,Received=FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryData.Now};
    bridge.ApplyDiscoveredFrame(b);
    check("Changing source clears prior optional engine channel",!bridge.LatestRawFlightData.engine1TorqueValid);
    check("Changing source clears prior weather instead of mixing",bridge.LatestSnapshot.Weather.Count==0);
    check("New source altitude snaps rather than interpolating across simulators",Mathf.Abs(bridge.LatestFlightData.altitudeMSL-1800*3.28084f)<1f);
    var previous=bridge.LatestRawFlightData.altitudeMSL;
    var stale=new FAA.XPlaneIntegration.Runtime.XPlaneDiscoveredFrame{Id="fixture-c",Label="ISOLATED STALE",Values=data,Received=FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryData.Now-2};
    bridge.ApplyDiscoveredFrame(stale);check("Stale discovery sample cannot replace active flight state",bridge.LatestRawFlightData.altitudeMSL==previous);
    bridge.GetType().GetField("discoveryLastReceived",flags).SetValue(bridge,FAA.XPlaneIntegration.Runtime.XPlaneDiscoveryData.Now-1.5);
    bridge.GetType().GetMethod("UpdateDiscoveredHealth",flags).Invoke(bridge,null);
    check("Silence invalidates healthy state",!bridge.IsFeedHealthy&&bridge.LastPacketAgeSeconds>1);
    bridge.SetDiscoveryWaiting("fixture-wait");
    check("Waiting state clears old sample and all snapshot categories",bridge.LatestRawFlightData==null&&bridge.LatestSnapshot.Aircraft.Count==0&&bridge.LatestSnapshot.Weather.Count==0&&bridge.LatestSnapshot.Systems.Count==0);
    bridge.StopBridge();check("Stop releases discovery ownership",!bridge.IsDiscoveryDriven&&!bridge.IsRunning);
    var coordinator=go.AddComponent<FAA.XPlaneIntegration.Runtime.XPlaneSourceDiscovery>();
    coordinator.GetType().GetField("bridge",flags).SetValue(coordinator,bridge);
    coordinator.GetType().GetField("config",flags).SetValue(coordinator,new FAA.XPlaneIntegration.Runtime.XPlaneSourceConfig());
    coordinator.GetType().GetProperty("Mode").SetValue(coordinator,FAA.XPlaneIntegration.Runtime.XPlaneSourceDiscovery.SelectionMode.Stopped);
    coordinator.GetType().GetMethod("StartDiscovery",flags).Invoke(coordinator,null);
    check("Stopped coordinator starts no transport or synthetic waiting session",!bridge.IsRunning&&coordinator.SourceSummary=="DATA SOURCE STOPPED");
}
finally{UnityEngine.Object.DestroyImmediate(go);}
return checks;
