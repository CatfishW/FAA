using UnityEngine;
using UnityEngine.UI;

namespace FAA.Explanations
{
    /// <summary>Resolution-independent line glyphs for workspace actions.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ExplanationGlyph : MaskableGraphic
    {
        public enum Kind { Spark, Send, Stop, Minus, Expand }
        private Kind kind;
        public void Set(Kind value) { if (kind == value) return; kind = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (kind == Kind.Spark)
            {
                Line(mesh, 12, 2, 15, 9); Line(mesh, 15, 9, 22, 12); Line(mesh, 22, 12, 15, 15); Line(mesh, 15, 15, 12, 22);
                Line(mesh, 12, 22, 9, 15); Line(mesh, 9, 15, 2, 12); Line(mesh, 2, 12, 9, 9); Line(mesh, 9, 9, 12, 2);
            }
            else if (kind == Kind.Send) { Line(mesh, 4, 5, 21, 12); Line(mesh, 21, 12, 4, 19); Line(mesh, 4, 19, 7, 12); Line(mesh, 7, 12, 4, 5); Line(mesh, 8, 12, 17, 12); }
            else if (kind == Kind.Stop) { Line(mesh, 6, 6, 18, 6); Line(mesh, 18, 6, 18, 18); Line(mesh, 18, 18, 6, 18); Line(mesh, 6, 18, 6, 6); }
            else if (kind == Kind.Minus) Line(mesh, 5, 12, 19, 12);
            else { Line(mesh, 4, 9, 4, 4); Line(mesh, 4, 4, 9, 4); Line(mesh, 15, 4, 20, 4); Line(mesh, 20, 4, 20, 9); Line(mesh, 4, 15, 4, 20); Line(mesh, 4, 20, 9, 20); Line(mesh, 15, 20, 20, 20); Line(mesh, 20, 20, 20, 15); }
        }
        private void Line(VertexHelper mesh, float x1, float y1, float x2, float y2)
        {
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 24;
            Vector2 center = rectTransform.rect.center;
            Vector2 a = center + new Vector2(x1 - 12, 12 - y1) * scale, b = center + new Vector2(x2 - 12, 12 - y2) * scale;
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * .8f * scale;
            int index = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero); mesh.AddVert(a + normal, color, Vector2.zero); mesh.AddVert(b + normal, color, Vector2.zero); mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
