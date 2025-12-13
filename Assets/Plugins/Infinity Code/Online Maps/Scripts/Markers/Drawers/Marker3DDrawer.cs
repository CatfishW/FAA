/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Implements the display of 3D markers
    /// </summary>
    public class Marker3DDrawer : MarkerDrawerBase
    {
        private ControlBase3D control;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="control">Reference to 3D control</param>
        public Marker3DDrawer(ControlBase3D control)
        {
            this.control = control;
            map = control.map;
            control.OnUpdate3DMarkers += Update3DMarkers;
        }

        public override void Dispose()
        {
            base.Dispose();
            control.OnUpdate3DMarkers -= Update3DMarkers;
            control = null;
        }

        private void Update3DMarkers()
        {
            Marker3DManager manager = control.marker3DManager;
            if (!manager || !manager.enabled) return;

            MapView view = map.view;
            GeoRect r = view.rect;
            TileRect tr = view.tileRect;

            long maxTiles = view.maxTiles;

            bool isEntireWorld = map.buffer.renderState.width == maxTiles * Constants.TileSize;
            if (isEntireWorld && Math.Abs(r.left - r.right) < 180)
            {
                if (r.left < 0)
                {
                    r.right += 360;
                    tr.right += maxTiles;
                }
                else
                {
                    r.left -= 360;
                    tr.left -= maxTiles;
                }
            }

            Bounds bounds = control.bounds;
            float bestYScale = ElevationManagerBase.GetElevationScale(r, _elevationManager);
            int zoom = view.intZoom;

            for (int i = manager.count - 1; i >= 0; i--)
            {
                Marker3D marker = manager[i];
                if (marker.manager == null) marker.manager = manager;
                marker.Update(bounds, r, zoom, tr, bestYScale);
            }
        }
    }
}