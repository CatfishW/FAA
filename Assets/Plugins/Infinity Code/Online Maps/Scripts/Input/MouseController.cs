/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Mouse controller for handling user interactions with the map.
    /// </summary>
    [AddComponentMenu("Infinity Code/Online Maps/Plugins/Mouse Controller")]
    [Plugin("Mouse Controller", true)]
    public class MouseController : MonoBehaviour
    {
        /// <summary>
        /// Event, which occurs when the smooth zoom is started.
        /// </summary>
        public Action OnSmoothZoomBegin;

        /// <summary>
        /// Event, which occurs when the smooth zoom is finish.
        /// </summary>
        public Action OnSmoothZoomFinish;

        /// <summary>
        /// Event, which occurs when the smooth zoom is starts init.
        /// </summary>
        public Action OnSmoothZoomInit;

        /// <summary>
        /// Event, which occurs when the smooth zoom is process.
        /// </summary>
        public Action OnSmoothZoomProcess;

        /// <summary>
        /// Event validating that current zoom event is allowed. True - zoom is allowed, false - forbidden.
        /// </summary>
        public Func<ZoomEvent, float, bool> OnValidateZoom;

        /// <summary>
        /// Specifies whether the user can manipulate the map.
        /// </summary>
        [Tooltip("Specifies whether the user can manipulate the map")]
        public bool allowUserControl = true;

        /// <summary>
        /// Specifies whether the user can change zoom of the map.
        /// </summary>
        [Tooltip("Specifies whether the user can change zoom of the map")]
        public bool allowZoom = true;

        /// <summary>
        /// Check that the input position is on the screen.
        /// </summary>
        [Tooltip("Check that the input position is on the screen")]
        public bool checkScreenSizeForWheelZoom = true;

        /// <summary>
        /// Drag marker while holding CTRL.
        /// </summary>
        [Tooltip("Hold CTRL and press on the marker to drag the item.")]
        public bool dragMarkerHoldingCTRL;

        /// <summary>
        /// Delay before invoking event OnMapLongPress.
        /// </summary>
        public float longPressDelay = 1;

        /// <summary>
        /// Distance (pixels) after which will start drag the map.
        /// </summary>
        public float startDragDistance = 4;

        /// <summary>
        /// Allows you to zoom the map when double-clicked.
        /// </summary>
        [Tooltip("Allows you to zoom the map when double-clicked")]
        public bool zoomInOnDoubleClick = true;

        /// <summary>
        /// Mode of zoom.
        /// </summary>
        [Tooltip("Mode of zoom (cursor position or center)")]
        public ZoomMode zoomMode = ZoomMode.target;

        /// <summary>
        /// Sensitivity of the touch zoom
        /// </summary>
        [Tooltip("Sensitivity of the touch zoom")]
        public float zoomSensitivity = 1;

        /// <summary>
        /// Zoom speed with any interaction (wheel, double click, touch)
        /// </summary>
        [Tooltip("Zoom speed with any interaction (wheel, double click, touch)")]
        public float zoomSpeed = 1;
        
        /// <summary>
        /// Gets the last known location of the cursor.
        /// </summary>
        [NonSerialized]
        internal GeoPoint lastCursorLocation;

        private Map map;
        private ControlBase control;

        private IInteractiveElement activeElement;
        private bool isMapPress;
#if UNITY_EDITOR
        private Vector2 initialGestureCenter;
#endif
        private float[] lastClickTimes = { 0, 0 };
        private TilePoint lastCursorTile;
        private Vector2 lastGestureCenter = Vector2.zero;

#pragma warning disable 0414
        private float lastGestureDistance;
#pragma warning restore 0414

        private int lastGestureTouchCount;
        private Vector2 lastInputPosition;
        protected bool lockClick;
        private IEnumerator longPressEnumerator;
        private int lastTouchCount;
        private bool mapDragStarted;
        private Vector2 pressPoint;
        private bool smoothZoomStarted;
        private Vector2[] touchPositions;
        private bool waitZeroTouches;

        /// <summary>
        /// Specifies whether to move map.
        /// </summary>
        public bool isMapDrag { get; set; }

        /// <summary>
        /// Specifies whether the user interacts with the map.
        /// </summary>
        public bool isUserControl { get; set; }

        private void Awake()
        {
            map = GetComponent<Map>();
            if (!map) map = Map.instance;
            if (!map) throw new Exception("Can not find a map.");

            control = map.control;
            control.OnHandleInteraction -= ProcessInteractions;
            control.OnHandleInteraction += ProcessInteractions;
        }

        private static string GetElementName(IInteractiveElement element)
        {
            if (element is Marker)
            {
                Marker marker = element as Marker;
                return !string.IsNullOrEmpty(marker.label) ? marker.label : marker.manager.IndexOf(marker).ToString();
            }

            if (element is DrawingElement)
            {
                DrawingElement drawingElement = element as DrawingElement;
                return drawingElement.manager.IndexOf(drawingElement).ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Method that is called when you press the map.
        /// </summary>
        public void OnPress()
        {
            isMapPress = false;

            if (waitZeroTouches)
            {
                if (InputManager.touchCount <= 1) waitZeroTouches = false;
                else return;
            }

            if (map.blockAllInteractions) return;

            control.marker2DManager.dragMarker = null;
            Vector2 inputPosition = InputManager.mousePosition;
            if (!control.HitTest(inputPosition)) return;
            if (control.IsCursorOnUIElement(inputPosition)) return;

            lastClickTimes[0] = lastClickTimes[1];
            lastClickTimes[1] = Time.realtimeSinceStartup;

            lastInputPosition = pressPoint = inputPosition;
            if (!control.ScreenToTile(inputPosition, out TilePoint t)) return;

            isMapPress = true;

            IInteractiveElement interactiveElement = control.GetInteractiveElement(inputPosition);
            IInteractiveElement pressedElement = null;

            if (interactiveElement != null)
            {
                if (interactiveElement is Marker) pressedElement = ProcessMarkerPress(interactiveElement as Marker);
                else if (interactiveElement is DrawingElement) pressedElement = ProcessDrawingElementPress(interactiveElement as DrawingElement);
            }

            ProcessMapPress(pressedElement);

            activeElement = interactiveElement;

            longPressEnumerator = WaitLongPress();
            StartCoroutine(longPressEnumerator);

            if (allowUserControl) isUserControl = true;
        }

        /// <summary>
        /// Method that is called when you release the map.
        /// </summary>
        public void OnRelease()
        {
            if (waitZeroTouches && InputManager.touchCount == 0) waitZeroTouches = false;
            if (GUIUtility.hotControl != 0 || map.blockAllInteractions) return;

            Vector2 inputPosition = InputManager.mousePosition;
            bool isClick = (pressPoint - inputPosition).sqrMagnitude < 400 && !lockClick;
            lockClick = false;
            isMapDrag = false;

            if (mapDragStarted)
            {
                Log.Info("Stop drag a map", Log.Type.map);
                mapDragStarted = false;
            }

            control.marker2DManager.dragMarker = null;

            StopLongPressCoroutine();

            lastInputPosition = Vector2.zero;
            isUserControl = false;

            if (!isMapPress) return;
            isMapPress = false;

            ProcessRelease(inputPosition, isClick);

            if (map.bufferStatus == BufferStatus.wait) map.needRedraw = true;
        }

        private void ProcessDoubleClick(bool isClick, IInteractiveElement interactiveElement, IInteractiveElement releasedElement, Vector2 inputPosition)
        {
            if (!isClick || Time.realtimeSinceStartup - lastClickTimes[0] > 0.5f) return;

            Marker marker = interactiveElement as Marker;
            DrawingElement drawingElement = interactiveElement as DrawingElement;

            if (marker?.OnDoubleClick != null) ProcessMarkerDoubleClick(marker);
            else if (drawingElement?.OnDoubleClick != null) ProcessDrawingElementDoubleClick(drawingElement);
            else if (releasedElement == null)
            {
                bool zoomIn = !(marker?.OnClick != null || drawingElement?.OnClick != null);
                ProcessMapDoubleClick(inputPosition, zoomIn);
            }

            lastClickTimes[0] = 0;
            lastClickTimes[1] = 0;
        }

        private static void ProcessDrawingElementDoubleClick(DrawingElement drawingElement)
        {
            string elementName = GetElementName(drawingElement);
            Log.Info($"Drawing element {elementName} is double clicked", Log.Type.interactiveElement);
            drawingElement.OnDoubleClick(drawingElement);
        }

        private IInteractiveElement ProcessDrawingElementPress(DrawingElement drawingElement)
        {
            IInteractiveElement pressedElement = null;

            if (drawingElement.OnPress != null)
            {
                string elementName = GetElementName(drawingElement);
                Log.Info($"Drawing element {elementName} is pressed", Log.Type.interactiveElement);
                pressedElement = drawingElement;
                drawingElement.OnPress(drawingElement);
            }

            if (map.showMarkerTooltip == ShowMarkerTooltip.onPress)
            {
                TooltipDrawerBase.tooltipDrawingElement = drawingElement;
                TooltipDrawerBase.tooltip = drawingElement.tooltip;
            }

            return pressedElement;
        }

        private static IInteractiveElement ProcessDrawingElementRelease(DrawingElement element, bool isClick)
        {
            IInteractiveElement releasedElement = null;
            if (element.OnRelease != null)
            {
                string elementName = GetElementName(element);
                Log.Info($"Drawing element {elementName} is released", Log.Type.interactiveElement);
                releasedElement = element;
                element.OnRelease(element);
            }

            if (isClick)
            {
                if (element.OnClick != null)
                {
                    string elementName = GetElementName(element);
                    Log.Info("Drawing element " + elementName + " is clicked", Log.Type.interactiveElement);
                    releasedElement = element;
                    element.OnClick(element);
                }
            }

            return releasedElement;
        }

        private void ProcessInteractions()
        {
            int touchCount = InputManager.touchCount;
            if (touchCount == 0 && InputManager.GetMouseButton(0)) touchCount = 1;

            if (touchCount != lastTouchCount)
            {
                if (touchCount == 1) OnPress();
                else if (touchCount == 0) OnRelease();

                if (lastTouchCount == 0) UpdateLastLocation();
            }

            if (isMapDrag && !smoothZoomStarted) UpdateLocation();

            if (allowZoom)
            {
                UpdateZoom();
                UpdateGestureZoom(touchCount);
            }

            if (control.marker2DManager.dragMarker != null) control.marker2DManager.DragMarker();
            else if (control.HitTest())
            {
                map.tooltipDrawer.ShowMarkersTooltip(InputManager.mousePosition);
            }
            else
            {
                TooltipDrawerBase.tooltip = string.Empty;
                TooltipDrawerBase.tooltipMarker = null;
            }

            lastTouchCount = touchCount;
            control.ScreenToLocationInternal(out lastCursorLocation);
        }

        private void ProcessMapDoubleClick(Vector2 inputPosition, bool zoomIn)
        {
            Log.Info("Map is double clicked", Log.Type.map);
            if (control.OnDoubleClick != null) control.OnDoubleClick();

            if (!allowZoom || !zoomInOnDoubleClick || !zoomIn) return;
            if (OnValidateZoom != null && !OnValidateZoom(ZoomEvent.doubleClick, map.view.zoom + 1)) return;

            if (zoomMode == ZoomMode.target) control.ZoomOnPoint(zoomSpeed, inputPosition);
            else map.view.zoom += zoomSpeed;
        }

        private void ProcessMapPress(IInteractiveElement pressedElement)
        {
            if (pressedElement == null && control.OnPress != null)
            {
                Log.Info("Map is pressed", Log.Type.map);
                control.OnPress();
            }

            if (control.marker2DManager.dragMarker == null) isMapDrag = true;
        }

        private void ProcessMapRelease(bool isClick)
        {
            if (control.OnRelease != null)
            {
                Log.Info("Map is released", Log.Type.map);
                control.OnRelease();
            }

            if (isClick)
            {
                if (control.OnClick != null)
                {
                    Log.Info("Map is clicked", Log.Type.map);
                    control.OnClick();
                }
            }
        }

        private static void ProcessMarkerDoubleClick(Marker marker)
        {
            string elementName = GetElementName(marker);
            Log.Info($"Marker {elementName} is double clicked", Log.Type.interactiveElement);
            marker.OnDoubleClick(marker);
        }

        private IInteractiveElement ProcessMarkerPress(Marker marker)
        {
            IInteractiveElement pressedElement = null;
            if (marker.OnPress != null)
            {
                string elementName = GetElementName(marker);
                Log.Info($"Marker {elementName} is pressed", Log.Type.interactiveElement);
                pressedElement = marker;
                marker.OnPress(marker);
            }

            if (map.showMarkerTooltip == ShowMarkerTooltip.onPress)
            {
                TooltipDrawerBase.tooltipMarker = marker;
                TooltipDrawerBase.tooltip = marker.label;
            }

            if (dragMarkerHoldingCTRL && InputManager.GetKey(KeyCode.LeftControl))
            {
                string elementName = GetElementName(marker);
                Log.Info($"Start drag marker {elementName}", Log.Type.interactiveElement);
                control.marker2DManager.dragMarker = marker;
            }

            return pressedElement;
        }

        private static IInteractiveElement ProcessMarkerRelease(Marker marker, bool isClick)
        {
            IInteractiveElement releasedElement = null;

            if (marker.OnRelease != null)
            {
                string elementName = GetElementName(marker);
                Log.Info($"Marker {elementName} is released", Log.Type.interactiveElement);
                marker.OnRelease(marker);
                releasedElement = marker;
            }

            if (isClick)
            {
                if (marker.OnClick != null)
                {
                    string elementName = GetElementName(marker);
                    Log.Info($"Marker {elementName} is clicked", Log.Type.interactiveElement);
                    marker.OnClick(marker);
                    releasedElement = marker;
                }
            }

            return releasedElement;
        }

        private void ProcessRelease(Vector2 inputPosition, bool isClick)
        {
            IInteractiveElement interactiveElement = control.GetInteractiveElement(inputPosition);
            IInteractiveElement releasedElement = null;

            ResetTooltip();

            if (interactiveElement != null)
            {
                if (interactiveElement is Marker) releasedElement = ProcessMarkerRelease(interactiveElement as Marker, isClick);
                else if (interactiveElement is DrawingElement) releasedElement = ProcessDrawingElementRelease(interactiveElement as DrawingElement, isClick);
            }

            if (releasedElement == null) ProcessMapRelease(isClick);

            if (activeElement != null && activeElement != interactiveElement)
            {
                if (activeElement is Marker) ProcessMarkerRelease(activeElement as Marker, false);
                else if (activeElement is DrawingElement) ProcessDrawingElementRelease(activeElement as DrawingElement, false);
                activeElement = null;
            }

            ProcessDoubleClick(isClick, interactiveElement, releasedElement, inputPosition);
        }

        private void ResetTooltip()
        {
            if (map.showMarkerTooltip == ShowMarkerTooltip.onPress && (TooltipDrawerBase.tooltipMarker != null || TooltipDrawerBase.tooltipDrawingElement != null))
            {
                TooltipDrawerBase.tooltipMarker = null;
                TooltipDrawerBase.tooltipDrawingElement = null;
                TooltipDrawerBase.tooltip = null;
            }
        }

        private void StartGestureZoom()
        {
            if (OnSmoothZoomInit != null) OnSmoothZoomInit();
            smoothZoomStarted = true;
            lastGestureDistance = 0;
            waitZeroTouches = true;
            lockClick = true;

            StopLongPressCoroutine();

            if (OnSmoothZoomBegin != null) OnSmoothZoomBegin();
        }

        private void StopGestureZoom()
        {
            smoothZoomStarted = false;
            lastGestureCenter = Vector2.zero;

#if UNITY_EDITOR
            initialGestureCenter = Vector2.zero;
#endif
            lastGestureDistance = 0;
            if (OnSmoothZoomFinish != null) OnSmoothZoomFinish();
        }

        private void StopLongPressCoroutine()
        {
            if (longPressEnumerator == null) return;

            StopCoroutine(longPressEnumerator);
            longPressEnumerator = null;
        }

        private void UpdateGestureZoom(int touchCount)
        {
#if UNITY_EDITOR
            if (InputManager.GetKey(KeyCode.LeftShift) && InputManager.GetMouseButton(0))
            {
                touchCount = 2;

                if (lastGestureCenter == Vector2.zero)
                {
                    initialGestureCenter = lastGestureCenter = InputManager.mousePosition;
                }

                touchPositions = new Vector2[2];
                touchPositions[0] = InputManager.mousePosition;
                touchPositions[1] = initialGestureCenter * 2 - InputManager.mousePosition;
            }
            else
            {
                if (InputManager.touchSupported)
                {
                    touchPositions = InputManager.touches.Select(t => t.position).ToArray();
                }
                else
                {
                    touchPositions = new Vector2[touchCount];
                    for (int i = 0; i < touchCount; i++) touchPositions[i] = InputManager.mousePosition;
                }
            }
#else
            if (InputManager.OnGetMultitouchInputPositions != null) touchPositions = InputManager.OnGetMultitouchInputPositions();
            else touchPositions = InputManager.touches.Select(t => t.position).ToArray();
#endif

            if (touchCount != lastGestureTouchCount)
            {
                lastGestureTouchCount = touchCount;
                if (touchCount == 2)
                {
                    if (map.notInteractUnderGUI)
                    {
                        Vector2 pos = (touchPositions[0] + touchPositions[1]) / 2;
                        if (control.HitTest(pos) && !control.IsCursorOnUIElement(pos)) StartGestureZoom();
                    }
                    else StartGestureZoom();
                }
                else if (smoothZoomStarted) StopGestureZoom();
            }

            if (smoothZoomStarted) UpdateGestureZoomValue();
        }

        private void UpdateGestureZoomValue()
        {
#if UNITY_EDITOR
            float delta = (touchPositions[0].x - lastGestureCenter.x) / 100;

            if (Mathf.Abs(delta) < 0.01f) return;

            lastGestureCenter = touchPositions[0];
#else
            float distance = (touchPositions[0] - touchPositions[1]).magnitude;

            if (Math.Abs(lastGestureDistance) < float.Epsilon)
            {
                lastGestureDistance = distance;
                return;
            }
            float delta = distance / lastGestureDistance - 1;

            if (Mathf.Abs(delta) < 0.01f) return;
            lastGestureDistance = distance;
#endif

            Vector2 screenPosition = Vector2.zero;
            foreach (Vector2 touchPosition in touchPositions) screenPosition += touchPosition;
            screenPosition /= touchPositions.Length;

            delta *= zoomSpeed;

            if (OnValidateZoom == null || OnValidateZoom(ZoomEvent.gesture, map.view.zoom + delta))
            {
                if (zoomMode == ZoomMode.target) control.ZoomOnPoint(delta * zoomSensitivity, screenPosition);
                else map.view.zoom += delta * zoomSensitivity;
            }


            if (OnSmoothZoomProcess != null) OnSmoothZoomProcess();
        }

        /// <summary>
        /// Force updates the latest coordinates of cursor.
        /// </summary>
        public void UpdateLastLocation()
        {
            lastInputPosition = InputManager.mousePosition;
            if (!control.ScreenToTileInternal(lastInputPosition, out TilePoint t)) return;
            lastCursorTile = t;
            lastCursorLocation = lastCursorTile.ToLocation(map);
        }

        /// <summary>
        /// Updates the map coordinates for the actions of the user.
        /// </summary>
        protected void UpdateLocation()
        {
            if (!allowUserControl || InputManager.touchCount > 1) return;

            Vector2 inputPosition = InputManager.mousePosition;

            if (!mapDragStarted && (pressPoint - inputPosition).sqrMagnitude < startDragDistance * startDragDistance) return;
            if (!mapDragStarted)
            {
                Log.Info("Start drag a map", Log.Type.map);
                mapDragStarted = true;
            }

            if (lastInputPosition == inputPosition) return;

            if (!control.ScreenToLocationInternal(out GeoPoint p)) return;

            Vector2d offset = p - lastCursorLocation;

            if (offset.x > 270) offset.x -= 360;
            else if (offset.x < -270) offset.x += 360;

            if (Math.Abs(offset.sqrMagnitude) <= double.Epsilon) return;

            map.view.center -= offset;
            map.needRedraw = true;

            StopLongPressCoroutine();

            if (control.OnDrag != null)
            {
                Log.Info("Map is being dragged", Log.Type.map);
                control.OnDrag();
            }
        }

        /// <summary>
        /// Updates the map zoom for mouse wheel.
        /// </summary>
        protected void UpdateZoom()
        {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            return;
#else
            if (!allowUserControl) return;
            if (!control.HitTest()) return;

            Vector2 inputPosition = InputManager.mousePosition;

            if (control.IsCursorOnUIElement(inputPosition)) return;
            if (checkScreenSizeForWheelZoom && (inputPosition.x <= 0 || inputPosition.x >= Screen.width || inputPosition.y <= 0 || inputPosition.y >= Screen.height)) return;

            float wheel = InputManager.GetAxis("Mouse ScrollWheel");
            if (Math.Abs(wheel) < float.Epsilon) return;

#if NETFX_CORE
            wheel = -wheel;
#endif

            float delta = wheel > 0 ? zoomSpeed : -zoomSpeed;
            if (OnValidateZoom == null || OnValidateZoom(ZoomEvent.wheel, map.view.zoom + delta))
            {
                if (zoomMode == ZoomMode.target) control.ZoomOnPoint(delta, inputPosition);
                else map.view.zoom += delta;
            }
#endif
        }

        private IEnumerator WaitLongPress()
        {
            yield return new WaitForSeconds(longPressDelay);

            Marker marker = null;
            DrawingElement drawingElement = null;
            Vector2 inputPosition = InputManager.mousePosition;

            IInteractiveElement interactiveElement = control.GetInteractiveElement(inputPosition);

            if (interactiveElement != null)
            {
                if (interactiveElement is Marker) marker = interactiveElement as Marker;
                else if (interactiveElement is DrawingElement) drawingElement = interactiveElement as DrawingElement;
            }

            if (marker?.OnLongPress != null)
            {
                string markerName = !string.IsNullOrEmpty(marker.label) ? marker.label : marker.manager.IndexOf(marker).ToString();
                Log.Info("Marker " + markerName + " is long pressed", Log.Type.interactiveElement);

                marker.OnLongPress(marker);
            }
            else if (drawingElement?.OnLongPress != null)
            {
                string elementName = drawingElement.manager.IndexOf(drawingElement).ToString();
                Log.Info("Drawing element " + elementName + " is pressed", Log.Type.interactiveElement);

                drawingElement.OnLongPress(drawingElement);
            }
            else if (control.OnLongPress != null)
            {
                Log.Info("Map is long pressed", Log.Type.map);

                control.OnLongPress();
                isMapDrag = false;
            }

            longPressEnumerator = null;
        }
    }
}