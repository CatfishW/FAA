using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaAnalogVector;
namespace FAA.Customization
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaClassicGaugeGraphic:MaskableGraphic
    {
        public enum Gauge { Airspeed,Altitude,Torque,RotorRpm }
        public Gauge Kind {get;private set;}
        public float DisplayedValue {get;private set;}
        public bool DataValid {get;private set;}
        public float NeedleAngle => Kind==Gauge.Airspeed?FaaAnalogAnimation.SpeedAngle(DisplayedValue):Kind==Gauge.Altitude?FaaAnalogAnimation.AltitudeAngle(DisplayedValue):FaaAnalogAnimation.PercentAngle(DisplayedValue,Kind==Gauge.Torque?150:120);
        private TMP_Text value,unit,secondary,thousands;
        private TMP_Text[] ticks;
        private Color tint;
        public void Configure(Gauge kind,Color green)
        {
            Kind=kind;tint=green;color=green;raycastTarget=false;
            bool small=kind==Gauge.Torque||kind==Gauge.RotorRpm;
            if(!small)
            {
                int count=kind==Gauge.Altitude?10:12;ticks=new TMP_Text[count];
                for(int i=0;i<count;i++)
                {
                    float degree=i*360f/count;Vector2 p=Bearing(degree,87);
                    ticks[i]=Text("Scale "+i,p,new Vector2(39,22),12);
                    ticks[i].text=kind==Gauge.Altitude?i.ToString():(i*20).ToString();
                    ticks[i].rectTransform.localRotation=Quaternion.Euler(0,0,-degree);
                }
            }
            value=Text("Live Value",new Vector2(0,small?-23:-30),new Vector2(small?64:80,33),small?25:29);
            value.enableAutoSizing=true;value.fontSizeMin=17;value.fontSizeMax=small?25:29;
            unit=Text("Units",new Vector2(0,small?-42:-48),new Vector2(115,18),small?11:10);
            unit.text=kind==Gauge.Airspeed?"KTS":kind==Gauge.Altitude?"FT MSL":kind==Gauge.Torque?"TQ":"RPM";
            secondary=Text("Source detail",new Vector2(0,small?-94:-137),new Vector2(small?200:250,22),11);
            if(kind==Gauge.Altitude)thousands=Text("Thousands",new Vector2(75,-41),new Vector2(55,36),12);
        }
        private TMP_Text Text(string name,Vector2 p,Vector2 size,float fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(transform,false);
            var text=go.AddComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.color=tint;text.fontSize=fontSize;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.richText=false;text.textWrappingMode=TextWrappingModes.NoWrap;
            text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,.5f);
            text.rectTransform.anchoredPosition=p;text.rectTransform.sizeDelta=size;return text;
        }
        public void Present(float reading,bool valid,string detail,Color green)
        {
            valid&=FaaAnalogFlightSample.Finite(reading);
            bool changed=DataValid!=valid||Mathf.Abs(DisplayedValue-reading)>.0001f||green!=tint;
            DisplayedValue=FaaAnalogFlightSample.Finite(reading)?reading:0;DataValid=valid;tint=green;
            float alpha=valid?1f:.35f;Color c=new Color(green.r,green.g,green.b,green.a*alpha);
            color=c;value.color=unit.color=c;secondary.color=c;
            if(ticks!=null)foreach(var t in ticks)t.color=c;
            value.text=valid?(Kind==Gauge.Airspeed?Mathf.RoundToInt(reading).ToString():Kind==Gauge.Altitude?Mathf.RoundToInt(reading).ToString():reading.ToString("F0")+"%"):"---";
            secondary.text=valid?detail:"DATA UNAVAILABLE";
            if(valid&&Kind==Gauge.Airspeed&&reading>=240)secondary.text="OFF SCALE  >240 KTS";
            if(valid&&(Kind==Gauge.Torque&&reading>150||Kind==Gauge.RotorRpm&&reading>120))secondary.text="OFF SCALE";
            if(thousands!=null){thousands.text=valid?Mathf.FloorToInt(reading/1000f)+"\nx1000":"--";thousands.color=c;}
            if(changed)SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();bool small=Kind==Gauge.Torque||Kind==Gauge.RotorRpm;float radius=small?76:115;Color c=color;
            Ring(vh,Vector2.zero,radius,2f,c,128);
            int steps=Kind==Gauge.Altitude?50:small?30:48;
            for(int i=0;i<steps;i++)
            {
                float a=small?-135+i*270f/(steps-1):i*360f/steps;bool major=small?i%5==0:i%(Kind==Gauge.Altitude?5:4)==0;
                Line(vh,Bearing(a,radius-7),Bearing(a,radius-(major?20:13)),major?1.8f:1.1f,new Color(c.r,c.g,c.b,c.a*(major?1:.6f)));
            }
            RoundedBox(vh,small?new Rect(-35,-51,70,42):new Rect(-43,-54,86,40),7,c);
            if(!DataValid)return;
            Vector2 tip=Bearing(NeedleAngle,radius-19);Line(vh,-Bearing(NeedleAngle,6),tip,small?4:5,c);Disc(vh,Vector2.zero,small?4:6,c);
        }
    }
}
