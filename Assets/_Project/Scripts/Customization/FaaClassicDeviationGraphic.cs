using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaAnalogVector;
namespace FAA.Customization
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaClassicDeviationGraphic:MaskableGraphic
    {
        public enum Scale { Localizer,Glideslope,VerticalSpeed }
        private Scale kind;private float value;private bool valid;
        public const float VsiRightRailX = FaaClassicVsiGeometry.RightRailX;
        public const float VsiLabelLeftX = FaaClassicVsiGeometry.LabelLeftX;
        public const float VsiLabelWidth = FaaClassicVsiGeometry.LabelWidth;
        private TMP_Text label,detail;private TMP_Text[] scaleLabels;
        public void Configure(Scale which,Color tint)
        {
            kind=which;color=tint;raycastTarget=false;
            label=Text("Signal",which==Scale.Localizer?new Vector2(0,39):new Vector2(0,147),which==Scale.Localizer?220:75,12,tint);
            detail=Text("Value",which==Scale.Localizer?new Vector2(0,-38):new Vector2(0,-150),which==Scale.Localizer?240:100,11,tint);
            if(which==Scale.Glideslope){label.rectTransform.anchoredPosition=new Vector2(-5,123);label.rectTransform.sizeDelta=new Vector2(36,22);detail.rectTransform.anchoredPosition=new Vector2(-12,-132);detail.rectTransform.sizeDelta=new Vector2(48,26);detail.fontSize=9;}
            if(which==Scale.VerticalSpeed){label.rectTransform.anchoredPosition=new Vector2(12,158);detail.rectTransform.anchoredPosition=new Vector2(13,-159);}
            if(which==Scale.VerticalSpeed)
            {
                scaleLabels=new TMP_Text[4];int n=0;
                foreach(int i in new[]{-2,-1,1,2})
                {
                    // Reference design: labels live between tick ends and the right rail.
                    // The pointer follows the outside contour rather than passing over digits.
                    var t=Text("VSI "+i,new Vector2(VsiLabelLeftX+VsiLabelWidth*.5f,i*60),VsiLabelWidth,16,tint);
                    t.alignment=TextAlignmentOptions.Center;t.text=Mathf.Abs(i).ToString();scaleLabels[n++]=t;
                }
            }
        }
        private TMP_Text Text(string name,Vector2 position,float width,float size,Color tint)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(transform,false);var t=go.AddComponent<TextMeshProUGUI>();
            t.font=TMP_Settings.defaultFontAsset;t.fontSize=size;t.color=tint;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
            t.richText=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.rectTransform.anchoredPosition=position;t.rectTransform.sizeDelta=new Vector2(width,25);return t;
        }
        public void Present(float input,bool available,Color tint,string mode=null)
        {
            value=FaaAnalogFlightSample.Finite(input)?input:0;valid=available&&FaaAnalogFlightSample.Finite(input);
            color=new Color(tint.r,tint.g,tint.b,tint.a*(valid?1:.4f));label.color=detail.color=color;
            if(scaleLabels!=null)foreach(var t in scaleLabels)t.color=color;
            label.text=mode??(kind==Scale.Localizer?"LOC":kind==Scale.Glideslope?"G/S":"V/S");
            detail.text=!valid?(kind==Scale.Glideslope?"NO GS":"NO DATA"):kind==Scale.VerticalSpeed?(Mathf.Abs(value)>2000?"OFF SCALE\n":"")+value.ToString("+0;-0;0")+" FPM":
                Mathf.Abs(value)>2.5f?"OFF SCALE":value.ToString("+0.0;-0.0;0.0")+" DOT";
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Color tint=color;
            if(kind==Scale.VerticalSpeed)
            {
                FaaClassicVsiGeometry.DrawScale(vh,tint);
                if(valid)FaaClassicVsiGeometry.DrawPointer(vh,value,tint);
                return;
            }
            Vector2 axis=kind==Scale.Localizer?Vector2.right:Vector2.up;
            for(int i=-2;i<=2;i++)if(i!=0)Ring(vh,axis*i*(kind==Scale.Localizer?48:46),kind==Scale.Localizer?13:6,2,tint,36);
            if(valid)
            {
                Vector2 pos=axis*Mathf.Clamp(value,-2.5f,2.5f)*(kind==Scale.Localizer?48:46);
                Diamond(vh,pos,kind==Scale.Localizer?17:14,kind==Scale.Localizer?30:8,tint);
            }
        }
    }
}
