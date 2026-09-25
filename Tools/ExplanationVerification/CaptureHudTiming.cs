// Read-only 180-frame / 10-second render-phase trace. Run in Play via unity eval_file.
if (!UnityEditor.EditorApplication.isPlaying) return "Enter Play mode first.";
var hud = UnityEngine.Object.FindFirstObjectByType<FAA.Customization.FaaConformalHudController>();
var camera = UnityEngine.Camera.main;
var view = camera == null ? null : camera.GetComponent<AircraftControl.Camera.AircraftCameraController>();
var bridge = UnityEngine.Object.FindFirstObjectByType<FAA.XPlaneIntegration.Runtime.XPlane12ApiHudBridge>();
if (hud == null || view == null || view.AircraftTransform == null) return "HUD/camera bindings unavailable.";
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var root = (UnityEngine.RectTransform)hud.GetType().GetField("_screenHudRoot", flags).GetValue(hud);
var basePosition = (UnityEngine.Vector2)hud.GetType().GetField("_headFixedAnchoredPosition", flags).GetValue(hud);
var canvas = hud.ScreenCanvas;
var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
var samples = new System.Collections.Generic.List<float[]>();
int frame = -1;
double start = UnityEngine.Time.realtimeSinceStartupAsDouble;
UnityEngine.Canvas.WillRenderCanvases callback = null;
callback = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying || root == null || camera == null)
    { UnityEngine.Canvas.willRenderCanvases -= callback; return; }
    if (UnityEngine.Time.frameCount == frame) return;
    frame = UnityEngine.Time.frameCount;
    var pixelRect = camera.pixelRect;
    var resolution = scaler.referenceResolution;
    var actual = new UnityEngine.Vector2((root.anchoredPosition.x-basePosition.x)*pixelRect.width/resolution.x,
        (root.anchoredPosition.y-basePosition.y)*pixelRect.height/resolution.y);
    var direction = view.AircraftReferenceRotation * UnityEngine.Vector3.forward;
    var finite = camera.WorldToScreenPoint(view.AircraftTransform.position + direction * 175.6f);
    // Independently evaluate a world direction (w=0): matrix translation cannot
    // contaminate the comparison, including at large world coordinates.
    var viewDirection = camera.worldToCameraMatrix.MultiplyVector(direction);
    var clip = camera.nonJitteredProjectionMatrix * new UnityEngine.Vector4(viewDirection.x, viewDirection.y, viewDirection.z, 0);
    var angular = new UnityEngine.Vector3(pixelRect.x + (clip.x/clip.w+1)*.5f*pixelRect.width,
        pixelRect.y + (clip.y/clip.w+1)*.5f*pixelRect.height, -viewDirection.z);
    var finiteOffset = (UnityEngine.Vector2)finite - pixelRect.center;
    var angularOffset = (UnityEngine.Vector2)angular - pixelRect.center;
    if (finite.z > 0 && angular.z > 0)
        samples.Add(new[] { UnityEngine.Vector2.Distance(actual, finiteOffset),
            UnityEngine.Vector2.Distance(actual, angularOffset),
            UnityEngine.Vector3.Distance(camera.transform.position, view.AircraftTransform.position),
            UnityEngine.Time.unscaledDeltaTime*1000, bridge == null ? 0 : bridge.LastPacketAgeSeconds*1000 });
    if (samples.Count < 180 && UnityEngine.Time.realtimeSinceStartupAsDouble-start < 10) return;
    UnityEngine.Canvas.willRenderCanvases -= callback;
    string[] names={"finite_anchor_difference_px","collimated_error_px","camera_position_lag_m","frame_ms","packet_age_ms"};
    var report = new System.Collections.Generic.Dictionary<string,object>();
    report["samples"] = samples.Count;
    report["pixel_width"] = pixelRect.width;
    for(int i=0;i<names.Length;i++)
    {
        int index=i; var values=samples.Select(s=>s[index]).OrderBy(v=>v).ToArray();
        if(values.Length>0) report[names[i]]=new {mean=values.Average(),p95=values[(int)((values.Length-1)*.95)],max=values.Last()};
    }
    System.IO.Directory.CreateDirectory("Temp");
    System.IO.File.WriteAllText("Temp/faa-hud-timing.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
};
UnityEngine.Canvas.willRenderCanvases += callback;
return "Read-only render trace started; result: Temp/faa-hud-timing.json";
