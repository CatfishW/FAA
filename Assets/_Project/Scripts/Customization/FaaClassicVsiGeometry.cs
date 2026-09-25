using UnityEngine;
using UnityEngine.UI;
using static FAA.Customization.FaaAnalogVector;

namespace FAA.Customization
{
    /// <summary>
    /// Reference-style VSI in local canvas units. Numbers stay INSIDE the rail;
    /// the live pointer follows its outside contour so it cannot cover those numbers.
    /// Geometry changes no telemetry, calibration, camera projection or gesture sizing.
    /// </summary>
    public static class FaaClassicVsiGeometry
    {
        public const float LeftRailX = -13f;
        public const float RightRailX = 15f;
        public const float HalfHeight = 136f;
        public const float RailWidth = 1.6f;
        public const float TickEndX = -2f;
        public const float LabelLeftX = 1f;
        public const float LabelWidth = 10f;
        public const float UnitsToPixels = .06f;
        public const float FullScaleFpm = 2000f;
        public const float PointerGap = 2f;
        public const float PointerHeight = 11f;
        public const float PointerWidth = 30f;
        public const float PointerBodyOffset = 10f;

        public static float ValueToY(float feetPerMinute) =>
            FaaAnalogFlightSample.Finite(feetPerMinute)
                ? Mathf.Clamp(feetPerMinute, -FullScaleFpm, FullScaleFpm) * UnitsToPixels : 0f;

        public static float RightBoundary(float localY)
        {
            if (!FaaAnalogFlightSample.Finite(localY)) return RightRailX;
            float y = Mathf.Abs(localY);
            if (y <= 24f) return LeftRailX + y;
            // Quadratic shoulder: derivative matches both the diagonal and vertical rail.
            if (y < 32f) return RightRailX - (32f-y)*(32f-y)/16f;
            return RightRailX;
        }

        public static Rect PointerBody(float feetPerMinute)
        {
            float y = ValueToY(feetPerMinute);
            float tip = RightBoundary(y) + PointerGap;
            return new Rect(tip+PointerBodyOffset, y-PointerHeight*.5f, PointerWidth, PointerHeight);
        }

        public static void DrawScale(VertexHelper vh, Color tint)
        {
            Line(vh, new Vector2(LeftRailX,-HalfHeight), new Vector2(LeftRailX,HalfHeight), RailWidth,tint);
            Line(vh, new Vector2(LeftRailX,HalfHeight), new Vector2(RightRailX,HalfHeight), RailWidth,tint);
            Line(vh, new Vector2(LeftRailX,-HalfHeight), new Vector2(RightRailX,-HalfHeight), RailWidth,tint);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Line(vh, new Vector2(RightRailX,sign*HalfHeight),new Vector2(RightRailX,sign*32f),RailWidth,tint);
                Vector2 previous = new Vector2(RightRailX,sign*32f);
                for (int i = 1; i <= 12; i++)
                {
                    float y = 32f-i*(8f/12f);
                    Vector2 next = new Vector2(RightBoundary(y),sign*y);
                    Line(vh,previous,next,RailWidth,tint); previous=next;
                }
                Line(vh,previous,new Vector2(LeftRailX,0),RailWidth,tint);
                for (int tick = 1; tick <= 4; tick++)
                    Line(vh,new Vector2(LeftRailX,sign*tick*30f),new Vector2(TickEndX,sign*tick*30f),1.2f,tint);
            }
        }

        public static void DrawPointer(VertexHelper vh, float feetPerMinute, Color tint)
        {
            if (!FaaAnalogFlightSample.Finite(feetPerMinute)) return;
            float y=ValueToY(feetPerMinute), tip=RightBoundary(y)+PointerGap;
            // Detached inward-pointing arrowhead and rounded body match the supplied reference.
            Triangle(vh,new Vector2(tip,y),new Vector2(tip+7f,y+3.8f),new Vector2(tip+7f,y-3.8f),tint);
            FillRoundedBox(vh,PointerBody(feetPerMinute),2.8f,tint);
        }

        private static void FillRoundedBox(VertexHelper vh,Rect box,float radius,Color tint)
        {
            Vector2 center=box.center, previous=Vector2.zero, first=Vector2.zero;
            for (int corner=0; corner<4; corner++)
            {
                Vector2 arcCenter=new Vector2(corner<2?box.xMax-radius:box.xMin+radius,
                    corner==0||corner==3?box.yMax-radius:box.yMin+radius);
                for (int step=0; step<=8; step++)
                {
                    Vector2 point=arcCenter+Bearing(corner*90f+step*11.25f,radius);
                    if(corner==0&&step==0) first=point;
                    else Triangle(vh,center,previous,point,tint);
                    previous=point;
                }
            }
            Triangle(vh,center,previous,first,tint);
        }
    }
}
