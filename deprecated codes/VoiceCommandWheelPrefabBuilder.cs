#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VoiceControl.UI;

namespace VoiceControl.Editor
{
    /// <summary>
    /// Builds the radial voice command wheel prefab following RMF architecture.
    /// Creates smooth round segments with proper center panel layout.
    /// </summary>
    public static class VoiceCommandWheelPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/VoiceControl/VoiceCommandWheel.prefab";

        [MenuItem("Tools/Aviation/Voice Control/Create Command Wheel Prefab", priority = 110)]
        public static void CreatePrefab()
        {
            var root = BuildWheel();
            if (root != null)
            {
                SavePrefab(root);
            }
        }

        [MenuItem("Tools/Aviation/Voice Control/Preview Command Wheel", priority = 111)]
        public static void PreviewInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                CreatePrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            if (prefab == null)
            {
                Debug.LogError("[VoiceCommandWheelPrefabBuilder] Failed to create prefab.");
                return;
            }

            string scenePath = "Assets/VoiceCommandWheelPreview.unity/VoiceCommandWheelPreview.unity";
            if (!File.Exists(scenePath))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            }

            // Setup canvas
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("VoiceCommandCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 110;
                var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Add camera
            Camera sceneCamera = Object.FindObjectOfType<Camera>();
            if (sceneCamera == null)
            {
                GameObject camObj = new GameObject("PreviewCamera");
                sceneCamera = camObj.AddComponent<Camera>();
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(0.08f, 0.10f, 0.12f, 1f);
                camObj.transform.position = new Vector3(0, 0, -10);
            }

            // Instantiate wheel
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
            if (instance != null)
            {
                var rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.zero;
                }
                instance.name = "VoiceCommandWheelInstance";
                Selection.activeGameObject = instance;
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[VoiceCommandWheelPrefabBuilder] Preview scene ready.");
        }

        private static GameObject BuildWheel()
        {
            // Root object
            GameObject root = new GameObject("VoiceCommandWheel", typeof(RectTransform), typeof(VoiceCommandWheelCore));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1000, 1000);

            var wheelCore = root.GetComponent<VoiceCommandWheelCore>();

            // Get sprites
            Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            Sprite bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

            if (knobSprite == null)
            {
                Debug.LogError("[VoiceCommandWheelPrefabBuilder] Failed to load built-in sprites.");
                return null;
            }

            // Colors
            Color normalColor = new Color(0.22f, 0.26f, 0.30f, 0.95f);
            Color highlightColor = new Color(0.45f, 0.85f, 0.55f, 0.98f);
            Color centerColor = new Color(0.15f, 0.18f, 0.22f, 0.98f);
            Color textColor = new Color(0.95f, 0.96f, 0.97f, 1f);
            Color subTextColor = new Color(0.70f, 0.75f, 0.80f, 0.9f);

            // ===== COLLAPSED BUTTON =====
            GameObject collapsed = CreateUIObject("CollapsedButton", root.transform, new Vector2(60, 60));
            var collapsedImg = collapsed.AddComponent<Image>();
            collapsedImg.sprite = knobSprite;
            collapsedImg.type = Image.Type.Sliced;
            collapsedImg.color = normalColor;

            var collapsedBtn = collapsed.AddComponent<Button>();
            collapsedBtn.interactable = true;
            collapsedBtn.targetGraphic = collapsedImg;

            // Set button colors
            var btnColors = collapsedBtn.colors;
            btnColors.normalColor = Color.white;
            btnColors.highlightedColor = new Color(0.8f, 0.9f, 1f, 1f);
            btnColors.pressedColor = new Color(0.6f, 0.8f, 1f, 1f);
            collapsedBtn.colors = btnColors;

            var collapsedCG = collapsed.AddComponent<CanvasGroup>();
            collapsedCG.blocksRaycasts = true;
            collapsedCG.alpha = 1f;
            collapsedCG.interactable = true;

            TMP_Text collapsedLabel = CreateTMP("◉", collapsed.transform, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            collapsedLabel.color = highlightColor;

            wheelCore.collapsedRoot = collapsed.GetComponent<RectTransform>();
            wheelCore.collapsedGroup = collapsedCG;

            // Bind click - use UnityEvent properly
            UnityEngine.Events.UnityAction toggleAction = () => {
                Debug.Log("[VoiceCommandWheel] Button clicked!");
                if (wheelCore != null)
                {
                    wheelCore.ToggleExpanded();
                }
                else
                {
                    Debug.LogError("[VoiceCommandWheel] wheelCore is null!");
                }
            };
            collapsedBtn.onClick.AddListener(toggleAction);

            // ===== EXPANDED ROOT =====
            GameObject expanded = CreateUIObject("ExpandedRoot", root.transform, new Vector2(900, 900));
            var expandedCG = expanded.AddComponent<CanvasGroup>();
            expandedCG.blocksRaycasts = false;

            wheelCore.expandedRoot = expanded.GetComponent<RectTransform>();
            wheelCore.expandedGroup = expandedCG;


            // ===== BACKGROUND RING =====
            GameObject outerRing = CreateUIObject("OuterRing", expanded.transform, new Vector2(720, 720));
            Image outerRingImg = outerRing.AddComponent<Image>();
            outerRingImg.sprite = knobSprite;
            outerRingImg.type = Image.Type.Sliced;
            outerRingImg.color = new Color(normalColor.r * 0.8f, normalColor.g * 0.8f, normalColor.b * 0.8f, 0.9f);

            // ===== SEGMENTS =====
            int segmentCount = 8;
            float sliceAngle = 360f / segmentCount;
            float globalOffset = 22.5f; // Offset so segments are centered

            wheelCore.segments = new List<VoiceCommandSegment>();

            // Create container for segments - larger for better spacing
            GameObject segmentsContainer = CreateUIObject("Segments", expanded.transform, new Vector2(800, 800));

            for (int i = 0; i < segmentCount; i++)
            {
                // Calculate angle for this segment (start from top, go clockwise)
                float angle = (i * sliceAngle) + globalOffset - 90f;
                float rad = angle * Mathf.Deg2Rad;

                // Segment object - larger for better visibility
                GameObject seg = CreateUIObject($"Segment_{i}", segmentsContainer.transform, new Vector2(140, 140));
                RectTransform segRect = seg.GetComponent<RectTransform>();

                // Position at edge of ring - further out
                float ringRadius = 320f;
                segRect.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ringRadius;

                // Background
                Image segBg = seg.AddComponent<Image>();
                segBg.sprite = knobSprite;
                segBg.type = Image.Type.Sliced;
                segBg.color = normalColor;

                // Button
                Button segBtn = seg.AddComponent<Button>();

                // Canvas group
                CanvasGroup segCG = seg.AddComponent<CanvasGroup>();

                // Icon badge (center of segment)
                GameObject iconObj = CreateUIObject("Icon", seg.transform, new Vector2(28, 28));
                RectTransform iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0, 8);
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = knobSprite;
                iconImg.type = Image.Type.Sliced;
                iconImg.color = highlightColor;

                // Label (below icon)
                TMP_Text segLabel = CreateTMP($"{i + 1}", seg.transform, 11, FontStyles.Bold, TextAlignmentOptions.Center);
                RectTransform labelRect = segLabel.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0, 0);
                labelRect.anchorMax = new Vector2(1, 0.4f);
                labelRect.offsetMin = new Vector2(2, 2);
                labelRect.offsetMax = new Vector2(-2, 0);
                segLabel.color = textColor;

                // Add VoiceCommandSegment component
                VoiceCommandSegment vcs = seg.AddComponent<VoiceCommandSegment>();
                vcs.backgroundImage = segBg;
                vcs.button = segBtn;
                vcs.labelText = segLabel as TextMeshProUGUI;
                vcs.canvasGroup = segCG;
                vcs.displayLabel = $"COMMAND {i + 1}";
                vcs.subLabel = "SYSTEM";
                vcs.normalColor = normalColor;
                vcs.highlightColor = highlightColor;

                wheelCore.segments.Add(vcs);
            }

            // ===== CENTER PANEL (single container) =====
            GameObject centerPanel = CreateUIObject("CenterPanel", expanded.transform, new Vector2(460, 460));
            Image centerImg = centerPanel.AddComponent<Image>();
            centerImg.sprite = knobSprite;
            centerImg.type = Image.Type.Sliced;
            centerImg.color = centerColor;

            // Center border
            GameObject centerBorder = CreateUIObject("CenterBorder", centerPanel.transform, new Vector2(445, 445));
            Image borderImg = centerBorder.AddComponent<Image>();
            borderImg.sprite = knobSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.15f);
            centerBorder.transform.SetAsFirstSibling();

            // Title - "COMMAND" at top
            TMP_Text titleLabel = CreateTMP("COMMAND", centerPanel.transform, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRectAnchors(titleLabel.GetComponent<RectTransform>(), 0.15f, 0.82f, 0.85f, 0.90f);
            titleLabel.color = subTextColor;

            // Main label - "SELECT" in middle-top
            TMP_Text mainLabel = CreateTMP("SELECT", centerPanel.transform, 28, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRectAnchors(mainLabel.GetComponent<RectTransform>(), 0.1f, 0.62f, 0.9f, 0.82f);
            mainLabel.color = textColor;
            wheelCore.centerLabel = mainLabel as TextMeshProUGUI;

            // Sub label - "SYSTEM" below main
            TMP_Text subLabel = CreateTMP("SYSTEM", centerPanel.transform, 16, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRectAnchors(subLabel.GetComponent<RectTransform>(), 0.1f, 0.50f, 0.9f, 0.60f);
            subLabel.color = highlightColor;
            wheelCore.centerSubLabel = subLabel as TextMeshProUGUI;

            // Page indicator - below sublabel
            TMP_Text pageText = CreateTMP("1 / 1", centerPanel.transform, 14, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRectAnchors(pageText.GetComponent<RectTransform>(), 0.35f, 0.40f, 0.65f, 0.50f);
            pageText.color = subTextColor;

            // Page nav buttons - compact, below page indicator
            GameObject navContainer = new GameObject("PageNav", typeof(RectTransform));
            navContainer.transform.SetParent(centerPanel.transform, false);
            RectTransform navRect = navContainer.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0.3f, 0.22f);
            navRect.anchorMax = new Vector2(0.7f, 0.32f);
            navRect.offsetMin = Vector2.zero;
            navRect.offsetMax = Vector2.zero;
            navRect.pivot = new Vector2(0.5f, 0.5f);

            var navLayout = navContainer.AddComponent<HorizontalLayoutGroup>();
            navLayout.childAlignment = TextAnchor.MiddleCenter;
            navLayout.spacing = 40;
            navLayout.childControlWidth = false;
            navLayout.childControlHeight = false;
            navLayout.childForceExpandWidth = false;
            navLayout.childForceExpandHeight = false;

            Button prevBtn = CreateNavButton(navContainer.transform, "◀", highlightColor, 24);
            Button nextBtn = CreateNavButton(navContainer.transform, "▶", highlightColor, 24);

            // Stat bars at bottom - clean rectangular bars
            GameObject barsContainer = new GameObject("StatBars", typeof(RectTransform));
            barsContainer.transform.SetParent(centerPanel.transform, false);
            RectTransform barsRect = barsContainer.GetComponent<RectTransform>();
            barsRect.anchorMin = new Vector2(0.15f, 0.06f);
            barsRect.anchorMax = new Vector2(0.85f, 0.18f);
            barsRect.offsetMin = Vector2.zero;
            barsRect.offsetMax = Vector2.zero;

            for (int i = 0; i < 3; i++)
            {
                float yPos = 0.66f - (i * 0.33f);
                GameObject bar = CreateUIObject($"Bar_{i}", barsContainer.transform, new Vector2(0, 6));
                RectTransform barRect = bar.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0, yPos);
                barRect.anchorMax = new Vector2(1, yPos + 0.28f);
                barRect.offsetMin = new Vector2(0, 1);
                barRect.offsetMax = new Vector2(0, -1);

                // Simple background - no sprite, just color
                Image barBg = bar.AddComponent<Image>();
                barBg.sprite = null;
                barBg.type = Image.Type.Simple;
                barBg.color = new Color(0.08f, 0.10f, 0.12f, 0.9f);

                GameObject fillObj = new GameObject("Fill", typeof(RectTransform));
                fillObj.transform.SetParent(bar.transform, false);
                RectTransform fillRect = fillObj.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0.33f * (i + 1), 1);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                // Simple fill - no sprite, just color
                Image fillImg = fillObj.AddComponent<Image>();
                fillImg.sprite = null;
                fillImg.type = Image.Type.Simple;
                fillImg.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.85f);
            }

            // Start in collapsed state
            expanded.SetActive(false);

            return root;
        }

        private static GameObject CreateUIObject(string name, Transform parent, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return go;
        }

        private static TMP_Text CreateTMP(string text, Transform parent, int size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return tmp;
        }

        private static Button CreateNavButton(Transform parent, string label, Color color, float size)
        {
            GameObject btnObj = CreateUIObject("NavButton", parent, new Vector2(size, size));
            Image img = btnObj.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = new Color(color.r, color.g, color.b, 0.15f);

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(color.r, color.g, color.b, 0.35f);
            colors.pressedColor = new Color(color.r, color.g, color.b, 0.55f);
            btn.colors = colors;

            TMP_Text text = CreateTMP(label, btnObj.transform, 11, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = color;

            return btn;
        }

        private static void SetRectAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SavePrefab(GameObject root)
        {
            string folder = Path.GetDirectoryName(PrefabPath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[VoiceCommandWheelPrefabBuilder] Prefab saved to {PrefabPath}");
        }
    }
}
#endif
