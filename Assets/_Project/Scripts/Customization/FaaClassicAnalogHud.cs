using System.Collections.Generic;
using FAA.XPlaneIntegration.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    [DefaultExecutionOrder(12480),DisallowMultipleComponent]
    public sealed class FaaClassicAnalogHud:MonoBehaviour
    {
        public readonly Dictionary<string,RectTransform> InstrumentRoots=new();
        public readonly Dictionary<string,string> Captions=new();
        public FaaAnalogAnimation Animation {get;}=new();
        public FaaAnalogFlightSample LastSample {get;private set;}
        public bool ReducedMotion {get;set;}
        public bool LocalAttitude {get;set;}=true;
        public bool Visible {get;private set;}
        public float PanelInspectionOpacity {get;set;}=1f;
        public Color ReferenceGreen {get;set;}=new Color(117f/255f,187f/255f,64f/255f,1);
        public RectTransform Root {get;private set;}
        private Canvas flightCanvas;
        private Transform sourceRoot;
        private XPlane12ApiHudBridge bridge;
        private CanvasGroup group;
        private FaaClassicGaugeGraphic speed,altitude,torque,rpm;
        private FaaClassicAttitudeGraphic attitude;
        private FaaClassicBankGraphic bank;
        private FaaClassicDeviationGraphic loc,gs,vsi;
        private TMP_Text collectiveMode,rollMode,pitchMode,coupling,rollArmed,pitchArmed,heading,dataFlag;
        private float nextSourceSearch;
        private readonly float[] layoutScales=new float[10];
        private Material textOutlineMaterial;

        public void Build(Canvas canvas,Transform originalRoot)
        {
            flightCanvas=canvas;sourceRoot=originalRoot;
            Root=(RectTransform)transform;Root.SetParent(canvas.transform,false);Root.anchorMin=Root.anchorMax=Root.pivot=new Vector2(.5f,.5f);
            Root.sizeDelta=new Vector2(960,820);Root.anchoredPosition=Vector2.zero;
            group=gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            // The reference's black is treated as transparency, never an opaque cockpit-blocking card.
            speed=Gauge("airspeed","Airspeed dial",142,482,FaaClassicGaugeGraphic.Gauge.Airspeed);
            altitude=Gauge("altitude","Altitude dial",720,482,FaaClassicGaugeGraphic.Gauge.Altitude);
            torque=Gauge("torque","Engine torque dial",250,669,FaaClassicGaugeGraphic.Gauge.Torque);
            rpm=Gauge("nr","Rotor RPM dial",602,669,FaaClassicGaugeGraphic.Gauge.RotorRpm);
            var bankRoot=Instrument("bank","Bank and slip references",420,220,760,250);
            bank=bankRoot.gameObject.AddComponent<FaaClassicBankGraphic>();
            var attitudeHolder=Instrument("attitude","Non-conformal attitude",420,385,250,290);
            attitudeHolder.gameObject.AddComponent<RectMask2D>();
            var draw=new GameObject("Classic attitude geometry",typeof(RectTransform));draw.transform.SetParent(attitudeHolder,false);
            var rt=(RectTransform)draw.transform;rt.sizeDelta=attitudeHolder.sizeDelta;
            attitude=draw.AddComponent<FaaClassicAttitudeGraphic>();attitude.Configure(ReferenceGreen);
            loc=Deviation("localizer","Localizer dots",424,565,250,80,FaaClassicDeviationGraphic.Scale.Localizer);
            gs=Deviation("glideslope","Glideslope dots",864,382,55,320,FaaClassicDeviationGraphic.Scale.Glideslope);
            vsi=Deviation("vertical-speed","Vertical speed scale",902,382,130,320,FaaClassicDeviationGraphic.Scale.VerticalSpeed);
            var modes=Instrument("heading","Modes and heading",420,48,390,72);
            collectiveMode=Text(modes,"Collective mode",new Vector2(-131,2),new Vector2(92,32),28);
            rollMode=Text(modes,"Roll mode",new Vector2(-26,2),new Vector2(118,32),27);
            pitchMode=Text(modes,"Pitch mode",new Vector2(143,2),new Vector2(122,32),27);
            coupling=Text(modes,"Coupling",new Vector2(61,6),new Vector2(54,27),17);
            foreach(var modeText in new[]{collectiveMode,rollMode,pitchMode})
            {modeText.enableAutoSizing=true;modeText.fontSizeMin=16;modeText.fontSizeMax=27;}
            rollArmed=Text(modes,"Lateral armed",new Vector2(-22,-22),new Vector2(122,22),13);
            pitchArmed=Text(modes,"Vertical armed",new Vector2(145,-22),new Vector2(130,22),13);
            heading=Text(modes,"Heading",new Vector2(10,-46),new Vector2(220,24),12);
            dataFlag=Text(Root,"Data validity",new Vector2(-60,-400),new Vector2(620,22),11);
            // Reference green needs local contrast against a bright outside scene. Tiny outlines,
            // not an opaque backdrop, preserve both readability and the unobstructed view.
            foreach(var graphic in Root.GetComponentsInChildren<MaskableGraphic>(true))
                if(!(graphic is TMP_Text))
                {
                    var outline=graphic.gameObject.AddComponent<Outline>();
                    outline.effectColor=new Color(0,0,0,.75f);outline.effectDistance=new Vector2(.7f,-.7f);
                }
            if(TMP_Settings.defaultFontAsset!=null&&TMP_Settings.defaultFontAsset.material!=null)
            {
                textOutlineMaterial=new Material(TMP_Settings.defaultFontAsset.material){name="Classic HUD contrast text"};
                if(textOutlineMaterial.HasProperty("_OutlineColor"))textOutlineMaterial.SetColor("_OutlineColor",new Color(0,0,0,.85f));
                if(textOutlineMaterial.HasProperty("_OutlineWidth"))textOutlineMaterial.SetFloat("_OutlineWidth",.13f);
                foreach(var text in Root.GetComponentsInChildren<TMP_Text>(true))
                {text.fontSharedMaterial=textOutlineMaterial;text.UpdateMeshPadding();}
            }
            Present(default,0,true);SetVisible(false);
        }
        private RectTransform Instrument(string id,string caption,float x,float y,float w,float h)
        {
            var go=new GameObject("Classic "+caption,typeof(RectTransform));var t=(RectTransform)go.transform;t.SetParent(Root,false);
            t.anchorMin=t.anchorMax=t.pivot=new Vector2(.5f,.5f);t.anchoredPosition=new Vector2(x-480,390-y);t.sizeDelta=new Vector2(w,h);
            InstrumentRoots.Add(id,t);Captions.Add(id,caption);return t;
        }
        private FaaClassicGaugeGraphic Gauge(string id,string caption,float x,float y,FaaClassicGaugeGraphic.Gauge kind)
        {
            bool small=kind==FaaClassicGaugeGraphic.Gauge.Torque||kind==FaaClassicGaugeGraphic.Gauge.RotorRpm;
            var root=Instrument(id,caption,x,y,small?180:250,small?210:290);
            var g=root.gameObject.AddComponent<FaaClassicGaugeGraphic>();g.Configure(kind,ReferenceGreen);return g;
        }
        private FaaClassicDeviationGraphic Deviation(string id,string caption,float x,float y,float width,float height,FaaClassicDeviationGraphic.Scale kind)
        {var root=Instrument(id,caption,x,y,width,height);var d=root.gameObject.AddComponent<FaaClassicDeviationGraphic>();d.Configure(kind,ReferenceGreen);return d;}
        private TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size,float font)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var t=go.AddComponent<TextMeshProUGUI>();
            t.font=TMP_Settings.defaultFontAsset;t.fontSize=font;t.color=ReferenceGreen;t.raycastTarget=false;t.richText=false;
            t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;
            t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=size;return t;
        }
        public void SetVisible(bool visible)
        {
            bool changed=Visible!=visible;
            if(changed)Animation.Reset();Visible=visible;
            if(group!=null&&(changed||!visible))group.alpha=visible?1:0;
            if(enabled!=visible)enabled=visible;
            if(changed&&visible)
            {
                if(bridge==null)bridge=FindFirstObjectByType<XPlane12ApiHudBridge>();
                Present(FaaAnalogFlightSample.Capture(bridge),0,true);
            }
        }
        public void ApplyLayout()
        {
            if(Root==null||flightCanvas==null)return;
            // Preserve the reference composition at 100%; enlarged individual instruments
            // get additional separation instead of running through neighbouring dials.
            int n=0;foreach(var p in InstrumentRoots)layoutScales[n++]=p.Value.localScale.x;
            System.Array.Sort(layoutScales,0,n);
            float common=n>0?layoutScales[n/2]:1f;
            float air=InstrumentRoots["airspeed"].localScale.x,alt=InstrumentRoots["altitude"].localScale.x;
            float att=InstrumentRoots["attitude"].localScale.x,tor=InstrumentRoots["torque"].localScale.x,rot=InstrumentRoots["nr"].localScale.x;
            float left=420-Mathf.Max(278*common,115*air+125*att+30*common),right=420+Mathf.Max(300*common,115*alt+125*att+30*common);
            float mainY=385+97*common;
            Place("airspeed",left,mainY);Place("altitude",right,mainY);
            Place("torque",left+108*common,Mathf.Max(385+284*common,mainY+115*air+76*tor+15*common));
            Place("nr",right-118*common,Mathf.Max(385+284*common,mainY+115*alt+76*rot+15*common));
            float bankScale=InstrumentRoots["bank"].localScale.x,modeScale=InstrumentRoots["heading"].localScale.x;
            float bankY=385-165*Mathf.Max(common,Mathf.Max(att,bankScale));
            Place("bank",420,bankY);Place("heading",420,bankY-Mathf.Max(172*common,120*bankScale+36*modeScale+18*common));
            float locScale=InstrumentRoots["localizer"].localScale.x;
            Place("localizer",420+4*common,385+Mathf.Max(180*common,145*att+39*locScale+15*common));
            float g=InstrumentRoots["glideslope"].localScale.x,v=InstrumentRoots["vertical-speed"].localScale.x;
            float gsX=right+115*alt+14*g+15*common;
            Place("glideslope",gsX,385-3*common);Place("vertical-speed",gsX+14*g+15*v+9*common,385-3*common);
            var rect=((RectTransform)flightCanvas.transform).rect;
            float fit=Mathf.Min(rect.width/1120f,rect.height*.72f/820f);
            float maxX=480,maxY=410;
            foreach(var pair in InstrumentRoots)
            {
                var r=pair.Value;maxX=Mathf.Max(maxX,Mathf.Abs(r.anchoredPosition.x+60)+r.rect.width*Mathf.Abs(r.localScale.x)*.5f);
                maxY=Mathf.Max(maxY,Mathf.Abs(r.anchoredPosition.y)+r.rect.height*Mathf.Abs(r.localScale.y)*.5f);
            }
            fit=Mathf.Min(fit,rect.width*.45f/maxX,rect.height*.445f/maxY);
            Vector3 size=Vector3.one*Mathf.Max(.05f,fit);Vector2 position=new Vector2(60*fit,0);
            if(Root.localScale!=size)Root.localScale=size;
            if(Root.anchoredPosition!=position)Root.anchoredPosition=position;
            bool valid=Visible&&flightCanvas.isActiveAndEnabled&&sourceRoot!=null&&sourceRoot.gameObject.activeInHierarchy;
            if(valid)foreach(var cg in sourceRoot.GetComponentsInParent<CanvasGroup>(true))if(cg.alpha<=.001f)valid=false;
            if(group!=null)group.alpha=valid?Mathf.Clamp01(PanelInspectionOpacity):0;
            if(InstrumentRoots.TryGetValue("attitude",out var center))center.gameObject.SetActive(LocalAttitude);
        }
        private void Place(string id,float x,float y)
        {
            Vector2 p=new Vector2(x-480,390-y);var r=InstrumentRoots[id];
            if(r.anchoredPosition!=p)r.anchoredPosition=p;
        }
        private void LateUpdate()
        {
            if(bridge==null&&Time.unscaledTime>=nextSourceSearch){nextSourceSearch=Time.unscaledTime+1;bridge=FindFirstObjectByType<XPlane12ApiHudBridge>();}
            Present(FaaAnalogFlightSample.Capture(bridge),Time.unscaledDeltaTime,ReducedMotion);ApplyLayout();
        }
        private void OnDestroy(){if(textOutlineMaterial!=null)Destroy(textOutlineMaterial);}
        public void Present(FaaAnalogFlightSample sample,float deltaTime,bool snap=false)
        {
            LastSample=sample;Animation.Step(sample,deltaTime,snap);var d=Animation.Display;Color c=ReferenceGreen;
            speed.Present(d.Speed,d.Fresh&&d.SpeedValid,"",c);
            altitude.Present(d.Altitude,d.Fresh&&d.AltitudeValid,"",c);
            string engines="MAX ENG / E1 "+(d.Engine1Valid?d.Engine1Torque.ToString("F0"):"--")+"% E2 "+(d.Engine2Valid?d.Engine2Torque.ToString("F0"):"--")+"%";
            torque.Present(d.Torque,d.Fresh&&d.TorqueValid,engines,c);rpm.Present(d.Rpm,d.Fresh&&d.RpmValid,"PRIMARY ROTOR",c);
            attitude.Present(d.Pitch,d.Roll,d.Fresh&&d.AttitudeValid,c);bank.Present(d.Roll,d.Slip,d.Fresh&&d.AttitudeValid,d.Fresh&&d.SlipValid,c);
            loc.Present(d.Localizer,d.Fresh&&d.LocValid,c);gs.Present(-d.Glideslope,d.Fresh&&d.GsValid,c);vsi.Present(d.VerticalSpeed,d.Fresh&&d.VerticalSpeedValid,c);
            collectiveMode.text="C: --";rollMode.text="R: "+(d.RollMode??"--");pitchMode.text="P: "+(d.PitchMode??"--");
            coupling.text=d.Coupling??"AP --";rollArmed.text=d.RollArmed??"";pitchArmed.text=d.PitchArmed??"";
            heading.text=d.Fresh&&d.AttitudeValid?"HDG "+Mathf.Repeat(d.Heading,360).ToString("000")+"° TRUE":"HDG ---";
            dataFlag.text=d.Fresh?"":"CLASSIC ANALOG  /  FLIGHT DATA UNAVAILABLE";
            collectiveMode.color=c*.55f;rollMode.color=pitchMode.color=c;coupling.color=c;rollArmed.color=pitchArmed.color=c*.65f;heading.color=c;dataFlag.color=c*.65f;
        }
    }
}
