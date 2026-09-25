using System.Collections.Generic;
using UnityEngine;

namespace FAA.Customization
{
    /// <summary>Canvas-local lanes: head/camera pose and utility-panel visibility cannot feed back into flight layout.</summary>
    public sealed class FaaNonConformalReflow
    {
        private readonly IReadOnlyList<FaaNonConformalScaleTarget> modules;
        public FaaNonConformalReflow(IReadOnlyList<FaaNonConformalScaleTarget> registered) { modules = registered; }
        private FaaNonConformalScaleTarget Find(string id)
        {
            foreach (var module in modules) if (module.Id == id) return module;
            return null;
        }
        private Rect Bounds(RectTransform root, string id)
        {
            var module = Find(id);
            return module != null && module.TryLayoutBounds(root, out Rect bounds) ? bounds : Rect.zero;
        }
        public void Apply(Camera view)
        {
            if (view == null) return;
            var canvas = Find("altitude")?.Target.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) return;
            var root = (RectTransform)canvas.transform;
            Rect viewport = root.rect;
            if (viewport.width < 200 || viewport.height < 200) return;
            Rect altitude = Bounds(root, "altitude"), airspeed = Bounds(root, "airspeed"),
                glide = Bounds(root, "glideslope"), vsi = Bounds(root, "vertical-speed"),
                nr = Bounds(root, "nr"), torque = Bounds(root, "torque");
            if (altitude.width <= 0 || airspeed.width <= 0) return;
            float gap = Mathf.Max(8f, viewport.width * .012f);
            float readoutHalf = Mathf.Max(altitude.height, airspeed.height) * .5f;
            float engineHeight = Mathf.Max(nr.height, torque.height);
            float rowY = Mathf.Max(viewport.y + viewport.height * .65f,
                viewport.y + viewport.height * .23f + engineHeight + gap + readoutHalf);
            rowY = Mathf.Min(rowY, viewport.yMax - viewport.height * .12f - readoutHalf);
            float altX = viewport.x + viewport.width * .67f;
            float glideX = altX + altitude.width * .5f + gap + glide.width * .5f;
            float vsiX = glideX + glide.width * .5f + gap + vsi.width * .5f;
            float overflow = Mathf.Max(0, vsiX + vsi.width * .5f - (viewport.xMax - gap));
            altX -= overflow; glideX -= overflow; vsiX -= overflow;
            float airX = viewport.center.x * 2f - altX;
            Find("altitude")?.PlaceLayoutCenter(root, new Vector2(altX, rowY));
            Find("airspeed")?.PlaceLayoutCenter(root, new Vector2(airX, rowY));
            Find("glideslope")?.PlaceLayoutCenter(root, new Vector2(glideX, rowY));
            Find("vertical-speed")?.PlaceLayoutCenter(root, new Vector2(vsiX, rowY));
            float engineTop = rowY - readoutHalf - gap;
            Find("nr")?.PlaceLayoutCenter(root, new Vector2(altX, engineTop - nr.height * .5f));
            Find("torque")?.PlaceLayoutCenter(root, new Vector2(airX, engineTop - torque.height * .5f));
        }
    }
}
