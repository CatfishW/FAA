if(!Application.isPlaying)throw new Exception("Run in Play mode.");
var results=new System.Collections.Generic.List<object>();
System.Action<string,bool> check=(name,passed)=>results.Add(new{name,passed});
var owner=new GameObject("Reference VSI runtime fixture");owner.hideFlags=HideFlags.DontSave;
try
{
    var canvas=owner.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
    var go=new GameObject("Isolated VSI",typeof(RectTransform));go.transform.SetParent(owner.transform,false);
    var gauge=go.AddComponent<FAA.Customization.FaaClassicDeviationGraphic>();
    gauge.Configure(FAA.Customization.FaaClassicDeviationGraphic.Scale.VerticalSpeed,Color.green);
    foreach(float scale in new[]{.4f,.72f,1f,1.6f})
    {
        go.transform.localScale=Vector3.one*scale;gauge.Present(1000,true,Color.green);Canvas.ForceUpdateCanvases();
        int count=0;float minimumTickGap=float.PositiveInfinity,minimumRailGap=float.PositiveInfinity;
        foreach(var text in go.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            if(!text.name.StartsWith("VSI "))continue;count++;text.ForceMeshUpdate(true,true);
            float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                var ch=text.textInfo.characterInfo[i];if(!ch.isVisible)continue;
                minimum=Mathf.Min(minimum,text.rectTransform.anchoredPosition.x+ch.bottomLeft.x);
                maximum=Mathf.Max(maximum,text.rectTransform.anchoredPosition.x+ch.topRight.x);
            }
            minimumTickGap=Mathf.Min(minimumTickGap,(minimum+1.4f)*scale);
            minimumRailGap=Mathf.Min(minimumRailGap,(14.2f-maximum)*scale);
        }
        check("Four inside-scale numeral meshes exist at "+scale,count==4);
        check("Actual glyphs clear ticks and rail at "+scale,minimumTickGap>.8f&&minimumRailGap>.8f);
    }
    var populate=typeof(FAA.Customization.FaaClassicDeviationGraphic).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.UI.VertexHelper)},null);
    using(var vh=new UnityEngine.UI.VertexHelper())
    {
        gauge.Present(0,true,Color.green);populate.Invoke(gauge,new object[]{vh});int valid=vh.currentVertCount;
        gauge.Present(0,false,Color.green);populate.Invoke(gauge,new object[]{vh});int invalid=vh.currentVertCount;
        check("Unavailable signal removes real pointer mesh",invalid<valid&&invalid>0);
        check("Unavailable signal is labelled",go.transform.Find("Value").GetComponent<TMPro.TMP_Text>().text=="NO DATA");
        gauge.Present(-3000,true,Color.green);populate.Invoke(gauge,new object[]{vh});
        check("Off-scale readout retains actual value",go.transform.Find("Value").GetComponent<TMPro.TMP_Text>().text.Contains("-3000")&&go.transform.Find("Value").GetComponent<TMPro.TMP_Text>().text.Contains("OFF SCALE"));
    }
    var animation=new FAA.Customization.FaaAnalogAnimation();
    var sample=new FAA.Customization.FaaAnalogFlightSample{Fresh=true,VerticalSpeedValid=true,VerticalSpeed=-1800};
    animation.Step(sample,0,true);float last=animation.Display.VerticalSpeed;
    sample.VerticalSpeed=1800;bool monotonic=true,bounded=true;
    for(int frame=0;frame<120;frame++)
    {
        animation.Step(sample,1f/120,false);float current=animation.Display.VerticalSpeed;
        monotonic&=current>=last-.001f;bounded&=current<=1800.001f&&current>=-1800.001f;last=current;
        gauge.Present(current,true,Color.green);
    }
    check("Animated vertical-speed sweep is monotonic and bounded",monotonic&&bounded);
    check("Sweep converges without reverse movement",Mathf.Abs(last-1800)<1f);
}
finally{UnityEngine.Object.DestroyImmediate(owner);}
return results;
