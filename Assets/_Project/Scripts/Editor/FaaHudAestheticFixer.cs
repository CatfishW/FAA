using FAA.Customization;
using AircraftControl.Core;
using AviationUI;
using CompassNavigatorPro;
using HUDControl.CompassBar;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FAA.EditorTools
{
    public static class FaaHudAestheticFixer
    {
        private static readonly Color HudGreen = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color HudGreenDim = new Color(0.2f, 1f, 0.2f, 0.74f);
        private static readonly Color PanelBackground = new Color(0f, 0.026f, 0.018f, 0.9f);
        private static readonly Vector2 HeadingTapeAnchoredPosition = new Vector2(-610f, 430f);
        private static readonly Vector2 HeadingTapeSize = new Vector2(600f, 38f);
        private const string HeadingTapeCanvasName = "FAAHeadingTapeCanvas";
        private const string HeadingTapeOverlayName = "FAA Heading Tape Overlay";

        [MenuItem("Tools/Aviation/Apply HUD Aesthetic Fixes")]
        public static void Apply()
        {
            ApplyCanvasOrdering();
            ApplyHeadingStripLayout();
            EnsureHeadingTapeOverlay();
            SuppressKronnectCompassBar();
            ApplyFlightHudColor();
            ApplyRadarPanelLayout();
            ApplySymbologyColor();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[FAA HUD] Applied aesthetic fixes for symbology color, heading strip layout, and radar panel spacing.");
        }

        private static void SuppressKronnectCompassBar()
        {
            foreach (CompassPro compass in Resources.FindObjectsOfTypeAll<CompassPro>())
            {
                if (compass == null || !IsSceneObject(compass.gameObject))
                {
                    continue;
                }

                SerializedObject serializedCompass = new SerializedObject(compass);
                SetBool(serializedCompass, "_showCompassBar", false);
                SetBool(serializedCompass, "_showCardinalPoints", false);
                SetBool(serializedCompass, "_showOrdinalPoints", false);
                SetBool(serializedCompass, "_showHalfWinds", false);
                SetFloat(serializedCompass, "_alpha", 0f);
                SetBool(serializedCompass, "_alwaysVisibleInEditMode", false);
                serializedCompass.ApplyModifiedPropertiesWithoutUndo();

                compass.enabled = false;

                foreach (Graphic graphic in compass.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.enabled = false;
                    graphic.raycastTarget = false;
                    EditorUtility.SetDirty(graphic);
                }

                foreach (Canvas canvas in compass.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.enabled = false;
                    EditorUtility.SetDirty(canvas);
                }

                foreach (CanvasGroup group in compass.GetComponentsInChildren<CanvasGroup>(true))
                {
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    EditorUtility.SetDirty(group);
                }

                HideCompassNavigatorChild(compass.transform);
                compass.gameObject.SetActive(false);
                EditorUtility.SetDirty(compass);
                EditorUtility.SetDirty(compass.gameObject);
            }

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsSceneObject(go) || !ShouldSuppressLegacyCompassObject(go))
                {
                    continue;
                }

                SuppressLegacyCompassObject(go);
            }
        }

        private static void HideCompassNavigatorChild(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string lowerName = child.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("compassbar") ||
                    lowerName.StartsWith("cardinal") ||
                    lowerName.StartsWith("intercardinal") ||
                    lowerName.Contains("compass icon"))
                {
                    child.gameObject.SetActive(false);
                    EditorUtility.SetDirty(child.gameObject);
                }
            }
        }

        private static void ApplyCanvasOrdering()
        {
            SetCanvasSorting("FAA_Scene/_UI/FAASymbologyCanvas", 5100, true);
            SetCanvasSorting("FAA_Scene/_UI/FAASymbologyCanvas/Second Interation GUI", 5100, true);
            SetCanvasSorting("XPlaneWeatherIndicatorCanvas", 5000, true);
            SetCanvasSorting("XPlaneWeatherRadarCanvas", 4980, true);
            SetCanvasSorting("XPlaneTrafficRadarCanvas", 4970, true);
        }

        private static void ApplyHeadingStripLayout()
        {
            GameObject headingPanel = FindByPath("FAA_Scene/_UI/FAASymbologyCanvas/Second Interation GUI/Heading Panel");
            if (headingPanel != null)
            {
                headingPanel.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                headingPanel.transform.localScale = Vector3.one;
            }

            GameObject readout = FindByPath("FAA_Scene/_UI/FAASymbologyCanvas/Second Interation GUI/Heading Panel/Compass Readout");
            if (readout != null)
            {
                readout.transform.localPosition = new Vector3(0f, -0.205f, 0f);
                readout.transform.localScale = new Vector3(0.72f, 0.72f, 1f);

                if (readout.TryGetComponent(out Image image))
                {
                    image.color = HudGreen;
                    image.raycastTarget = false;
                    EditorUtility.SetDirty(image);
                }
            }

            GameObject tape = FindByPath("FAA_Scene/_UI/FAASymbologyCanvas/Second Interation GUI/Heading Panel/Compass Bar Generated");
            if (tape == null)
            {
                return;
            }

            if (tape.TryGetComponent(out CompassBarGenerator generator))
            {
                generator.enabled = false;
                SerializedObject serializedGenerator = new SerializedObject(generator);
                SetFloat(serializedGenerator, "pixelsPerDegree", 2.2f);
                SetFloat(serializedGenerator, "tapeHeight", 34f);
                SetInt(serializedGenerator, "repeatCopies", 1);
                SetFloat(serializedGenerator, "majorTickHeight", 14f);
                SetFloat(serializedGenerator, "majorTickWidth", 2f);
                SetColor(serializedGenerator, "majorTickColor", HudGreen);
                SetBool(serializedGenerator, "showMinorTicks", true);
                SetFloat(serializedGenerator, "minorTickHeight", 7f);
                SetFloat(serializedGenerator, "minorTickWidth", 1.5f);
                SetColor(serializedGenerator, "minorTickColor", HudGreenDim);
                SetFloat(serializedGenerator, "labelFontSize", 14f);
                SetFloat(serializedGenerator, "cardinalFontSize", 18f);
                SetColor(serializedGenerator, "labelColor", HudGreen);
                SetColor(serializedGenerator, "cardinalColor", HudGreen);
                SetFloat(serializedGenerator, "labelVerticalOffset", 16f);
                SetInt(serializedGenerator, "labelIntervalDegrees", 30);
                SetBool(serializedGenerator, "useShortFormat", true);
                SetColor(serializedGenerator, "backgroundColor", Color.clear);
                SetBool(serializedGenerator, "showBackground", false);
                serializedGenerator.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(generator);
            }

            SuppressLegacyCompassObject(tape);
        }

        private static void EnsureHeadingTapeOverlay()
        {
            Canvas canvas = EnsureHeadingTapeCanvas();
            GameObject canvasObject = canvas != null ? canvas.gameObject : null;
            if (canvasObject == null)
            {
                return;
            }

            Transform existing = FindSceneObjectByName(HeadingTapeOverlayName)?.transform;
            GameObject overlayObject = existing != null
                ? existing.gameObject
                : new GameObject(HeadingTapeOverlayName, typeof(RectTransform));

            overlayObject.name = HeadingTapeOverlayName;
            overlayObject.transform.SetParent(canvasObject.transform, false);
            overlayObject.transform.SetAsLastSibling();
            overlayObject.SetActive(true);
            RemoveDuplicateHeadingTapeOverlays(overlayObject);

            FaaHeadingTapeOverlay overlay = overlayObject.GetComponent<FaaHeadingTapeOverlay>() ??
                                           overlayObject.AddComponent<FaaHeadingTapeOverlay>();
            overlay.enabled = true;
            overlay.Configure(HeadingTapeAnchoredPosition, HeadingTapeSize, HudGreen, HudGreenDim);
            overlay.SetDataSources(
                FindFirstSceneComponent<AviationFlightDataProvider>(),
                FindFirstSceneComponent<AircraftController>(),
                Camera.main != null ? Camera.main.transform : null);

            EditorUtility.SetDirty(overlay);
            EditorUtility.SetDirty(overlayObject);
        }

        private static Canvas EnsureHeadingTapeCanvas()
        {
            GameObject canvasObject = FindSceneObjectByName(HeadingTapeCanvasName);
            Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                canvasObject = new GameObject(HeadingTapeCanvasName, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
            }

            RemoveDuplicateHeadingTapeCanvases(canvasObject);

            canvasObject.name = HeadingTapeCanvasName;
            canvasObject.transform.SetParent(null, false);
            canvasObject.SetActive(true);
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5200;
            SerializedObject serializedCanvas = new SerializedObject(canvas);
            ApplySerializedBool(serializedCanvas, "m_OverrideSorting", true);
            SerializedProperty sortingOrder = serializedCanvas.FindProperty("m_SortingOrder");
            if (sortingOrder != null)
            {
                sortingOrder.intValue = 5200;
            }
            serializedCanvas.ApplyModifiedPropertiesWithoutUndo();

            RectTransform rectTransform = canvasObject.GetComponent<RectTransform>() ?? canvasObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>() ??
                                                 canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>() ??
                                         canvasObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            EditorUtility.SetDirty(canvasObject);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
            EditorUtility.SetDirty(raycaster);
            return canvas;
        }

        private static void RemoveDuplicateHeadingTapeCanvases(GameObject keep)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsSceneObject(go) || go == keep || go.name != HeadingTapeCanvasName)
                {
                    continue;
                }

                Object.DestroyImmediate(go);
            }
        }

        private static void RemoveDuplicateHeadingTapeOverlays(GameObject keep)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsSceneObject(go) || go == keep || go.name != HeadingTapeOverlayName)
                {
                    continue;
                }

                Object.DestroyImmediate(go);
            }
        }

        private static void ApplyFlightHudColor()
        {
            GameObject hudRoot = FindByPath("FAA_Scene/_UI/FAASymbologyCanvas/Second Interation GUI");
            if (hudRoot == null)
            {
                return;
            }

            foreach (Graphic graphic in hudRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || ShouldSkipHudGraphic(graphic.transform))
                {
                    continue;
                }

                Color color = WithHudAlpha(graphic.color.a);
                graphic.color = color;
                graphic.raycastTarget = false;
                ApplySerializedColor(graphic, "m_Color", color);
                EditorUtility.SetDirty(graphic);
            }

            foreach (TMP_Text text in hudRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || ShouldSkipHudGraphic(text.transform))
                {
                    continue;
                }

                ApplyTmpHudGreen(text);
                EditorUtility.SetDirty(text);
            }

            foreach (Outline outline in hudRoot.GetComponentsInChildren<Outline>(true))
            {
                outline.effectColor = HudGreenDim;
                EditorUtility.SetDirty(outline);
            }

            foreach (Shadow shadow in hudRoot.GetComponentsInChildren<Shadow>(true))
            {
                shadow.effectColor = new Color(0f, 0.08f, 0.02f, 0.72f);
                EditorUtility.SetDirty(shadow);
            }
        }

        private static bool ShouldSkipHudGraphic(Transform transform)
        {
            string path = GetPath(transform).ToLowerInvariant();
            return path.Contains("/compass bar generated") ||
                   path.Contains("/radarcanvas/") ||
                   path.Contains("/maskcanvas/") ||
                   path.Contains("/compassnavigatorpro") ||
                   path.Contains("/visualunderstanding") ||
                   path.Contains("/analysis trigger buttons") ||
                   path.Contains("/vc/") ||
                   path.Contains("/voice");
        }

        private static bool ShouldSuppressLegacyCompassObject(GameObject go)
        {
            string path = GetPath(go.transform).ToLowerInvariant();
            string lowerName = go.name.ToLowerInvariant();
            return lowerName == "compass bar generated" ||
                   lowerName == "faa_compasstape" ||
                   lowerName == "maskcanvas" ||
                   lowerName == "masker" ||
                   lowerName == "compassnavigatorpro" ||
                   path.Contains("/heading panel/compass bar generated") ||
                   path.Contains("/compass bar generated") ||
                   path.Contains("/faa_compasstape") ||
                   path.Contains("/maskcanvas") ||
                   path.Contains("/maskcanvas/") ||
                   path.Contains("/masker/compassnavigatorpro") ||
                   path.Contains("/compassnavigatorpro");
        }

        private static void SuppressLegacyCompassObject(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().FullName ?? string.Empty;
                if (typeName == "HUDControl.CompassBar.CompassBarElement")
                {
                    SetPrivateField(behaviour, "enableTapeScroll", false);
                    SetPrivateField(behaviour, "compassTape", null);
                }
                else if (typeName == "HUDControl.CompassBar.CompassBarGenerator" ||
                         typeName == "CompassBarSystem.CompassTapeGenerator" ||
                         typeName == "CompassBarSystem.CompassBarController" ||
                         typeName == "CompassNavigatorPro.CompassPro")
                {
                    behaviour.enabled = false;
                    EditorUtility.SetDirty(behaviour);
                }
            }

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.enabled = false;
                graphic.raycastTarget = false;
                Color color = graphic.color;
                color.a = 0f;
                graphic.color = color;
                EditorUtility.SetDirty(graphic);
            }

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enabled = false;
                text.raycastTarget = false;
                text.color = new Color(text.color.r, text.color.g, text.color.b, 0f);
                text.canvasRenderer.SetAlpha(0f);
                EditorUtility.SetDirty(text);
            }

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
                EditorUtility.SetDirty(canvas);
            }

            foreach (CanvasGroup group in root.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                EditorUtility.SetDirty(group);
            }

            foreach (CanvasRenderer renderer in root.GetComponentsInChildren<CanvasRenderer>(true))
            {
                renderer.cull = true;
                renderer.SetAlpha(0f);
            }

            root.SetActive(false);
            EditorUtility.SetDirty(root);
        }

        private static Color WithHudAlpha(float currentAlpha)
        {
            Color color = HudGreen;
            color.a = Mathf.Max(currentAlpha, 0.92f);
            return color;
        }

        private static void ApplyTmpHudGreen(TMP_Text text)
        {
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.color = HudGreen;
            if (text.fontSharedMaterial != null)
            {
                text.faceColor = HudGreen;
                text.outlineColor = HudGreenDim;
            }

            text.enableVertexGradient = false;
            text.raycastTarget = false;
            text.canvasRenderer.SetColor(HudGreen);

            SerializedObject serializedText = new SerializedObject(text);
            ApplySerializedColor(serializedText, "m_Color", HudGreen);
            ApplySerializedColor(serializedText, "m_fontColor", HudGreen);
            ApplySerializedColor(serializedText, "m_faceColor", HudGreen);
            ApplySerializedColor(serializedText, "m_outlineColor", HudGreenDim);
            ApplySerializedBool(serializedText, "m_enableVertexGradient", false);
            serializedText.ApplyModifiedPropertiesWithoutUndo();

            text.ForceMeshUpdate(true, true);
        }

        private static void ApplySerializedColor(Object target, string propertyName, Color value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            ApplySerializedColor(serializedObject, propertyName, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplySerializedColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Color)
            {
                property.colorValue = value;
            }
        }

        private static void ApplySerializedBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.Boolean)
            {
                property.boolValue = value;
            }
        }

        private static void ApplyRadarPanelLayout()
        {
            SetRect("XPlaneWeatherRadarCanvas/X-Plane Weather Radar System",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 28f), new Vector2(372f, 372f));
            SetRect("XPlaneWeatherRadarCanvas/WeatherControlStrip",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 406f), new Vector2(176f, 44f));

            SetRect("XPlaneTrafficRadarCanvas/Traffic Radar System",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-28f, 28f), new Vector2(420f, 420f));
            SetRect("XPlaneTrafficRadarCanvas/TrafficControlStrip",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-28f, 456f), new Vector2(226f, 44f));

            ApplyPanelChrome("XPlaneWeatherRadarCanvas/WeatherControlStrip");
            ApplyPanelChrome("XPlaneTrafficRadarCanvas/TrafficControlStrip");
        }

        private static void ApplySymbologyColor()
        {
            foreach (SymbologyColorManager manager in Resources.FindObjectsOfTypeAll<SymbologyColorManager>())
            {
                if (!IsSceneObject(manager.gameObject))
                {
                    continue;
                }

                SerializedObject serializedManager = new SerializedObject(manager);
                SetInt(serializedManager, "currentPreset", (int)ColorPreset.Green);
                SetBool(serializedManager, "preserveElementAlpha", true);
                SetBool(serializedManager, "tintOnlySymbologyElements", true);
                serializedManager.ApplyModifiedPropertiesWithoutUndo();

                manager.RefreshCache();
                manager.SetColorPreset(ColorPreset.Green);
                manager.ApplyColorImmediate(manager.CurrentColor);
                EditorUtility.SetDirty(manager);
            }
        }

        private static void ApplyPanelChrome(string path)
        {
            GameObject go = FindByPath(path);
            if (go == null)
            {
                return;
            }

            if (go.TryGetComponent(out Image image))
            {
                image.color = PanelBackground;
                EditorUtility.SetDirty(image);
            }

            if (go.TryGetComponent(out Outline outline))
            {
                outline.effectColor = new Color(HudGreen.r, HudGreen.g, HudGreen.b, 0.55f);
                outline.effectDistance = new Vector2(1f, -1f);
                EditorUtility.SetDirty(outline);
            }
        }

        private static void SetCanvasSorting(string path, int sortingOrder, bool overrideSorting)
        {
            GameObject go = FindByPath(path);
            if (go == null || !go.TryGetComponent(out Canvas canvas))
            {
                return;
            }

            canvas.overrideSorting = overrideSorting;
            canvas.sortingOrder = sortingOrder;
            EditorUtility.SetDirty(canvas);
        }

        private static void SetRect(string path, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject go = FindByPath(path);
            if (go == null || !go.TryGetComponent(out RectTransform rectTransform))
            {
                return;
            }

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localScale = Vector3.one;
            EditorUtility.SetDirty(rectTransform);
        }

        private static GameObject FindByPath(string path)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!IsSceneObject(go))
                {
                    continue;
                }

                if (GetPath(go.transform) == path)
                {
                    return go;
                }
            }

            return null;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (IsSceneObject(go) && go.name == objectName)
                {
                    return go;
                }
            }

            return null;
        }

        private static T FindFirstSceneComponent<T>() where T : Component
        {
            foreach (T component in Resources.FindObjectsOfTypeAll<T>())
            {
                if (component != null && IsSceneObject(component.gameObject))
                {
                    return component;
                }
            }

            return null;
        }

        private static string GetPath(Transform transform)
        {
            if (transform.parent == null)
            {
                return transform.name;
            }

            return GetPath(transform.parent) + "/" + transform.name;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go);
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetColor(SerializedObject serializedObject, string propertyName, Color value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        private static void SetPrivateField(Component component, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = component.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(component, value);
                EditorUtility.SetDirty(component);
            }
        }
    }
}
