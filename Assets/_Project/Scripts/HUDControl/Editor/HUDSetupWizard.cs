using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using TMPro;
using AircraftControl.Core;
using HUDControl.Core;
using HUDControl.Elements;
using HUDControl.CompassBar;

namespace HUDControl.Editor
{
    /// <summary>
    /// Editor window for easy one-click setup of the HUD Control System.
    /// Access via menu: Tools > HUD Control > Setup Wizard
    /// Features: Auto-detect UI GameObjects, auto-add element components, auto-link references.
    /// </summary>
    public class HUDSetupWizard : EditorWindow
    {
        #region Settings
        
        private bool createController = true;
        private bool autoAddElements = true;
        private bool autoLinkReferences = true;
        private bool linkToAircraftController = true;
        
        private AircraftController targetAircraftController;
        private GameObject hudRoot;
        
        private Vector2 scrollPosition;
        
        // Element search results
        private List<HUDElementBase> foundElements = new List<HUDElementBase>();
        private Dictionary<string, bool> elementToggles = new Dictionary<string, bool>();
        
        // Auto-detected UI GameObjects (before element components added)
        private List<DetectedUIElement> detectedUIElements = new List<DetectedUIElement>();
        
        // Element name patterns for auto-detection
        private static readonly Dictionary<string, Type> ElementPatterns = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "Attitude", typeof(AttitudeIndicatorElement) },
            { "Airspeed", typeof(AirspeedIndicatorElement) },
            { "Altimeter", typeof(AltimeterElement) },
            { "Heading", typeof(HeadingIndicatorElement) },
            { "VSI", typeof(VSIElement) },
            { "Torque", typeof(TorquePanelElement) },
            { "NR/ENG", typeof(NRIndicatorElement) },
            { "NR Ind", typeof(NRIndicatorElement) },
            { "Glidescope", typeof(GlidescopeElement) },
            { "Glideslope", typeof(GlidescopeElement) },
            { "Localizer", typeof(LocalizerElement) },
            { "Bank Scale", typeof(BankScaleElement) },
            { "Bank", typeof(BankScaleElement) },
            { "FPV", typeof(FPVElement) },
            { "Compass Bar", typeof(CompassBarElement) },
            { "CompassBar", typeof(CompassBarElement) },
        };
        
        private struct DetectedUIElement
        {
            public GameObject gameObject;
            public string matchedPattern;
            public Type elementType;
            public bool hasComponent;
            public bool selected;
        }
        
        #endregion
        
        #region Static Accessors
        
        [MenuItem("Tools/HUD Control/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<HUDSetupWizard>("HUD Control Setup");
            window.minSize = new Vector2(500, 700);
            window.Show();
        }
        
        [MenuItem("Tools/HUD Control/Quick Setup (One Click)")]
        public static void QuickSetup()
        {
            var wizard = CreateInstance<HUDSetupWizard>();
            wizard.targetAircraftController = FindObjectOfType<AircraftController>();
            wizard.ScanForUIElements();
            wizard.PerformFullSetup();
            DestroyImmediate(wizard);
        }
        
        [MenuItem("Tools/HUD Control/Add Element Components/Attitude Indicator")]
        public static void AddAttitudeIndicator() => AddElementToSelection<AttitudeIndicatorElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Airspeed Indicator")]
        public static void AddAirspeedIndicator() => AddElementToSelection<AirspeedIndicatorElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Altimeter")]
        public static void AddAltimeter() => AddElementToSelection<AltimeterElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Heading Indicator")]
        public static void AddHeadingIndicator() => AddElementToSelection<HeadingIndicatorElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/VSI")]
        public static void AddVSI() => AddElementToSelection<VSIElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Torque Panel")]
        public static void AddTorquePanel() => AddElementToSelection<TorquePanelElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/NR Indicator")]
        public static void AddNRIndicator() => AddElementToSelection<NRIndicatorElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Localizer")]
        public static void AddLocalizer() => AddElementToSelection<LocalizerElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Glidescope")]
        public static void AddGlidescope() => AddElementToSelection<GlidescopeElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Bank Scale")]
        public static void AddBankScale() => AddElementToSelection<BankScaleElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/FPV")]
        public static void AddFPV() => AddElementToSelection<FPVElement>();
        
        [MenuItem("Tools/HUD Control/Add Element Components/Compass Bar")]
        public static void AddCompassBar() => AddElementToSelection<CompassBarElement>();
        
        private static void AddElementToSelection<T>() where T : HUDElementBase
        {
            if (Selection.activeGameObject != null)
            {
                var existing = Selection.activeGameObject.GetComponent<T>();
                if (existing == null)
                {
                    var element = Undo.AddComponent<T>(Selection.activeGameObject);
                    AutoLinkReferences(element);
                    Debug.Log($"[HUDSetupWizard] Added {typeof(T).Name} to {Selection.activeGameObject.name}");
                }
                else
                {
                    Debug.LogWarning($"[HUDSetupWizard] {Selection.activeGameObject.name} already has {typeof(T).Name}");
                }
            }
            else
            {
                Debug.LogWarning("[HUDSetupWizard] No GameObject selected");
            }
        }
        
        #endregion
        
        #region GUI
        
        private void OnEnable()
        {
            targetAircraftController = FindObjectOfType<AircraftController>();
            ScanForUIElements();
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space(10);
            
            DrawDataSourceSection();
            EditorGUILayout.Space(10);
            
            DrawAutoDetectionSection();
            EditorGUILayout.Space(10);
            
            DrawExistingElementsSection();
            EditorGUILayout.Space(10);
            
            DrawStencilMaskSection();
            EditorGUILayout.Space(10);
            
            DrawSceneStatusSection();
            EditorGUILayout.Space(10);
            
            DrawActionsSection();
            
            EditorGUILayout.EndScrollView();
        }

        
        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HUD Control System Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This wizard automatically sets up the HUD Control System:\n" +
                "• Auto-detects UI GameObjects by name (Attitude, Airspeed, etc.)\n" +
                "• Auto-adds element components to detected UI\n" +
                "• Auto-links child references (readouts, tapes, pointers)\n" +
                "• Creates HUDController and links to AircraftController", 
                MessageType.Info);
        }
        
        private void DrawDataSourceSection()
        {
            EditorGUILayout.LabelField("Data Source", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            targetAircraftController = (AircraftController)EditorGUILayout.ObjectField(
                "Aircraft Controller", 
                targetAircraftController, 
                typeof(AircraftController), 
                true);
            
            if (targetAircraftController == null)
            {
                if (GUILayout.Button("Find AircraftController"))
                {
                    targetAircraftController = FindObjectOfType<AircraftController>();
                }
            }
            else
            {
                EditorGUILayout.LabelField("✓ AircraftController found", EditorStyles.miniLabel);
            }
            
            EditorGUI.indentLevel--;
        }
        
        private void DrawAutoDetectionSection()
        {
            EditorGUILayout.LabelField("Auto-Detect UI Elements", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            hudRoot = (GameObject)EditorGUILayout.ObjectField(
                "Search Root", 
                hudRoot, 
                typeof(GameObject), 
                true);
            
            if (GUILayout.Button("Scan UI", GUILayout.Width(80)))
            {
                ScanForUIElements();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            if (detectedUIElements.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No matching UI GameObjects found. Assign a HUD root GameObject and click 'Scan UI'.\n" +
                    "Looks for names containing: Attitude, Airspeed, Altimeter, Heading, VSI, Torque, NR/ENG, Glidescope, Localizer, Bank Scale, FPV", 
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Detected {detectedUIElements.Count} UI element(s) to add:");
                
                EditorGUI.indentLevel++;
                for (int i = 0; i < detectedUIElements.Count; i++)
                {
                    var detected = detectedUIElements[i];
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    bool newSelected = EditorGUILayout.Toggle(detected.selected, GUILayout.Width(20));
                    if (newSelected != detected.selected)
                    {
                        detected.selected = newSelected;
                        detectedUIElements[i] = detected;
                    }
                    
                    // Status icon
                    string icon = detected.hasComponent ? "✓" : "○";
                    EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                    
                    EditorGUILayout.LabelField($"[{detected.matchedPattern}]", GUILayout.Width(100));
                    EditorGUILayout.ObjectField(detected.gameObject, typeof(GameObject), true);
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                {
                    for (int i = 0; i < detectedUIElements.Count; i++)
                    {
                        var d = detectedUIElements[i];
                        d.selected = true;
                        detectedUIElements[i] = d;
                    }
                }
                if (GUILayout.Button("Select None"))
                {
                    for (int i = 0; i < detectedUIElements.Count; i++)
                    {
                        var d = detectedUIElements[i];
                        d.selected = false;
                        detectedUIElements[i] = d;
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
        }
        
        private void DrawExistingElementsSection()
        {
            EditorGUILayout.LabelField("Existing Elements", EditorStyles.boldLabel);
            
            if (foundElements.Count == 0)
            {
                EditorGUILayout.HelpBox("No existing HUD element components found.", MessageType.None);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                foreach (var element in foundElements)
                {
                    if (element == null) continue;
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("✓", GUILayout.Width(20));
                    EditorGUILayout.LabelField($"[{element.ElementId}]", GUILayout.Width(100));
                    EditorGUILayout.ObjectField(element, typeof(HUDElementBase), true);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }
        
        private void DrawSceneStatusSection()
        {
            EditorGUILayout.LabelField("Scene Status", EditorStyles.boldLabel);
            
            var existingController = FindObjectOfType<HUDController>();
            int pendingAdditions = 0;
            foreach (var d in detectedUIElements)
                if (d.selected && !d.hasComponent) pendingAdditions++;
            
            EditorGUILayout.BeginVertical("box");
            DrawStatusLine("HUDController", existingController != null);
            DrawStatusLine("AircraftController", targetAircraftController != null);
            DrawStatusLine("Existing Elements", foundElements.Count > 0, foundElements.Count.ToString());
            DrawStatusLine("Pending Additions", pendingAdditions > 0, pendingAdditions.ToString());
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusLine(string name, bool exists, string extra = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(exists ? "✓" : "○", GUILayout.Width(20));
            EditorGUILayout.LabelField(name, GUILayout.Width(150));
            
            string status = exists ? "OK" : "Missing";
            if (!string.IsNullOrEmpty(extra)) status = extra;
            
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawStencilMaskSection()
        {
            EditorGUILayout.LabelField("Stencil Masking", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Add Unity Mask components to panels to clip content that moves outside the visible area.\n" +
                "This prevents UI elements from disappearing during animation.",
                MessageType.Info);
            
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.LabelField("Quick Add Masks:", EditorStyles.miniLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Attitude"))
                AddMaskToPanel("Attitude", true);
            if (GUILayout.Button("Heading"))
                AddMaskToPanel("Heading", false);
            if (GUILayout.Button("Airspeed"))
                AddMaskToPanel("Airspeed", false);
            if (GUILayout.Button("Altimeter"))
                AddMaskToPanel("Altimeter", false);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("VSI"))
                AddMaskToPanel("VSI", false);
            if (GUILayout.Button("Localizer"))
                AddMaskToPanel("Localizer", false);
            if (GUILayout.Button("Glidescope"))
                AddMaskToPanel("Glidescope", false);
            if (GUILayout.Button("All Detected"))
                AddMasksToAllDetected();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void AddMaskToPanel(string panelNamePattern, bool circular)
        {
            if (hudRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a HUD Root first.", "OK");
                return;
            }
            
            // Find the panel
            Transform panel = FindPanelRecursive(hudRoot.transform, panelNamePattern);
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Not Found", 
                    $"Could not find panel containing '{panelNamePattern}' in hierarchy.", "OK");
                return;
            }
            
            // Check if already has Mask
            var existingMask = panel.GetComponent<UnityEngine.UI.Mask>();
            if (existingMask != null)
            {
                Debug.Log($"[HUDSetupWizard] {panel.name} already has a Mask component.");
                return;
            }
            
            // Add Mask component
            Undo.RecordObject(panel.gameObject, "Add Mask Component");
            var mask = Undo.AddComponent<UnityEngine.UI.Mask>(panel.gameObject);
            mask.showMaskGraphic = false;
            
            // Ensure it has an Image component for the mask shape
            var image = panel.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                image = Undo.AddComponent<UnityEngine.UI.Image>(panel.gameObject);
                image.color = new Color(1, 1, 1, 0.01f); // Nearly invisible
            }
            
            Debug.Log($"[HUDSetupWizard] Added Mask to {panel.name}");
            EditorUtility.SetDirty(panel.gameObject);
        }
        
        private void AddMasksToAllDetected()
        {
            if (hudRoot == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a HUD Root first.", "OK");
                return;
            }
            
            int added = 0;
            foreach (var detected in detectedUIElements)
            {
                if (detected.gameObject == null) continue;
                
                // Skip if already has Mask
                if (detected.gameObject.GetComponent<UnityEngine.UI.Mask>() != null)
                    continue;
                
                var mask = Undo.AddComponent<UnityEngine.UI.Mask>(detected.gameObject);
                mask.showMaskGraphic = false;
                
                var image = detected.gameObject.GetComponent<UnityEngine.UI.Image>();
                if (image == null)
                {
                    image = Undo.AddComponent<UnityEngine.UI.Image>(detected.gameObject);
                    image.color = new Color(1, 1, 1, 0.01f);
                }
                
                added++;
                EditorUtility.SetDirty(detected.gameObject);
            }
            
            Debug.Log($"[HUDSetupWizard] Added Mask to {added} detected elements.");
        }
        
        private Transform FindPanelRecursive(Transform parent, string namePattern)
        {
            if (parent.name.IndexOf(namePattern, StringComparison.OrdinalIgnoreCase) >= 0)
                return parent;
            
            foreach (Transform child in parent)
            {
                var found = FindPanelRecursive(child, namePattern);
                if (found != null) return found;
            }
            
            return null;
        }
        
        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            // Options
            createController = EditorGUILayout.Toggle("Create HUDController", createController);
            linkToAircraftController = EditorGUILayout.Toggle("Link to AircraftController", linkToAircraftController);
            autoAddElements = EditorGUILayout.Toggle("Auto-add element components", autoAddElements);
            autoLinkReferences = EditorGUILayout.Toggle("Auto-link UI references", autoLinkReferences);
            
            EditorGUILayout.Space(10);
            
            // Main setup button
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 40
            };
            
            if (GUILayout.Button("🚀 Setup Everything (One Click)", buttonStyle))
            {
                PerformFullSetup();
            }
            
            EditorGUILayout.Space(5);
            
            // Secondary actions
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Add Elements Only"))
            {
                AddSelectedElements();
                ScanForUIElements();
            }
            
            if (GUILayout.Button("Link References Only"))
            {
                LinkAllReferences();
            }
            
            if (GUILayout.Button("Refresh"))
            {
                ScanForUIElements();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        #endregion
        
        #region Logic
        
        private void ScanForUIElements()
        {
            detectedUIElements.Clear();
            foundElements.Clear();
            
            Transform searchRoot = hudRoot != null ? hudRoot.transform : null;
            
            if (searchRoot == null)
            {
                // Try to find a Canvas as default root
                var canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    searchRoot = canvas.transform;
                    hudRoot = canvas.gameObject;
                }
            }
            
            if (searchRoot != null)
            {
                ScanTransformRecursive(searchRoot);
            }
            
            // Also find existing elements
            if (hudRoot != null)
            {
                foundElements.AddRange(hudRoot.GetComponentsInChildren<HUDElementBase>(true));
            }
            else
            {
                foundElements.AddRange(FindObjectsOfType<HUDElementBase>());
            }
            
            Debug.Log($"[HUDSetupWizard] Detected {detectedUIElements.Count} UI elements, {foundElements.Count} existing components");
        }
        
        private void ScanTransformRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                string name = child.name;
                
                foreach (var pattern in ElementPatterns)
                {
                    if (name.IndexOf(pattern.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Check if already has component
                        bool hasComponent = child.GetComponent<HUDElementBase>() != null;
                        
                        detectedUIElements.Add(new DetectedUIElement
                        {
                            gameObject = child.gameObject,
                            matchedPattern = pattern.Key,
                            elementType = pattern.Value,
                            hasComponent = hasComponent,
                            selected = !hasComponent // Auto-select if doesn't have component yet
                        });
                        
                        break; // Only match first pattern
                    }
                }
                
                // Recurse
                ScanTransformRecursive(child);
            }
        }
        
        private void PerformFullSetup()
        {
            Undo.SetCurrentGroupName("Setup HUD Control System");
            int undoGroup = Undo.GetCurrentGroup();
            
            int elementsAdded = 0;
            int referencesLinked = 0;
            
            // Step 1: Add element components to detected UI
            if (autoAddElements)
            {
                elementsAdded = AddSelectedElements();
            }
            
            // Step 2: Auto-link references
            if (autoLinkReferences)
            {
                referencesLinked = LinkAllReferences();
            }
            
            // Step 3: Refresh element list
            ScanForUIElements();
            
            // Step 4: Create/find HUDController
            HUDController controller = null;
            if (createController)
            {
                controller = FindObjectOfType<HUDController>();
                
                if (controller == null)
                {
                    GameObject controllerObj = new GameObject("HUDController");
                    Undo.RegisterCreatedObjectUndo(controllerObj, "Create HUD Controller");
                    controller = controllerObj.AddComponent<HUDController>();
                }
            }
            
            // Step 5: Link to AircraftController
            if (linkToAircraftController && controller != null)
            {
                if (targetAircraftController == null)
                {
                    targetAircraftController = FindObjectOfType<AircraftController>();
                }
                
                if (targetAircraftController != null)
                {
                    SerializedObject so = new SerializedObject(controller);
                    var acProp = so.FindProperty("aircraftController");
                    if (acProp != null)
                    {
                        acProp.objectReferenceValue = targetAircraftController;
                        so.ApplyModifiedProperties();
                    }
                }
            }
            
            // Step 6: Register all elements with controller
            if (controller != null && foundElements.Count > 0)
            {
                SerializedObject so = new SerializedObject(controller);
                var elementsProp = so.FindProperty("elements");
                
                if (elementsProp != null)
                {
                    elementsProp.ClearArray();
                    
                    for (int i = 0; i < foundElements.Count; i++)
                    {
                        if (foundElements[i] == null) continue;
                        
                        elementsProp.InsertArrayElementAtIndex(i);
                        elementsProp.GetArrayElementAtIndex(i).objectReferenceValue = foundElements[i];
                    }
                    
                    so.ApplyModifiedProperties();
                }
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            
            // Select controller
            if (controller != null)
            {
                Selection.activeGameObject = controller.gameObject;
            }
            
            // Show completion dialog
            EditorUtility.DisplayDialog(
                "HUD Control Setup Complete",
                $"HUD Control System has been set up!\n\n" +
                $"• Elements added: {elementsAdded}\n" +
                $"• References linked: {referencesLinked}\n" +
                $"• Total elements registered: {foundElements.Count}\n" +
                $"• AircraftController: {(targetAircraftController != null ? "Linked" : "Not found")}\n\n" +
                "Press Play to test!",
                "OK");
        }
        
        private int AddSelectedElements()
        {
            int added = 0;
            
            for (int i = 0; i < detectedUIElements.Count; i++)
            {
                var detected = detectedUIElements[i];
                
                if (!detected.selected) continue;
                if (detected.gameObject == null || detected.elementType == null) continue;
                
                // Auto-delete existing HUDElementBase component if present
                var existingElement = detected.gameObject.GetComponent<HUDElementBase>();
                if (existingElement != null)
                {
                    Debug.Log($"[HUDSetupWizard] Removing old {existingElement.GetType().Name} from {detected.gameObject.name}");
                    Undo.DestroyObjectImmediate(existingElement);
                }
                
                // Add component
                var element = Undo.AddComponent(detected.gameObject, detected.elementType) as HUDElementBase;
                
                if (element != null)
                {
                    added++;
                    Debug.Log($"[HUDSetupWizard] Added {detected.elementType.Name} to {detected.gameObject.name}");
                    
                    // Auto-link references for this element
                    if (autoLinkReferences)
                    {
                        AutoLinkReferences(element);
                    }
                }
                
                detected.hasComponent = true;
                detectedUIElements[i] = detected;
            }
            
            return added;
        }

        
        private int LinkAllReferences()
        {
            int linked = 0;
            
            // Refresh found elements
            if (hudRoot != null)
            {
                foundElements.Clear();
                foundElements.AddRange(hudRoot.GetComponentsInChildren<HUDElementBase>(true));
            }
            
            foreach (var element in foundElements)
            {
                if (element != null)
                {
                    if (AutoLinkReferences(element))
                    {
                        linked++;
                    }
                }
            }
            
            return linked;
        }
        
        /// <summary>
        /// Auto-link UI references for an element based on child naming conventions
        /// </summary>
        private static bool AutoLinkReferences(HUDElementBase element)
        {
            if (element == null) return false;
            
            SerializedObject so = new SerializedObject(element);
            bool anyLinked = false;
            
            // Common patterns to look for in children - matches FAA symbology naming conventions
            var patterns = new Dictionary<string, string[]>
            {
                // Attitude Indicator
                { "rollPivot", new[] { "Scale", "RollPivot", "AttitudePivot", "Pivot", "Attitude Scale" } },
                { "pitchLadder", new[] { "PitchLadder", "Ladder", "Horizon", "HorizonImage", "Pitch Scale" } },
                { "miniatureAircraft", new[] { "MiniatureAircraft", "Miniature Aircraft", "AircraftSymbol", "Aircraft", "Center Symbol" } },
                { "fpvMarker", new[] { "FPV", "FlightPathVector", "FPVMarker", "Flight Path Vector" } },
                { "scaleTransform", new[] { "Scale", "Attitude Scale" } },
                
                // Airspeed Indicator  
                { "airspeedReadout", new[] { "Airspeed Readout", "AirspeedReadout", "Readout", "Text", "IAS" } },
                { "speedTape", new[] { "Tape", "SpeedTape", "Speed Tape", "Airspeed Tape" } },
                { "windowPanel", new[] { "Window", "Panel", "Frame" } },
                
                // Altimeter
                { "altitudeReadout", new[] { "Altitude Readout", "AltitudeReadout", "Readout", "Text", "ALT" } },
                { "altitudeTape", new[] { "Tape", "AltitudeTape", "Altitude Tape" } },
                
                // Heading Indicator
                { "headingReadout", new[] { "Compass Readout", "HeadingReadout", "Readout", "Text", "HDG", "365" } },
                { "compassCard", new[] { "Compass Card", "CompassCard", "Card", "Heading Tape" } },
                { "headingPanel", new[] { "Panel", "Heading Panel", "Window" } },
                
                // VSI
                { "digitalReadout", new[] { "Digital Display", "DigitalReadout", "Readout", "Text", "VS Display" } },
                { "vsiTape", new[] { "VSI Tape", "VSITape", "Tape", "VS Tape" } },
                { "vsiPointer", new[] { "Pointer", "VSI Pointer", "VS Pointer" } },
                
                // Torque Panel
                { "torquePointerL", new[] { "Torque Indicator L", "TorquePointerL", "Pointer L", "Left Pointer", "L Pointer" } },
                { "torquePointerR", new[] { "Torque Indicator R", "TorquePointerR", "Pointer R", "Right Pointer", "R Pointer" } },
                { "torqueFrame", new[] { "Torque Frame", "Frame", "Gauge Frame" } },
                { "torqueSafetyLimit", new[] { "Safety Limit", "Limit", "Red Line" } },
                { "torqueReadout", new[] { "Torque Readout", "Readout", "Text" } },
                
                // NR/RPM Indicator
                { "nrIndicatorFrame", new[] { "NR Frame", "Frame", "Gauge Frame" } },
                { "rpmCenterPointer", new[] { "RPM Center Pointer", "CenterPointer", "Center", "NR Pointer" } },
                { "rpmPointerL", new[] { "RPM Pointer L", "PointerL", "Left", "L Pointer", "ENG L" } },
                { "rpmPointerR", new[] { "RPM Pointer R", "PointerR", "Right", "R Pointer", "ENG R" } },
                { "rpmSafeRangeL", new[] { "Safe Range L", "SafeL", "Green Arc L" } },
                { "rpmSafeRangeR", new[] { "Safe Range R", "SafeR", "Green Arc R" } },
                { "rpmReadout", new[] { "RPM Readout", "NR Readout", "Readout", "Text" } },
                
                // Localizer (CDI)
                { "cdiNeedle", new[] { "CDI", "CDI Needle", "Needle", "Localizer Needle" } },
                { "deviationDotsPanel", new[] { "Deviation Dots", "Dots Panel", "CDI Dots" } },
                
                // Glidescope
                { "glidescopeNeedle", new[] { "Glidescope Needle", "GS Needle", "Needle", "Glideslope Needle" } },
                
                // Bank Scale
                { "bankScale", new[] { "Bank Scale", "BankScale", "Bank Arc" } },
                { "bankScaleIP", new[] { "Bank Scale IP", "BankScaleIP", "Inner Part" } },
                { "bankScaleArc", new[] { "Bank Scale", "BankScale", "Bank Arc" } },
                { "rollPointer", new[] { "Roll Pointer", "RollPointer", "Pointer", "Bank Pointer", "Triangle" } },
                { "slipSlider", new[] { "Slip Slider", "SlipSlider", "Slider", "Ball", "Slip Ball", "Inclinometer" } },
            };


            
            Transform elementTransform = element.transform;
            
            foreach (var pattern in patterns)
            {
                var prop = so.FindProperty(pattern.Key);
                if (prop == null) continue;
                
                // Skip if already assigned
                if (prop.objectReferenceValue != null) continue;
                
                // Search for matching child
                foreach (var childName in pattern.Value)
                {
                    Transform found = FindChildRecursive(elementTransform, childName);
                    if (found != null)
                    {
                        // Check property type and assign appropriate component
                        if (prop.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            string typeName = prop.type;
                            
                            if (typeName.Contains("TMP_Text") || typeName.Contains("TextMeshPro"))
                            {
                                // TMP_Text component
                                var tmp = found.GetComponent<TMP_Text>();
                                if (tmp != null)
                                {
                                    prop.objectReferenceValue = tmp;
                                    anyLinked = true;
                                }
                            }
                            else if (typeName.Contains("Image"))
                            {
                                // UnityEngine.UI.Image component
                                var img = found.GetComponent<UnityEngine.UI.Image>();
                                if (img != null)
                                {
                                    prop.objectReferenceValue = img;
                                    anyLinked = true;
                                }
                            }
                            else
                            {
                                // Default to RectTransform (most common for position/rotation animations)
                                var rt = found as RectTransform;
                                if (rt != null)
                                {
                                    prop.objectReferenceValue = rt;
                                    anyLinked = true;
                                }
                            }
                        }
                        
                        break;
                    }
                }
            }

            
            if (anyLinked)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(element);
            }
            
            return anyLinked;
        }
        
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
                
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            
            return null;
        }
        
        #endregion
    }
}
