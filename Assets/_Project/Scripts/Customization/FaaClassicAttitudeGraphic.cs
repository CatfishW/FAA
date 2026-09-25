using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaAnalogVector;
namespace FAA.Customization
{
    /// <summary>Head-fixed attitude instrument. Its pixel pitch spacing is NOT conformal angular projection.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaClassicAttitudeGraphic:MaskableGraphic
    {
        private readonly TMP_Text[] labels=new TMP_Text[9];
        private TMP_Text flag;
        private float pitch,roll;
        private bool valid;
        public void Configure(Color tint)
        {
            color=tint;raycastTarget=false;
            for(int i=0;i<labels.Length;i++)
            {
                var go=new GameObject("Pitch "+i,typeof(RectTransform));go.transform.SetParent(transform,false);
                var t=go.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.fontSize=22;t.alignment=TextAlignmentOptions.Center;
                t.color=tint;t.raycastTarget=false;t.rectTransform.sizeDelta=new Vector2(55,28);labels[i]=t;
            }
            var status=new GameObject("Attitude source",typeof(RectTransform));status.transform.SetParent(transform,false);
            flag=status.AddComponent<TextMeshProUGUI>();flag.font=TMP_Settings.defaultFontAsset;flag.fontSize=11;flag.alignment=TextAlignmentOptions.Center;
            flag.raycastTarget=false;flag.rectTransform.anchoredPosition=new Vector2(0,-130);flag.rectTransform.sizeDelta=new Vector2(250,20);
        }
        public void Present(float newPitch,float newRoll,bool isValid,Color tint)
        {
            pitch=newPitch;roll=newRoll;valid=isValid&&FaaAnalogFlightSample.Finite(pitch)&&FaaAnalogFlightSample.Finite(roll);
            if(!valid){pitch=0;roll=0;}
            color=tint;flag.color=tint;flag.text=valid?"":"ATTITUDE UNAVAILABLE";
            int center=Mathf.RoundToInt(pitch/5)*5;Quaternion rotation=Quaternion.Euler(0,0,roll);
            for(int i=0;i<labels.Length;i++)
            {
                int degrees=center+(i-4)*5;float y=(degrees-pitch)*10.6f;var t=labels[i];
                bool visible=valid&&degrees!=0&&Mathf.Abs(degrees)<=90&&Mathf.Abs(y)<142;
                if(t.gameObject.activeSelf!=visible)t.gameObject.SetActive(visible);
                if(!visible)continue;
                t.text=degrees.ToString();t.color=tint;t.rectTransform.anchoredPosition=rotation*new Vector3(0,y,0);
                t.rectTransform.localRotation=rotation;
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!valid)return;
            Quaternion rotation=Quaternion.Euler(0,0,roll);
            int center=Mathf.RoundToInt(pitch/5)*5;
            for(int i=-4;i<=4;i++)
            {
                int degrees=center+i*5;if(Mathf.Abs(degrees)>90)continue;
                float y=(degrees-pitch)*10.6f;
                if(Mathf.Abs(y)>145)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    if(degrees>=0)RotatedLine(vh,rotation,new Vector2(side*28,y),new Vector2(side*112,y),2,color);
                    else for(int dash=0;dash<4;dash++)RotatedLine(vh,rotation,new Vector2(side*(28+dash*22),y),new Vector2(side*(44+dash*22),y),2,color);
                    float hook=degrees>=0?-6:6;
                    RotatedLine(vh,rotation,new Vector2(side*112,y),new Vector2(side*112,y+hook),1.5f,color);
                }
            }
            // Fixed aircraft boresight is an instrument reference, not a flight-path vector.
            Line(vh,new Vector2(-14,-6),Vector2.zero,2,color);Line(vh,Vector2.zero,new Vector2(14,-6),2,color);
        }
        private static void RotatedLine(VertexHelper vh,Quaternion rotation,Vector2 a,Vector2 b,float width,Color color)
        {Line(vh,rotation*(Vector3)a,rotation*(Vector3)b,width,color);}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaClassicBankGraphic:MaskableGraphic
    {
        private float bank,slip;private bool valid,slipValid;
        public void Present(float angle,float side,bool attitudeValid,bool validSlip,Color tint)
        {bank=angle;slip=side;valid=attitudeValid;slipValid=validSlip;color=tint;raycastTarget=false;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Vector2 c=new Vector2(0,-340);Color tint=color;
            if(!valid)tint.a*=.35f;
            for(int i=0;i<108;i++)Line(vh,c+Bearing(-54+i,420),c+Bearing(-53+i,420),3.3f,tint);
            foreach(float a in new[]{-50f,-35f,-25f,-10f,10f,25f,35f,50f})Line(vh,c+Bearing(a,403),c+Bearing(a,438),2.5f,tint);
            foreach(float a in new[]{-54f,54f})
            {
                Vector2 tip=c+Bearing(a,420),r=Bearing(a+90,10),outside=c+Bearing(a,439);
                Line(vh,tip,outside-r,2,tint);Line(vh,outside-r,outside+r,2,tint);Line(vh,outside+r,tip,2,tint);
            }
            if(!valid)return;
            float angle=Mathf.Clamp(Mathf.DeltaAngle(0,bank),-60,60)*.9f;
            Vector2 tip2=c+Bearing(angle,426),base2=c+Bearing(angle,459),right=Bearing(angle+90,15);
            Line(vh,tip2,base2-right,2,tint);Line(vh,base2-right,base2+right,2,tint);Line(vh,base2+right,tip2,2,tint);
            Triangle(vh,new Vector2(0,72),new Vector2(-17,42),new Vector2(17,42),tint);
            if(slipValid)Line(vh,new Vector2(-14+slip*19,32),new Vector2(14+slip*19,32),6,tint);
        }
    }
}
