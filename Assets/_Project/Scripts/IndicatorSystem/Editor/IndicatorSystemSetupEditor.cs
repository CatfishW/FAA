using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using IndicatorSystem.Core;
using IndicatorSystem.Display;
using IndicatorSystem.Controller;
using IndicatorSystem.Integration;
using TrafficRadar.Core;
using TrafficRadar;
using WeatherRadar;

namespace IndicatorSystem.Editor
{
    /// <summary>
    /// Editor utilities for one-click indicator system setup.
    /// Auto-generates distinct prefabs for each aircraft type.
    /// </summary>
    public static class IndicatorSystemSetupEditor
    {
        private const string SettingsPath = "Assets/_Project/Scripts/IndicatorSystem/Settings/IndicatorSettings.asset";
        private const string PrefabsPath = "Assets/_Project/Scripts/IndicatorSystem/Prefabs";
        
        [MenuItem("Tools/Indicator System/Setup Indicator System", priority = 1)]
        public static void SetupIndicatorSystem()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Setup Error", "Cannot setup while in Play mode.", "OK");
                return;
            }
            
            // Check for existing system
            var existingController = Object.FindObjectOfType<IndicatorSystemController>();
            if (existingController != null)
            {
                if (!EditorUtility.DisplayDialog("Indicator System Exists",
                    "An Indicator System already exists in the scene. Do you want to create another one?",
                    "Create New", "Cancel"))
                {
                    Selection.activeGameObject = existingController.gameObject;
                    return;
                }
            }
            
            // Create settings asset if needed
            IndicatorSettings settings = EnsureSettingsAsset();
            
            // Create prefabs for each aircraft type
            CreateAircraftTypePrefabs(settings);
            
            // Create main system object
            GameObject systemRoot = new GameObject("[Indicator System]");
            Undo.RegisterCreatedObjectUndo(systemRoot, "Create Indicator System");
            
            // Add controller
            var controller = systemRoot.AddComponent<IndicatorSystemController>();
            SetPrivateField(controller, "settings", settings);
            
            // Add bridges
            var trafficBridge = systemRoot.AddComponent<TrafficIndicatorBridge>();
            var weatherBridge = systemRoot.AddComponent<WeatherIndicatorBridge>();
            
            // Try to find and link existing radar components
            var trafficController = Object.FindObjectOfType<TrafficRadarController>();
            if (trafficController != null)
            {
                SetPrivateField(trafficBridge, "trafficRadarController", trafficController);
                Debug.Log($"[IndicatorSystem] Linked to TrafficRadarController: {trafficController.gameObject.name}");
            }
            
            var weatherProvider = Object.FindObjectOfType<WeatherRadarProviderBase>();
            if (weatherProvider != null)
            {
                SetPrivateField(weatherBridge, "weatherProvider", weatherProvider);
                Debug.Log($"[IndicatorSystem] Linked to WeatherRadarProviderBase: {weatherProvider.gameObject.name}");
            }
            
            // Set reference to controller
            SetPrivateField(trafficBridge, "indicatorController", controller);
            SetPrivateField(weatherBridge, "indicatorController", controller);
            
            // Select the created object
            Selection.activeGameObject = systemRoot;
            
            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            
            Debug.Log("[IndicatorSystem] Setup complete! The system will auto-initialize at runtime.");
            
            EditorUtility.DisplayDialog("Setup Complete",
                $"Indicator System created successfully!\n\n" +
                $"Traffic Controller: {(trafficController != null ? "Linked" : "Not found - link manually")}\n" +
                $"Weather Provider: {(weatherProvider != null ? "Linked" : "Not found - link manually")}\n\n" +
                $"Settings: {SettingsPath}\n" +
                $"Prefabs created in: {PrefabsPath}",
                "OK");
        }
        
        [MenuItem("Tools/Indicator System/Create Aircraft Prefabs", priority = 2)]
        public static void CreateAircraftPrefabsMenu()
        {
            var settings = EnsureSettingsAsset();
            CreateAircraftTypePrefabs(settings);
            EditorUtility.DisplayDialog("Prefabs Created", 
                $"Aircraft indicator prefabs created in:\n{PrefabsPath}", "OK");
        }
        
        [MenuItem("Tools/Indicator System/Create Settings Asset", priority = 3)]
        public static void CreateSettingsAsset()
        {
            var settings = EnsureSettingsAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
        
        [MenuItem("Tools/Indicator System/Find Indicator System", priority = 4)]
        public static void FindIndicatorSystem()
        {
            var controller = Object.FindObjectOfType<IndicatorSystemController>();
            if (controller != null)
            {
                Selection.activeGameObject = controller.gameObject;
                EditorGUIUtility.PingObject(controller.gameObject);
            }
            else
            {
                EditorUtility.DisplayDialog("Not Found",
                    "No Indicator System found in the scene.\n\nUse Tools > Indicator System > Setup to create one.",
                    "OK");
            }
        }
        
        [MenuItem("Tools/Indicator System/Documentation", priority = 100)]
        public static void OpenDocumentation()
        {
            string readmePath = "Assets/_Project/Scripts/IndicatorSystem/README.md";
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(readmePath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
            else
            {
                EditorUtility.DisplayDialog("Documentation",
                    "README.md not found at expected path.\n\n" +
                    "The Indicator System provides on/off screen indicators for traffic and weather radar targets.\n\n" +
                    "Quick Start:\n" +
                    "1. Tools > Indicator System > Setup\n" +
                    "2. Link radar components in Inspector\n" +
                    "3. Enter Play mode to see indicators",
                    "OK");
            }
        }
        
        #region Prefab Creation
        
        private static void CreateAircraftTypePrefabs(IndicatorSettings settings)
        {
            // Ensure prefabs directory exists
            EnsureDirectory(PrefabsPath);
            
            // Create prefabs for each type
            var defaultPrefab = CreateIndicatorPrefab("IndicatorDefault", 
                new Color(0f, 1f, 1f, 1f), // Cyan
                IndicatorShape.Diamond);
            
            var commercialPrefab = CreateIndicatorPrefab("IndicatorCommercial", 
                new Color(0.2f, 0.6f, 1f, 1f), // Blue
                IndicatorShape.Airliner);
            
            var militaryPrefab = CreateIndicatorPrefab("IndicatorMilitary", 
                new Color(0.8f, 0f, 0f, 1f), // Red
                IndicatorShape.Fighter);
            
            var generalPrefab = CreateIndicatorPrefab("IndicatorGeneral", 
                new Color(0f, 1f, 0.4f, 1f), // Green
                IndicatorShape.SmallPlane);
            
            var helicopterPrefab = CreateIndicatorPrefab("IndicatorHelicopter", 
                new Color(1f, 0.8f, 0f, 1f), // Orange/Yellow
                IndicatorShape.Helicopter);
            
            var unknownPrefab = CreateIndicatorPrefab("IndicatorUnknown", 
                new Color(0.7f, 0.7f, 0.7f, 1f), // Gray
                IndicatorShape.Circle);
            
            // Assign to settings
            settings.useCustomPrefabs = true;
            settings.defaultIndicatorPrefab = defaultPrefab;
            settings.commercialPrefab = commercialPrefab;
            settings.militaryPrefab = militaryPrefab;
            settings.generalPrefab = generalPrefab;
            settings.helicopterPrefab = helicopterPrefab;
            settings.unknownPrefab = unknownPrefab;
            
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[IndicatorSystem] Created aircraft type prefabs");
        }
        
        private enum IndicatorShape
        {
            Diamond,
            Airliner,
            Fighter,
            SmallPlane,
            Helicopter,
            Circle
        }
        
        private static GameObject CreateIndicatorPrefab(string name, Color baseColor, IndicatorShape shape)
        {
            // Create root
            GameObject root = new GameObject(name);
            RectTransform rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 80);
            
            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            IndicatorElement element = root.AddComponent<IndicatorElement>();
            
            // === MAIN SYMBOL (On-Screen) ===
            GameObject symbolObj = new GameObject("Symbol");
            symbolObj.transform.SetParent(root.transform, false);
            RectTransform symbolRt = symbolObj.AddComponent<RectTransform>();
            symbolRt.anchoredPosition = Vector2.zero;
            symbolRt.sizeDelta = new Vector2(40, 40);
            
            Image symbolImg = symbolObj.AddComponent<Image>();
            symbolImg.color = baseColor;
            CreateShapeSprite(symbolImg, shape);
            
            // Add outline effect for visibility
            var outline = symbolObj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            
            // === OFF-SCREEN ARROW ===
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(root.transform, false);
            RectTransform arrowRt = arrowObj.AddComponent<RectTransform>();
            arrowRt.anchoredPosition = Vector2.zero;
            arrowRt.sizeDelta = new Vector2(30, 30);
            
            Image arrowImg = arrowObj.AddComponent<Image>();
            arrowImg.color = baseColor;
            CreateArrowSprite(arrowImg);
            
            var arrowOutline = arrowObj.AddComponent<Outline>();
            arrowOutline.effectColor = new Color(0, 0, 0, 0.8f);
            arrowOutline.effectDistance = new Vector2(1f, -1f);
            arrowObj.SetActive(false);
            
            // === CALLSIGN LABEL ===
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(root.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(0, 28);
            labelRt.sizeDelta = new Vector2(80, 18);
            
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 11;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.enableAutoSizing = false;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            
            // Add background for label
            GameObject labelBg = new GameObject("LabelBackground");
            labelBg.transform.SetParent(labelObj.transform, false);
            labelBg.transform.SetAsFirstSibling();
            RectTransform labelBgRt = labelBg.AddComponent<RectTransform>();
            labelBgRt.anchorMin = Vector2.zero;
            labelBgRt.anchorMax = Vector2.one;
            labelBgRt.offsetMin = new Vector2(-4, -2);
            labelBgRt.offsetMax = new Vector2(4, 2);
            
            Image labelBgImg = labelBg.AddComponent<Image>();
            labelBgImg.color = new Color(0, 0, 0, 0.6f);
            
            // === DISTANCE TEXT ===
            GameObject distObj = new GameObject("Distance");
            distObj.transform.SetParent(root.transform, false);
            RectTransform distRt = distObj.AddComponent<RectTransform>();
            distRt.anchoredPosition = new Vector2(0, -28);
            distRt.sizeDelta = new Vector2(50, 16);
            
            TextMeshProUGUI distText = distObj.AddComponent<TextMeshProUGUI>();
            distText.fontSize = 10;
            distText.alignment = TextAlignmentOptions.Center;
            distText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            // Distance background
            GameObject distBg = new GameObject("DistBackground");
            distBg.transform.SetParent(distObj.transform, false);
            distBg.transform.SetAsFirstSibling();
            RectTransform distBgRt = distBg.AddComponent<RectTransform>();
            distBgRt.anchorMin = Vector2.zero;
            distBgRt.anchorMax = Vector2.one;
            distBgRt.offsetMin = new Vector2(-4, -1);
            distBgRt.offsetMax = new Vector2(4, 1);
            
            Image distBgImg = distBg.AddComponent<Image>();
            distBgImg.color = new Color(0, 0, 0, 0.5f);
            
            // === ALTITUDE TEXT (+1, -3, 0) ===
            GameObject altObj = new GameObject("Altitude");
            altObj.transform.SetParent(root.transform, false);
            RectTransform altRt = altObj.AddComponent<RectTransform>();
            altRt.anchoredPosition = new Vector2(40, 0);
            altRt.sizeDelta = new Vector2(40, 24);
            
            TextMeshProUGUI altText = altObj.AddComponent<TextMeshProUGUI>();
            altText.fontSize = 12;
            altText.fontStyle = FontStyles.Bold;
            altText.alignment = TextAlignmentOptions.Left;
            altText.color = Color.white;
            altText.text = "+0";
            
            // Altitude background
            GameObject altBg = new GameObject("AltBackground");
            altBg.transform.SetParent(altObj.transform, false);
            altBg.transform.SetAsFirstSibling();
            RectTransform altBgRt = altBg.AddComponent<RectTransform>();
            altBgRt.anchorMin = Vector2.zero;
            altBgRt.anchorMax = Vector2.one;
            altBgRt.offsetMin = new Vector2(-3, -1);
            altBgRt.offsetMax = new Vector2(3, 1);
            
            Image altBgImg = altBg.AddComponent<Image>();
            altBgImg.color = new Color(0, 0, 0, 0.5f);
            
            // === AIRCRAFT TYPE BADGE ===
            GameObject badgeObj = new GameObject("TypeBadge");
            badgeObj.transform.SetParent(root.transform, false);
            RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
            badgeRt.anchoredPosition = new Vector2(-35, 0);
            badgeRt.sizeDelta = new Vector2(26, 16);
            
            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.color = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 0.9f);
            
            GameObject badgeTextObj = new GameObject("BadgeText");
            badgeTextObj.transform.SetParent(badgeObj.transform, false);
            RectTransform badgeTextRt = badgeTextObj.AddComponent<RectTransform>();
            badgeTextRt.anchorMin = Vector2.zero;
            badgeTextRt.anchorMax = Vector2.one;
            badgeTextRt.offsetMin = Vector2.zero;
            badgeTextRt.offsetMax = Vector2.zero;
            
            TextMeshProUGUI badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            badgeText.fontSize = 9;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = Color.white;
            badgeText.text = GetBadgeText(shape);
            
            // Wire up the IndicatorElement references
            SetPrivateField(element, "rectTransform", rt);
            SetPrivateField(element, "symbolImage", symbolImg);
            SetPrivateField(element, "arrowImage", arrowImg);
            SetPrivateField(element, "labelText", labelText);
            SetPrivateField(element, "distanceText", distText);
            SetPrivateField(element, "altitudeText", altText);
            SetPrivateField(element, "canvasGroup", cg);
            
            // Save as prefab
            string prefabPath = $"{PrefabsPath}/{name}.prefab";
            
            // Delete existing prefab if any
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }
            
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            
            Debug.Log($"[IndicatorSystem] Created prefab: {prefabPath}");
            return prefab;
        }
        
        private static void CreateShapeSprite(Image image, IndicatorShape shape)
        {
            // Create a simple procedural texture for the shape
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            // Fill transparent
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 4;
            
            switch (shape)
            {
                case IndicatorShape.Diamond:
                    DrawDiamond(pixels, size, center, radius);
                    break;
                case IndicatorShape.Airliner:
                    DrawAirliner(pixels, size, center, radius);
                    break;
                case IndicatorShape.Fighter:
                    DrawFighter(pixels, size, center, radius);
                    break;
                case IndicatorShape.SmallPlane:
                    DrawSmallPlane(pixels, size, center, radius);
                    break;
                case IndicatorShape.Helicopter:
                    DrawHelicopter(pixels, size, center, radius);
                    break;
                case IndicatorShape.Circle:
                default:
                    DrawCircle(pixels, size, center, radius);
                    break;
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
            image.sprite = sprite;
        }
        
        private static void DrawDiamond(Color[] pixels, int size, Vector2 center, float radius)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    if ((dx / radius + dy / radius) <= 1f)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
        
        private static void DrawCircle(Color[] pixels, int size, Vector2 center, float radius)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
        
        private static void DrawAirliner(Color[] pixels, int size, Vector2 center, float radius)
        {
            // Large aircraft shape - wide body
            float fuselageWidth = radius * 0.25f;
            float fuselageLength = radius * 0.9f;
            float wingSpan = radius * 1.0f;
            float wingWidth = radius * 0.2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    
                    // Fuselage (vertical oval)
                    if (Mathf.Abs(dx) <= fuselageWidth && Mathf.Abs(dy) <= fuselageLength)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Wings (horizontal bar)
                    else if (Mathf.Abs(dy) <= wingWidth && Mathf.Abs(dx) <= wingSpan && dy < 0)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Tail (smaller horizontal bar at back)
                    else if (Mathf.Abs(dy - fuselageLength * 0.7f) <= wingWidth * 0.5f && 
                             Mathf.Abs(dx) <= wingSpan * 0.4f)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
        
        private static void DrawFighter(Color[] pixels, int size, Vector2 center, float radius)
        {
            // Fighter jet - swept wings, thin fuselage
            float fuselageWidth = radius * 0.15f;
            float fuselageLength = radius * 0.95f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float normalizedY = dy / fuselageLength;
                    
                    // Fuselage (narrow)
                    if (Mathf.Abs(dx) <= fuselageWidth && Mathf.Abs(dy) <= fuselageLength)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Swept wings (triangular)
                    else if (normalizedY > -0.5f && normalizedY < 0.3f)
                    {
                        float wingExtent = radius * (0.9f - Mathf.Abs(normalizedY) * 1.5f);
                        if (Mathf.Abs(dx) <= wingExtent)
                        {
                            pixels[y * size + x] = Color.white;
                        }
                    }
                }
            }
        }
        
        private static void DrawSmallPlane(Color[] pixels, int size, Vector2 center, float radius)
        {
            // Small single-engine aircraft
            float fuselageWidth = radius * 0.2f;
            float fuselageLength = radius * 0.8f;
            float wingSpan = radius * 0.9f;
            float wingWidth = radius * 0.12f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    
                    // Fuselage
                    if (Mathf.Abs(dx) <= fuselageWidth && Mathf.Abs(dy) <= fuselageLength)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Wings (straight)
                    else if (Mathf.Abs(dy + fuselageLength * 0.1f) <= wingWidth && 
                             Mathf.Abs(dx) <= wingSpan)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Tail
                    else if (Mathf.Abs(dy - fuselageLength * 0.6f) <= wingWidth * 0.8f && 
                             Mathf.Abs(dx) <= wingSpan * 0.35f)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
        
        private static void DrawHelicopter(Color[] pixels, int size, Vector2 center, float radius)
        {
            // Helicopter with rotor disk
            float bodyWidth = radius * 0.35f;
            float bodyLength = radius * 0.5f;
            float rotorRadius = radius * 0.9f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    
                    // Body (oval)
                    if (Mathf.Abs(dx) <= bodyWidth && Mathf.Abs(dy) <= bodyLength)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Rotor disk (circle outline)
                    else if (dist >= rotorRadius - 3 && dist <= rotorRadius)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Rotor blades (cross)
                    else if ((Mathf.Abs(dx) <= 2 || Mathf.Abs(dy) <= 2) && dist <= rotorRadius)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    // Tail boom
                    else if (Mathf.Abs(dx) <= 3 && dy > 0 && dy <= radius * 0.85f)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
        }
        
        private static void CreateArrowSprite(Image image)
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            
            // Draw arrow pointing up (will be rotated at runtime)
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    
                    // Triangle pointing up
                    float normalizedY = (dy + size / 2f) / size;
                    float maxX = (1f - normalizedY) * (size / 2f - 2);
                    
                    if (Mathf.Abs(dx) <= maxX && dy <= size / 2f - 4 && dy >= -size / 2f + 2)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
            image.sprite = sprite;
        }
        
        private static void CreateAltitudeArrow(Image image, bool pointUp)
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = pointUp ? (y - center.y) : (center.y - y);
                    
                    // Small triangle
                    float normalizedY = (dy + size / 2f) / size;
                    float maxX = (1f - normalizedY) * (size / 2f - 1);
                    
                    if (Mathf.Abs(dx) <= maxX && dy <= size / 2f - 2 && dy >= -size / 4f)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }
            
            tex.SetPixels(pixels);
            tex.Apply();
            
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
            image.sprite = sprite;
        }
        
        private static string GetBadgeText(IndicatorShape shape)
        {
            switch (shape)
            {
                case IndicatorShape.Airliner: return "COM";
                case IndicatorShape.Fighter: return "MIL";
                case IndicatorShape.SmallPlane: return "GA";
                case IndicatorShape.Helicopter: return "HEL";
                case IndicatorShape.Circle: return "UNK";
                default: return "";
            }
        }
        
        #endregion
        
        #region Private Helpers
        
        private static IndicatorSettings EnsureSettingsAsset()
        {
            // Try to load existing
            var settings = AssetDatabase.LoadAssetAtPath<IndicatorSettings>(SettingsPath);
            if (settings != null)
                return settings;
            
            // Create new
            settings = ScriptableObject.CreateInstance<IndicatorSettings>();
            
            // Ensure directory exists
            EnsureDirectory(System.IO.Path.GetDirectoryName(SettingsPath));
            
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[IndicatorSystem] Created settings asset at: {SettingsPath}");
            
            return settings;
        }
        
        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path);
                string folderName = System.IO.Path.GetFileName(path);
                
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDirectory(parent);
                }
                
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
        
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Custom inspector for IndicatorSystemController with quick actions.
    /// </summary>
    [CustomEditor(typeof(IndicatorSystemController))]
    public class IndicatorSystemControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Find Traffic Controller"))
            {
                var trafficController = Object.FindObjectOfType<TrafficRadarController>();
                if (trafficController != null)
                {
                    EditorGUIUtility.PingObject(trafficController.gameObject);
                    Debug.Log($"Found TrafficRadarController: {trafficController.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("No TrafficRadarController found in scene");
                }
            }
            
            if (GUILayout.Button("Find Weather Provider"))
            {
                var weatherProvider = Object.FindObjectOfType<WeatherRadarProviderBase>();
                if (weatherProvider != null)
                {
                    EditorGUIUtility.PingObject(weatherProvider.gameObject);
                    Debug.Log($"Found WeatherRadarProviderBase: {weatherProvider.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning("No WeatherRadarProviderBase found in scene");
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Create Aircraft Prefabs"))
            {
                IndicatorSystemSetupEditor.CreateAircraftPrefabsMenu();
            }
            
            if (GUILayout.Button("Open Settings"))
            {
                var controller = (IndicatorSystemController)target;
                var settings = controller.Settings;
                if (settings != null)
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                
                var controller = (IndicatorSystemController)target;
                EditorGUILayout.LabelField("Initialized:", controller.IsInitialized.ToString());
                EditorGUILayout.LabelField("Active Indicators:", controller.ActiveIndicatorCount.ToString());
            }
        }
    }
}
