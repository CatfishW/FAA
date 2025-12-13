using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.Collections.Generic;

namespace AviationUI.Editor
{
    /// <summary>
    /// FAA Symbology Setup Wizard - Creates aviation symbology UI elements
    /// according to the FAA specification sheet.
    /// 
    /// Canvas Scale: 0.9 x 0.9
    /// All Objects Scale: 1
    /// All Parent RectTransforms: 100 x 100
    /// PPU: 100 for all elements
    /// Font: SairaSemiCondensed-Regular for all TMP_Text
    /// </summary>
    public class FAASymbologySetupWizard : EditorWindow
    {
        #region Constants
        
        private const float CANVAS_SCALE = 0.9f;
        private const float PARENT_SIZE = 100f;
        private const int PPU = 100;
        
        private const string TEXTURE_PATH = "Assets/_Project/Textures/";
        private const string FONT_PATH = "Assets/Fonts/SairaSemiCondensed-Regular.ttf";
        private const string FAA_SYMBOLOGY_PATH = "Assets/_Project/Textures/FAA_Symbology/";
        
        #endregion

        #region Font Sizes
        
        private static class FontSizes
        {
            public const int Info = 72;
            public const int VerticalSpeedText = 48;
            public const int MagHeading = 45;
            public const int GroundSpeed = 60;
            public const int RAText = 60;
            public const int AGL = 31;
            public const int RPM = 31;
            public const int Torque = 60;
            public const int KTS = 72;
            public const int NAV = 72;
            public const int WaypointInfo = 55;
            public const int P1 = 72;
            public const int APBlockLarge = 72;
            public const int APBlockSmall = 50;
        }
        
        #endregion

        #region Private Fields
        
        private Vector2 scrollPosition;
        private TMP_FontAsset tmpFont;
        private bool createPlaceholders = true;
        private Color placeholderColor = new Color(1f, 1f, 1f, 0.8f);
        
        #endregion

        [MenuItem("Tools/Aviation/FAA Symbology Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<FAASymbologySetupWizard>("FAA Symbology Wizard");
            window.minSize = new Vector2(500, 600);
        }

        private void OnEnable()
        {
            // Load font asset
            var font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
            if (font != null)
            {
                // Try to find existing TMP font asset
                string[] guids = AssetDatabase.FindAssets("SairaSemiCondensed-Regular t:TMP_FontAsset");
                if (guids.Length > 0)
                {
                    tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("FAA Aviation Symbology Setup Wizard", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Creates aviation symbology UI elements according to FAA specification.\n\n" +
                "• Canvas scaled to 0.9 x 0.9\n" +
                "• All parent RectTransforms: 100 x 100\n" +
                "• All objects use scale 1\n" +
                "• PPU: 100 for all elements\n" +
                "• Font: SairaSemiCondensed-Regular\n",
                MessageType.Info);

            EditorGUILayout.Space(15);

            // Settings
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            createPlaceholders = EditorGUILayout.Toggle("Create Placeholder Images", createPlaceholders);
            if (createPlaceholders)
            {
                placeholderColor = EditorGUILayout.ColorField("Placeholder Color", placeholderColor);
            }

            // Font reference
            EditorGUILayout.Space(10);
            tmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField("TMP Font Asset", tmpFont, typeof(TMP_FontAsset), false);
            
            if (tmpFont == null)
            {
                EditorGUILayout.HelpBox(
                    "TMP Font Asset not assigned. Text will use default TMP font.\n" +
                    "To use SairaSemiCondensed-Regular:\n" +
                    "1. Window > TextMeshPro > Font Asset Creator\n" +
                    "2. Load SairaSemiCondensed-Regular.ttf\n" +
                    "3. Generate and save the asset\n" +
                    "4. Assign it above",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(20);

            // Generate Buttons
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
            if (GUILayout.Button("Create Full Aviation Symbology UI", GUILayout.Height(45)))
            {
                CreateFullSymbologyUI();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // Individual component buttons
            EditorGUILayout.LabelField("Create Individual Components", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1. Attitude/FPV")) CreateAttitudeFPVPanel(null);
            if (GUILayout.Button("2. Localizer")) CreateLocalizerPanel(null);
            if (GUILayout.Button("3. Airspeed")) CreateAirspeedPanel(null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("4. NR/ENG N2")) CreateNREngN2Panel(null);
            if (GUILayout.Button("5. Altimeter")) CreateAltimeterPanel(null);
            if (GUILayout.Button("6. VSI")) CreateVSIPanel(null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("7. Wind Speed")) CreateWindSpeedPanel(null);
            if (GUILayout.Button("8. Torque")) CreateTorquePanel(null);
            if (GUILayout.Button("9. Glidescope")) CreateGlidescopePanel(null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("10. Heading")) CreateHeadingPanel(null);
            if (GUILayout.Button("12. Bank Scale")) CreateBankScalePanel(null);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Save as Prefab"))
            {
                SaveCurrentAsPrefab();
            }

            EditorGUILayout.Space(20);
            DrawComponentInfo();

            EditorGUILayout.EndScrollView();
        }

        private void DrawComponentInfo()
        {
            EditorGUILayout.LabelField("Component Hierarchy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Attitude/FPV Panel (Miniature Aircraft, FPV, Scale)\n" +
                "2. Localizer Position Indicator (CDI, Deviation Dots)\n" +
                "3. Airspeed Indicator (Vertical Tape, Window Panel, Readout)\n" +
                "4. NR/ENG N2 Indicator (Frame, RPM Safety Range, Pointers)\n" +
                "5. Altimeter (Tape, Window Panel, Digital Display)\n" +
                "6. Vertical Speed Indicator (Scale, Pointer, Bar, Arrow)\n" +
                "7. Wind Speed Panel (Arrow, Circle)\n" +
                "8. Torque Panel (Frame, Safety Bar, Indicators)\n" +
                "9. Glidescope (Needle, Dots)\n" +
                "10. Heading Panel (Compass Card, Cardinal Points)\n" +
                "12. Bank Scale (Scale, Indicator Panel, Roll Pointer, Slip Slider)",
                MessageType.None);
        }

        #region Main Creation Methods

        private void CreateFullSymbologyUI()
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("FAASymbologyCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create root container with 0.9 scale
            GameObject rootContainer = CreateParentRectTransform(canvasObj.transform, "SymbologyRoot",
                Vector3.zero, true);
            rootContainer.transform.localScale = Vector3.one * CANVAS_SCALE;

            // Create all panels
            CreateAttitudeFPVPanel(rootContainer.transform);
            CreateLocalizerPanel(rootContainer.transform);
            CreateAirspeedPanel(rootContainer.transform);
            CreateNREngN2Panel(rootContainer.transform);
            CreateAltimeterPanel(rootContainer.transform);
            CreateVSIPanel(rootContainer.transform);
            CreateWindSpeedPanel(rootContainer.transform);
            CreateTorquePanel(rootContainer.transform);
            CreateGlidescopePanel(rootContainer.transform);
            CreateHeadingPanel(rootContainer.transform);
            CreateBankScalePanel(rootContainer.transform);

            // Create text elements container
            CreateTextElements(rootContainer.transform);

            Undo.RegisterCreatedObjectUndo(canvasObj, "Create FAA Symbology UI");
            Selection.activeGameObject = canvasObj;
            EditorGUIUtility.PingObject(canvasObj);

            Debug.Log("[FAASymbologySetupWizard] Created full aviation symbology UI!");
        }

        #endregion

        #region Component Creation Methods

        // 1. ATTITUDE/FPV PANEL
        private void CreateAttitudeFPVPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "1_AttitudeFPV_Panel",
                new Vector3(0, 0, 0));

            // Miniature Aircraft - 256x256, 0.2625751 x 0.2625751
            CreateImageElement(panel.transform, "MiniatureAircraft",
                new Vector2(256, 256), new Vector2(0.2625751f, 0.2625751f),
                Vector3.zero);

            // FPV - 256x256, 0.271635 x 0.271635, Y: -0.0035
            CreateImageElement(panel.transform, "FPV",
                new Vector2(256, 256), new Vector2(0.271635f, 0.271635f),
                new Vector3(0, -0.0035f, 0));

            // Scale - 341x2048, 0.63 x 2.67
            CreateImageElement(panel.transform, "Scale",
                new Vector2(341, 2048), new Vector2(0.63f, 2.67f),
                Vector3.zero);
        }

        // 2. LOCALIZER POSITION INDICATOR
        private void CreateLocalizerPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "2_LocalizerPositionIndicator",
                new Vector3(0, -0.35f, 0));

            // Course Deviation Indicator (CDI) - 256x256, 0.087 x 0.198
            CreateImageElement(panel.transform, "CDI",
                new Vector2(256, 256), new Vector2(0.087f, 0.198f),
                Vector3.zero);

            // Deviation Dots Panel - N/A, 100 x 100
            GameObject dotsPanel = CreateParentRectTransform(panel.transform, "DeviationDotsPanel",
                Vector3.zero);

            // Deviation Dots 1-4 - 256x256, 0.06 x 0.06 each
            CreateImageElement(dotsPanel.transform, "DeviationDot_1",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(-0.18f, 0, 0));

            CreateImageElement(dotsPanel.transform, "DeviationDot_2",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(-0.09f, 0, 0));

            CreateImageElement(dotsPanel.transform, "DeviationDot_3",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(0.09f, 0, 0));

            CreateImageElement(dotsPanel.transform, "DeviationDot_4",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(0.18f, 0, 0));
        }

        // 3. AIRSPEED INDICATOR
        private void CreateAirspeedPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "3_AirspeedIndicator",
                new Vector3(-0.90f, 0, 0));

            // Airspeed Vertical Tape - 32x1024, 0.1146427 x 0.68, Y: 0.272
            CreateImageElement(panel.transform, "AirspeedVerticalTape",
                new Vector2(32, 1024), new Vector2(0.1146427f, 0.68f),
                new Vector3(0, 0.272f, 0));

            // Window Panel - N/A, 100 x 100, X: -0.19
            GameObject windowPanel = CreateParentRectTransform(panel.transform, "WindowPanel",
                new Vector3(-0.19f, 0, 0));

            // Airspeed Readout - 512x512, 0.3 x 0.3382059
            CreateImageElement(windowPanel.transform, "AirspeedReadout",
                new Vector2(512, 512), new Vector2(0.3f, 0.3382059f),
                Vector3.zero);

            // Add KTS text
            CreateTMPText(windowPanel.transform, "KTS_Text", "KTS",
                FontSizes.KTS, new Vector3(0, -0.2f, 0));
        }

        // 4. NR/ENG N2 INDICATOR
        private void CreateNREngN2Panel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "4_NREngN2Indicator",
                new Vector3(0.5679707f, -0.692f, 0));

            // NR Indicator Frame - 512x512, 0.2918475 x 0.2918475, Y: 0.133
            CreateImageElement(panel.transform, "NRIndicatorFrame",
                new Vector2(512, 512), new Vector2(0.2918475f, 0.2918475f),
                new Vector3(0, 0.133f, 0));

            // RPM Safety Range Left - 256x256, 0.2029119 x 0.2029119, X: -0.0675, Y: 0.173
            CreateImageElement(panel.transform, "RPMSafetyRange_L",
                new Vector2(256, 256), new Vector2(0.2029119f, 0.2029119f),
                new Vector3(-0.0675f, 0.173f, 0));

            // RPM Safety Range Right
            CreateImageElement(panel.transform, "RPMSafetyRange_R",
                new Vector2(256, 256), new Vector2(0.2029119f, 0.2029119f),
                new Vector3(0.0675f, 0.173f, 0));

            // RPM Pointer Left - 512x512, 0.1 x 0.1, X: -0.1236, Y: 0.006
            CreateImageElement(panel.transform, "RPMPointer_L",
                new Vector2(512, 512), new Vector2(0.1f, 0.1f),
                new Vector3(-0.1236f, 0.006f, 0));

            // RPM Pointer Right
            CreateImageElement(panel.transform, "RPMPointer_R",
                new Vector2(512, 512), new Vector2(0.1f, 0.1f),
                new Vector3(0.1236f, 0.006f, 0));

            // RPM Center Pointer - 512x512, 0.111 x 0.111
            CreateImageElement(panel.transform, "RPMCenterPointer",
                new Vector2(512, 512), new Vector2(0.111f, 0.111f),
                Vector3.zero);

            // Add RPM text
            CreateTMPText(panel.transform, "RPM_Text", "RPM",
                FontSizes.RPM, new Vector3(0, -0.1f, 0));
        }

        // 5. ALTIMETER
        private void CreateAltimeterPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "5_Altimeter",
                new Vector3(1.258f, 0, 0));

            // Altimeter Tape - 32x1024, 0.1146427 x 0.1736253
            CreateImageElement(panel.transform, "AltimeterTape",
                new Vector2(32, 1024), new Vector2(0.1146427f, 0.1736253f),
                Vector3.zero);

            // Window Panel - N/A, 100 x 100
            GameObject windowPanel = CreateParentRectTransform(panel.transform, "WindowPanel",
                Vector3.zero);

            // Digital Display - 512x512, 0.3 x 0.3382059
            CreateImageElement(windowPanel.transform, "DigitalDisplay",
                new Vector2(512, 512), new Vector2(0.3f, 0.3382059f),
                Vector3.zero);

            // Add altitude text
            CreateTMPText(windowPanel.transform, "Altitude_Text", "00000",
                FontSizes.Info, Vector3.zero);
        }

        // 6. VERTICAL SPEED INDICATOR (VSI)
        private void CreateVSIPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "6_VSI",
                new Vector3(0.832f, 0, 0));

            // VSI Scale - 256x1536, 0.2341584 x 1.072914
            CreateImageElement(panel.transform, "VSIScale",
                new Vector2(256, 1536), new Vector2(0.2341584f, 1.072914f),
                Vector3.zero);

            // VSI Pointer - 256x256, 0.040107 x 0.040107, X: 0.067, Y: 0.358
            CreateImageElement(panel.transform, "VSIPointer",
                new Vector2(256, 256), new Vector2(0.040107f, 0.040107f),
                new Vector3(0.067f, 0.358f, 0));

            // Bar - 1024x1024, 0.01281343 x 0.6908157, X: 0.136
            CreateImageElement(panel.transform, "Bar",
                new Vector2(1024, 1024), new Vector2(0.01281343f, 0.6908157f),
                new Vector3(0.136f, 0, 0));

            // Arrow - 512x512, 0.040107 x 0.040107, Y: 0.3606
            CreateImageElement(panel.transform, "Arrow",
                new Vector2(512, 512), new Vector2(0.040107f, 0.040107f),
                new Vector3(0, 0.3606f, 0));

            // Add vertical speed text
            CreateTMPText(panel.transform, "VSI_Text", "0",
                FontSizes.VerticalSpeedText, new Vector3(0.1f, 0, 0));
        }

        // 7. WIND SPEED PANEL
        private void CreateWindSpeedPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "7_WindSpeedPanel",
                new Vector3(-0.5970154f, -0.134f, 0));

            // Arrow - 512x512, 0.1 x 0.1
            CreateImageElement(panel.transform, "Arrow",
                new Vector2(512, 512), new Vector2(0.1f, 0.1f),
                Vector3.zero);

            // Wind Speed Circle - 512x512, 0.1 x 0.1
            CreateImageElement(panel.transform, "WindSpeedCircle",
                new Vector2(512, 512), new Vector2(0.1f, 0.1f),
                Vector3.zero);
        }

        // 8. TORQUE PANEL
        private void CreateTorquePanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "8_TorquePanel",
                new Vector3(-0.5970303f, -0.685f, 0));

            // Torque Frame - 512x512, 0.2918475 x 0.2918475, X: 0.017, Y: 0.111
            CreateImageElement(panel.transform, "TorqueFrame",
                new Vector2(512, 512), new Vector2(0.2918475f, 0.2918475f),
                new Vector3(0.017f, 0.111f, 0));

            // Torque Safety Limit Bar - 256x256, 0.17 x 0.42, X: -0.0004, Y: 0.2169
            CreateImageElement(panel.transform, "TorqueSafetyLimitBar",
                new Vector2(256, 256), new Vector2(0.17f, 0.42f),
                new Vector3(-0.0004f, 0.2169f, 0));

            // Torque Indicator Left - 512x512, 0.1 x 0.1, X: -0.0606
            CreateImageElement(panel.transform, "TorqueIndicator_L",
                new Vector2(512, 512), new Vector2(0.1f, 0.1f),
                new Vector3(-0.0606f, 0, 0));

            // Torque Indicator Right
            CreateImageElement(panel.transform, "TorqueIndicator_R",
                new Vector2(512, 512), new Vector2(0.1f, 0.1f),
                new Vector3(0.0606f, 0, 0));

            // Add torque text
            CreateTMPText(panel.transform, "Torque_Text", "TQ",
                FontSizes.Torque, new Vector3(0, -0.15f, 0));
        }

        // 9. GLIDESCOPE
        private void CreateGlidescopePanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "9_Glidescope",
                new Vector3(0, 0, 0));

            // Glidescope Needle - 256x256, 0.1 x 0.08, X: 0.6694
            CreateImageElement(panel.transform, "GlidescopeNeedle",
                new Vector2(256, 256), new Vector2(0.1f, 0.08f),
                new Vector3(0.6694f, 0, 0));

            // Glidescope Dots - 256x256, 0.06 x 0.06, X: 0.67
            CreateImageElement(panel.transform, "GlidescopeDot_1",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(0.67f, 0.18f, 0));

            CreateImageElement(panel.transform, "GlidescopeDot_2",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(0.67f, 0.09f, 0));

            CreateImageElement(panel.transform, "GlidescopeDot_3",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(0.67f, -0.09f, 0));

            CreateImageElement(panel.transform, "GlidescopeDot_4",
                new Vector2(256, 256), new Vector2(0.06f, 0.06f),
                new Vector3(0.67f, -0.18f, 0));
        }

        // 10. HEADING PANEL
        private void CreateHeadingPanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "10_HeadingPanel",
                new Vector3(0, -0.597f, 0));

            // Compass Card - 1024x1024, 0.7 x 0.7, Y: -0.1679997
            CreateImageElement(panel.transform, "CompassCard",
                new Vector2(1024, 1024), new Vector2(0.7f, 0.7f),
                new Vector3(0, -0.1679997f, 0));

            // Cardinal Points - 1024x1024, 0.68 x 0.68, Y: -0.1679997
            CreateImageElement(panel.transform, "CardinalPoints",
                new Vector2(1024, 1024), new Vector2(0.68f, 0.68f),
                new Vector3(0, -0.1679997f, 0));

            // Add heading text
            CreateTMPText(panel.transform, "Heading_Text", "000",
                FontSizes.MagHeading, new Vector3(0, 0.1f, 0));
        }

        // 12. BANK SCALE
        private void CreateBankScalePanel(Transform parent)
        {
            if (parent == null) parent = GetOrCreateCanvas();
            
            GameObject panel = CreateParentRectTransform(parent, "12_BankScale",
                new Vector3(0, 0.408f, 0));

            // Bank Scale - 1024x1024, 1.2 x 1.2, Y: -0.408
            CreateImageElement(panel.transform, "BankScale",
                new Vector2(1024, 1024), new Vector2(1.2f, 1.2f),
                new Vector3(0, -0.408f, 0));

            // Bank Scale Indicator Panel - N/A, 100 x 100, Y: -0.408
            GameObject indicatorPanel = CreateParentRectTransform(panel.transform, "BankScaleIndicatorPanel",
                new Vector3(0, -0.408f, 0));

            // Roll Pointer - 256x256, 0.1267855 x 0.1267855, Y: 0.425
            CreateImageElement(indicatorPanel.transform, "RollPointer",
                new Vector2(256, 256), new Vector2(0.1267855f, 0.1267855f),
                new Vector3(0, 0.425f, 0));

            // Slip Slider - 256x256, 0.19 x 0.158, Y: 0.412
            CreateImageElement(indicatorPanel.transform, "SlipSlider",
                new Vector2(256, 256), new Vector2(0.19f, 0.158f),
                new Vector3(0, 0.412f, 0));
        }

        // Text Elements
        private void CreateTextElements(Transform parent)
        {
            GameObject textPanel = CreateParentRectTransform(parent, "TextElements",
                Vector3.zero);

            // Info text
            CreateTMPText(textPanel.transform, "Info_Text", "INFO",
                FontSizes.Info, new Vector3(-0.8f, 0.4f, 0));

            // Ground Speed
            CreateTMPText(textPanel.transform, "GroundSpeed_Text", "GS 000",
                FontSizes.GroundSpeed, new Vector3(-0.8f, 0.3f, 0));

            // RA Text
            CreateTMPText(textPanel.transform, "RA_Text", "RA",
                FontSizes.RAText, new Vector3(0.9f, -0.3f, 0));

            // AGL
            CreateTMPText(textPanel.transform, "AGL_Text", "AGL 0000",
                FontSizes.AGL, new Vector3(0.9f, -0.35f, 0));

            // NAV
            CreateTMPText(textPanel.transform, "NAV_Text", "NAV",
                FontSizes.NAV, new Vector3(-0.9f, -0.4f, 0));

            // Waypoint Info
            CreateTMPText(textPanel.transform, "WaypointInfo_Text", "WPT",
                FontSizes.WaypointInfo, new Vector3(-0.9f, -0.45f, 0));

            // P1
            CreateTMPText(textPanel.transform, "P1_Text", "P1",
                FontSizes.P1, new Vector3(0.4f, 0.4f, 0));

            // AP Block Large
            CreateTMPText(textPanel.transform, "APBlock_Large_Text", "AP",
                FontSizes.APBlockLarge, new Vector3(0.5f, 0.4f, 0));

            // AP Block Small
            CreateTMPText(textPanel.transform, "APBlock_Small_Text", "HDG",
                FontSizes.APBlockSmall, new Vector3(0.5f, 0.35f, 0));
        }

        #endregion

        #region Helper Methods

        private Transform GetOrCreateCanvas()
        {
            var existing = FindObjectOfType<Canvas>();
            if (existing != null) return existing.transform;

            GameObject canvasObj = new GameObject("FAASymbologyCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            return canvasObj.transform;
        }

        private GameObject CreateParentRectTransform(Transform parent, string name, Vector3 position, bool isRoot = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            
            if (isRoot)
            {
                // Stretch to fill parent
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                // Standard parent rect: 100x100, centered
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(PARENT_SIZE, PARENT_SIZE);
                
                // Position is in normalized units, need to scale to reference resolution
                rect.anchoredPosition = new Vector2(
                    position.x * 1920f * 0.5f, // Half because centered
                    position.y * 1080f * 0.5f
                );
            }

            return obj;
        }

        private GameObject CreateImageElement(Transform parent, string name, 
            Vector2 resolution, Vector2 rectSize, Vector3 position)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            
            // Size is relative to parent (100x100), scaled by rectSize values
            // Then scaled for screen reference resolution
            float screenScale = Mathf.Min(1920f, 1080f) / 2f;
            rect.sizeDelta = new Vector2(
                rectSize.x * screenScale,
                rectSize.y * screenScale
            );
            
            // Position relative to parent
            rect.anchoredPosition = new Vector2(
                position.x * screenScale,
                position.y * screenScale
            );

            // Add Image component
            Image img = obj.AddComponent<Image>();
            
            // Try to load sprite or create placeholder
            Sprite sprite = TryLoadSprite(name);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else if (createPlaceholders)
            {
                img.color = placeholderColor;
            }
            else
            {
                img.color = new Color(1, 1, 1, 0.3f);
            }
            
            img.raycastTarget = false;

            return obj;
        }

        private GameObject CreateTMPText(Transform parent, string name, string text,
            int fontSize, Vector3 position)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200, 100);
            
            float screenScale = Mathf.Min(1920f, 1080f) / 2f;
            rect.anchoredPosition = new Vector2(
                position.x * screenScale,
                position.y * screenScale
            );

            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;

            // Set font if available
            if (tmpFont != null)
            {
                tmp.font = tmpFont;
            }

            // Line spacing = 1
            tmp.lineSpacing = 0; // TMP uses 0 as default (1.0 multiplier)

            return obj;
        }

        private Sprite TryLoadSprite(string elementName)
        {
            // Try different naming patterns
            string[] patterns = {
                FAA_SYMBOLOGY_PATH + elementName + ".png",
                TEXTURE_PATH + elementName + ".png",
                FAA_SYMBOLOGY_PATH + elementName.Replace("_", "") + ".png",
                TEXTURE_PATH + elementName.Replace("_", "") + ".png"
            };

            foreach (string path in patterns)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) return sprite;

                // Try to convert texture to sprite
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spritePixelsPerUnit = PPU;
                        importer.SaveAndReimport();
                        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }
            }

            return null;
        }

        private void SaveCurrentAsPrefab()
        {
            GameObject canvasObj = GameObject.Find("FAASymbologyCanvas");
            if (canvasObj == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "No FAASymbologyCanvas found in scene. Create one first.", "OK");
                return;
            }

            string prefabPath = "Assets/_Project/Prefabs/AviationUI";
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "AviationUI");
            }

            string prefabFile = prefabPath + "/FAASymbologyUI.prefab";
            prefabFile = AssetDatabase.GenerateUniqueAssetPath(prefabFile);

            PrefabUtility.SaveAsPrefabAsset(canvasObj, prefabFile);
            AssetDatabase.Refresh();

            Debug.Log($"[FAASymbologySetupWizard] Saved prefab to: {prefabFile}");
            EditorUtility.DisplayDialog("Success", $"Saved to:\n{prefabFile}", "OK");
        }

        #endregion
    }
}
