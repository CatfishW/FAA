/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements drawing billboard markers
    /// </summary>
    public class MarkerBillboardDrawer : Marker2DMeshDrawer
    {
        /// <summary>
        /// Size of markers
        /// </summary>
        public float marker2DSize
        {
            get { return control.marker2DSize; }
        }

        private Dictionary<int, MarkerBillboard> markerBillboards;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="control">Reference to dynamic mesh control</param>
        public MarkerBillboardDrawer(ControlBaseDynamicMesh control)
        {
            this.control = control;
            map = control.map;
            control.OnDrawMarkers += OnDrawMarkers;
        }

        public override void Dispose()
        {
            base.Dispose();
            control.OnDrawMarkers -= OnDrawMarkers;
            control = null;

            if (markerBillboards != null)
            {
                foreach (KeyValuePair<int, MarkerBillboard> pair in markerBillboards)
                {
                    if (pair.Value != null) pair.Value.Dispose();
                }
            }

            if (markersGameObjects != null) foreach (GameObject go in markersGameObjects) Utils.Destroy(go);

            markerBillboards = null;
            markersGameObjects = null;
            markersMeshes = null;
            markersRenderers = null;
            markerBillboards = null;
        }

        private void OnDrawMarkers()
        {
            if (markersGameObjects == null) InitMarkersMesh(0);
            if (markerBillboards == null) markerBillboards = new Dictionary<int, MarkerBillboard>();

            GeoRect r = map.view.rect.rightFixed;

            Bounds mapBounds = control.bounds;
            Vector3 positionOffset = control.transform.position - mapBounds.min;
            Vector3 size = mapBounds.size;
            size = control.transform.rotation * size;
            if (!control.resultIsTexture) positionOffset.x -= size.x;

            foreach (KeyValuePair<int, MarkerBillboard> billboard in markerBillboards) billboard.Value.used = false;

            foreach (Marker2D marker in control.marker2DManager)
            {
                if (!marker.enabled || !marker.range.Contains(map.view.intZoom)) continue;
                
                GeoPoint p = marker.location;

                if (!r.ContainsWrapped(p)) continue;

                int markerHashCode = marker.GetHashCode();
                MarkerBillboard markerBillboard;

                if (!markerBillboards.TryGetValue(markerHashCode, out markerBillboard))
                {
                    markerBillboard = MarkerBillboard.Create(marker);
                    markerBillboard.transform.parent = markersGameObjects[0].transform;
                    markerBillboard.gameObject.layer = markersGameObjects[0].layer;

                    markerBillboards.Add(markerHashCode, markerBillboard);
                }

                if (!markerBillboard) continue;

                float sx = size.x / map.buffer.renderState.width * marker2DSize * marker.scale;
                float sz = size.z / map.buffer.renderState.height * marker2DSize * marker.scale;
                float s = Mathf.Max(sx, sz);

                markerBillboard.transform.localScale = new Vector3(s, s, s);
                markerBillboard.transform.position = control.LocationToWorld3(p.x, p.y, r);

                markerBillboard.used = true;
            }

            List<int> keysForRemove = new List<int>();

            foreach (KeyValuePair<int, MarkerBillboard> billboard in markerBillboards)
            {
                if (billboard.Value.used) continue;
                billboard.Value.Dispose();
                keysForRemove.Add(billboard.Key);
            }

            foreach (int key in keysForRemove) markerBillboards.Remove(key);
        }
    }
}