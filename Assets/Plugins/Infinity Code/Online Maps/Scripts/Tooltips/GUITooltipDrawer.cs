/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Draws tooltips using IMGUI GUI.
    /// </summary>
    public class GUITooltipDrawer: TooltipDrawerBase
    {
        /// <summary>
        /// Event triggered when drawing a tooltip.
        /// </summary>
        public static Action<GUIStyle, string, Vector2> OnDrawTooltip;

        /// <summary>
        /// Allows you to customize the appearance of the tooltip.
        /// </summary>
        /// <param name="style">The reference to the style.</param>
        public delegate void OnPrepareTooltipStyleDelegate(ref GUIStyle style);

        /// <summary>
        /// Event caused when preparing tooltip style.
        /// </summary>
        public static OnPrepareTooltipStyleDelegate OnPrepareTooltipStyle;

        private static GUIContent content;
        private GUIStyle tooltipStyle;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="map">Reference to the map</param>
        public GUITooltipDrawer(Map map)
        {
            this.map = map;
            control = map.control;

            map.OnGUIAfter += DrawTooltips;

            tooltipStyle = new GUIStyle
            {
                normal =
                {
                    background = map.tooltipBackgroundTexture,
                    textColor = new Color32(230, 230, 230, 255)
                },
                border = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(4, 4, 4, 4),
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                stretchWidth = true,
                padding = new RectOffset(0, 0, 3, 3)
            };
        
            content = new GUIContent();
        }

        ~GUITooltipDrawer()
        {
            map.OnGUIAfter -= DrawTooltips;
            OnPrepareTooltipStyle = null;
            tooltipStyle = null;
            map = null;
            control = null;
        }

        private void DrawTooltips()
        {
            if (string.IsNullOrEmpty(tooltip) && map.showMarkerTooltip != ShowMarkerTooltip.always) return;

            GUIStyle style = new GUIStyle(tooltipStyle);

            if (OnPrepareTooltipStyle != null) OnPrepareTooltipStyle(ref style);

            if (!string.IsNullOrEmpty(tooltip)) InvokeInteractiveElementEvents(style);

            if (map.showMarkerTooltip == ShowMarkerTooltip.always)
            {
                if (control is TileSetControl) DrawTilesetTooltip(style);
                else DrawOtherControlTooltip(style);
            }
        }

        private void DrawOtherControlTooltip(GUIStyle style)
        {
            DrawOtherControlMarker2DTooltip(style);
            DrawOtherControlMarker3DTooltip(style);
        }

        private void DrawOtherControlMarker3DTooltip(GUIStyle style)
        {
            ControlBase3D control3D = control as ControlBase3D;
            if (!control3D) return;
            GeoRect r = map.view.rect.rightFixed;
            Camera cam = control3D.currentCamera;
            Vector3 position = map.transform.position;
            Quaternion rotation = map.transform.rotation;
            Vector3 localScale = map.transform.localScale;
            Vector3 lossyScale = map.transform.lossyScale;
            Vector3 boundsSize = control3D.bounds.size;

            foreach (Marker3D marker in control3D.marker3DManager)
            {
                if (string.IsNullOrEmpty(marker.label)) continue;
                    
                GeoPoint l = marker.location;
                if (!r.ContainsWrapped(l)) continue;

                if (marker.OnDrawTooltip != null) marker.OnDrawTooltip(marker);
                else if (Marker.OnMarkerDrawTooltip != null) Marker.OnMarkerDrawTooltip(marker);
                else
                {
                    Vector2d p = control3D.LocationToLocal(l);

                    double px = (-p.x / control3D.width + 0.5) * boundsSize.x;
                    double pz = (p.y / control3D.height - 0.5) * boundsSize.z;

                    Vector3 offset = rotation * new Vector3((float) px, 0, (float) pz);
                    offset.Scale(lossyScale);

                    Vector3 p1 = position + offset;
                    Vector3 p2 = p1 + new Vector3(0, 0, boundsSize.z / control3D.height * marker.scale);

                    Vector2 screenPoint1 = cam.WorldToScreenPoint(p1);
                    Vector2 screenPoint2 = cam.WorldToScreenPoint(p2);

                    float yOffset = (screenPoint1.y - screenPoint2.y) * localScale.x - 10;

                    OnGUITooltip(style, marker.label, screenPoint1 + new Vector2(0, yOffset));
                }
            }
        }

        private void DrawOtherControlMarker2DTooltip(GUIStyle style)
        {
            Rect screenRect = new Rect(0, 0, Screen.width, Screen.height);
            
            foreach (Marker2D marker in control.marker2DManager)
            {
                if (string.IsNullOrEmpty(marker.label)) continue;

                Rect rect = marker.screenRect;
                if (!screenRect.Overlaps(rect)) continue;
                
                if (marker.OnDrawTooltip != null) marker.OnDrawTooltip(marker);
                else if (Marker.OnMarkerDrawTooltip != null) Marker.OnMarkerDrawTooltip(marker);
                else OnGUITooltip(style, marker.label, new Vector2(rect.x + rect.width / 2, rect.y + rect.height));
            }
        }

        private void DrawTilesetMarker2DTooltips(GUIStyle style, TileSetControl tsControl, float widthScale, float heightScale)
        {
            GeoRect r = map.view.rect.rightFixed;
            Camera cam = tsControl.currentCamera;
            Vector3 localScale = map.transform.localScale;
            
            foreach (Marker2D marker in control.marker2DManager)
            {
                if (string.IsNullOrEmpty(marker.label)) continue;
                
                GeoPoint l = marker.location;
                if (!r.ContainsWrapped(l)) continue;

                if (marker.OnDrawTooltip != null) marker.OnDrawTooltip(marker);
                else if (Marker.OnMarkerDrawTooltip != null) Marker.OnMarkerDrawTooltip(marker);
                else
                {
                    Vector3 pivotPoint = tsControl.LocationToWorld3(l, r);
                    Vector3 centerPoint = pivotPoint;

                    float xOffset = widthScale * marker.width * marker.scale;
                    float zOffset = heightScale * marker.height * marker.scale;

                    if (marker.align == Align.BottomLeft ||
                        marker.align == Align.Left ||
                        marker.align == Align.TopLeft)
                    {
                        centerPoint.x += xOffset;
                    }
                    else if (marker.align == Align.BottomRight ||
                             marker.align == Align.Right ||
                             marker.align == Align.TopRight)
                    {
                        centerPoint.x -= xOffset;
                    }

                    if (marker.align == Align.Top ||
                        marker.align == Align.TopLeft ||
                        marker.align == Align.TopRight)
                    {
                        centerPoint.z += zOffset;
                    }
                    else if (marker.align == Align.BottomLeft ||
                             marker.align == Align.Bottom ||
                             marker.align == Align.BottomRight)
                    {
                        centerPoint.z -= zOffset;
                    }

                    bool useRotation = marker.align != Align.Center && Math.Abs(marker.rotation) > float.Epsilon;
                    if (useRotation) centerPoint = Quaternion.Euler(0, marker.rotation, 0) * (centerPoint - pivotPoint) + pivotPoint;

                    Vector3 topPoint = centerPoint + new Vector3(0, 0, zOffset);

                    if (useRotation) topPoint = Quaternion.Euler(0, -marker.rotation, 0) * (centerPoint - topPoint) + centerPoint;

                    Vector2 screenPoint1 = cam.WorldToScreenPoint(centerPoint);
                    Vector2 screenPoint2 = cam.WorldToScreenPoint(topPoint);

                    float yOffset = (screenPoint1 - screenPoint2).magnitude * localScale.x - 10;

                    OnGUITooltip(style, marker.label, screenPoint1 + new Vector2(0, yOffset));
                }
            }
        }

        private void DrawTilesetMarker3DTooltip(GUIStyle style, TileSetControl tsControl, Vector2 sizeInScene)
        {
            GeoRect r = map.view.rect.rightFixed;
            Camera cam = tsControl.currentCamera;
            Vector3 localScale = map.transform.localScale;
            
            foreach (Marker3D marker in tsControl.marker3DManager)
            {
                if (string.IsNullOrEmpty(marker.label)) continue;
                
                GeoPoint l = marker.location;
                if (!r.ContainsWrapped(l)) continue;

                if (marker.OnDrawTooltip != null) marker.OnDrawTooltip(marker);
                else if (Marker.OnMarkerDrawTooltip != null) Marker.OnMarkerDrawTooltip(marker);
                else
                {
                    Vector3 p1 = tsControl.LocationToWorld3(l, r);
                    Vector3 p2 = p1 + new Vector3(0, 0, sizeInScene.y / tsControl.height * marker.scale);

                    Vector2 screenPoint1 = cam.WorldToScreenPoint(p1);
                    Vector2 screenPoint2 = cam.WorldToScreenPoint(p2);

                    float yOffset = (screenPoint1.y - screenPoint2.y) * localScale.x - 10;

                    OnGUITooltip(style, marker.label, screenPoint1 + new Vector2(0, yOffset));
                }
            }
        }

        private void DrawTilesetTooltip(GUIStyle style)
        {
            TileSetControl tsControl = control as TileSetControl;

            GeoRect r = map.view.rect.rightFixed;
            Vector2 sizeInScene = tsControl.sizeInScene;

            float widthScale = sizeInScene.x / tsControl.width / 2;
            float heightScale = sizeInScene.y / tsControl.height / 2;

            DrawTilesetMarker2DTooltips(style, tsControl, widthScale, heightScale);
            DrawTilesetMarker3DTooltip(style, tsControl, sizeInScene);
        }

        private void InvokeInteractiveElementEvents(GUIStyle style)
        {
            Vector2 inputPosition = InputManager.mousePosition;

            if (tooltipMarker != null)
            {
                if (tooltipMarker.OnDrawTooltip != null) tooltipMarker.OnDrawTooltip(tooltipMarker);
                else if (Marker.OnMarkerDrawTooltip != null) Marker.OnMarkerDrawTooltip(tooltipMarker);
                else OnGUITooltip(style, tooltip, inputPosition);
            }
            else if (tooltipDrawingElement != null)
            {
                if (tooltipDrawingElement.OnDrawTooltip != null) tooltipDrawingElement.OnDrawTooltip(tooltipDrawingElement);
                else if (DrawingElement.OnElementDrawTooltip != null) DrawingElement.OnElementDrawTooltip(tooltipDrawingElement);
                else OnGUITooltip(style, tooltip, inputPosition);
            }
        }

        private void OnGUITooltip(GUIStyle style, string text, Vector2 position)
        {
            if (OnDrawTooltip != null)
            {
                OnDrawTooltip(style, text, position);
                return;
            }

            content.text = text;
            Vector2 size = style.CalcSize(content);
            GUI.Label(new Rect(position.x - size.x / 2 - 5, Screen.height - position.y - size.y - 20, size.x + 10, size.y + 5), text, style);
        }
    }
}