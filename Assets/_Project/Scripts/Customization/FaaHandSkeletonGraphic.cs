using UnityEngine;
using UnityEngine.UI;
namespace FAA.Customization
{
    /// <summary>Both real detected 21-joint hands on a mirrored local preview; never generated hand poses.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaHandSkeletonGraphic:MaskableGraphic
    {
        private FaaWebcamResult frame;
        private static readonly int[] Links={0,1,1,2,2,3,3,4,0,5,5,6,6,7,7,8,5,9,9,10,10,11,11,12,9,13,13,14,14,15,15,16,13,17,0,17,17,18,18,19,19,20};
        public void SetFrame(FaaWebcamResult value){frame=value;raycastTarget=false;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(frame?.hands==null)return;int index=0;
            foreach(var hand in frame.hands)
            {
                if(!FaaMultiFingerResize.Valid(hand))continue;
                var tint=index++==0?new Color(.25f,1,.72f,1):new Color(.5f,.74f,1,1);
                Vector2 Point(int joint)=>new Vector2((1-hand.landmarks[joint*3])*rectTransform.rect.width+rectTransform.rect.xMin,
                    (1-hand.landmarks[joint*3+1])*rectTransform.rect.height+rectTransform.rect.yMin);
                for(int i=0;i<Links.Length;i+=2)Line(mesh,Point(Links[i]),Point(Links[i+1]),2,tint);
                for(int i=0;i<21;i++)
                {
                    Vector2 center=Point(i);float radius=i==4||i==8||i==12||i==16||i==20?4:2.3f;
                    Line(mesh,center-Vector2.right*radius,center+Vector2.right*radius,radius*2,tint);
                }
            }
        }
        private static void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color color)
        {
            if((a-b).sqrMagnitude<.0001f)return;Vector2 d=(b-a).normalized;Vector2 n=new Vector2(-d.y,d.x)*width*.5f;int s=mesh.currentVertCount;
            mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(s,s+1,s+2);mesh.AddTriangle(s,s+2,s+3);
        }
    }
}
