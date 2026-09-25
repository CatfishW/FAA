// Real Unity vector/TMP rendering of an isolated VSI at explicit test inputs.
// Never changes the live bridge, aircraft, camera feed or HUD preferences.
if(!Application.isPlaying)throw new Exception("Capture the reference fixture in Play mode.");
var output="docs/screenshots/2026-09-25";System.IO.Directory.CreateDirectory(output);
var parent=new GameObject("Reference VSI capture fixture");parent.hideFlags=HideFlags.DontSave;
RenderTexture render=null;Texture2D pixels=null;var previous=RenderTexture.active;
var report=new System.Collections.Generic.List<object>();
try
{
    var viewObject=new GameObject("Isolated capture camera",typeof(Camera));viewObject.transform.SetParent(parent.transform,false);
    var view=viewObject.GetComponent<Camera>();view.enabled=false;view.transform.position=new Vector3(900000,900000,900000);
    view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;view.cullingMask=1<<31;
    view.orthographic=true;view.orthographicSize=500;view.nearClipPlane=.1f;view.farClipPlane=4;
    render=new RenderTexture(360,1080,24,RenderTextureFormat.ARGB32){antiAliasing=4};render.Create();view.targetTexture=render;
    var canvasObject=new GameObject("Capture canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
    canvasObject.transform.SetParent(parent.transform,false);canvasObject.layer=31;
    var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=1;
    var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
    var instrument=new GameObject("Rendered VSI",typeof(RectTransform));instrument.layer=31;instrument.transform.SetParent(canvasObject.transform,false);
    var rect=instrument.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(140,340);rect.anchoredPosition=new Vector2(-50,0);rect.localScale=Vector3.one*3;
    var gauge=instrument.AddComponent<FAA.Customization.FaaClassicDeviationGraphic>();gauge.Configure(FAA.Customization.FaaClassicDeviationGraphic.Scale.VerticalSpeed,new Color(117f/255f,187f/255f,64f/255f,1));
    foreach(Transform child in instrument.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
    foreach(float value in new[]{0f,1000f,-1000f})
    {
        gauge.Present(value,true,new Color(117f/255f,187f/255f,64f/255f,1));Canvas.ForceUpdateCanvases();
        foreach(var text in instrument.GetComponentsInChildren<TMPro.TMP_Text>())text.ForceMeshUpdate(true,true);
        view.Render();RenderTexture.active=render;
        pixels=new Texture2D(360,1080,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,360,1080),0,0);pixels.Apply();
        string name=value==0?"vsi-reference-zero.png":value>0?"vsi-reference-climb.png":"vsi-reference-descent.png";
        System.IO.File.WriteAllBytes(output+"/"+name,pixels.EncodeToPNG());UnityEngine.Object.DestroyImmediate(pixels);pixels=null;
        report.Add(new{path=output+"/"+name,testValueFpm=value,source="Actual Unity renderer, isolated test input, not live flight telemetry"});
    }
}
finally
{
    RenderTexture.active=previous;if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
    UnityEngine.Object.DestroyImmediate(parent);if(render!=null){render.Release();UnityEngine.Object.DestroyImmediate(render);}
}
return report;
