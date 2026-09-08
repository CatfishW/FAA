using UnityEngine;
using UnityEngine.UI;

namespace FAA.Customization
{
    [ExecuteAlways, RequireComponent(typeof(CanvasRenderer))]
    public sealed class FaaSvgIconGraphic : MaskableGraphic
    {
        [SerializeField] private FaaRadarIcon icon;
        private static FaaSvgIconLibrary library;
        public FaaRadarIcon Icon => icon;

        public void SetIcon(FaaRadarIcon value)
        {
            icon = value;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (library == null) library = Resources.Load<FaaSvgIconLibrary>(FaaSvgIconLibrary.ResourcePath);
            var entry = library != null ? library.Find(icon) : null;
            if (entry == null) return;
            float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 24f;
            Vector2 center = rectTransform.rect.center;
            foreach (Vector2 point in entry.vertices)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = center + point * scale;
                vertex.color = color;
                mesh.AddVert(vertex);
            }
            for (int i = 0; i < entry.triangles.Length; i += 3)
                mesh.AddTriangle(entry.triangles[i], entry.triangles[i + 1], entry.triangles[i + 2]);
        }
    }
}
