using UnityEngine;

namespace FAA.Customization
{
    /// <summary>Transform UI geometry without round-tripping through a moving camera or large world coordinates.</summary>
    public static class FaaCanvasLocalGeometry
    {
        public static bool TryMatrix(Transform child, Transform ancestor, out Matrix4x4 matrix)
        {
            matrix = Matrix4x4.identity;
            if (child == null || ancestor == null) return false;
            for (int depth = 0; child != null && depth < 128; depth++, child = child.parent)
            {
                if (child == ancestor) return true;
                matrix = Matrix4x4.TRS(child.localPosition,child.localRotation,child.localScale) * matrix;
            }
            return false;
        }
        public static Rect TransformRect(Rect rect, Matrix4x4 matrix)
        {
            Vector2 minimum = new Vector2(float.PositiveInfinity,float.PositiveInfinity), maximum = -minimum;
            for (int i=0;i<4;i++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3((i & 1)==0 ? rect.xMin : rect.xMax, (i & 2)==0 ? rect.yMin : rect.yMax,0));
                minimum=Vector2.Min(minimum,point); maximum=Vector2.Max(maximum,point);
            }
            return Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
        }
    }
}
