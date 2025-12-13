using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AviationUI.Editor
{
    /// <summary>
    /// Editor window to rescale very small objects to normal size.
    /// Traverses children of an assigned parent and rescales them proportionally,
    /// preserving their relative scale AND position relationships.
    /// </summary>
    public class ObjectRescalerWindow : EditorWindow
    {
        private GameObject parentObject;
        private float scaleThreshold = 0.1f;
        private float targetBaseScale = 1.0f;
        private bool includeParent = false;
        private bool scalePositions = true; // Also scale positions proportionally
        private bool useLocalPositions = true; // Scale local positions (relative to parent)
        
        private Vector2 scrollPosition;
        private List<TransformScaleInfo> foundObjects = new List<TransformScaleInfo>();
        private float calculatedMultiplier = 1f;
        private float smallestScaleFound = 1f;
        private Vector3 pivotPoint = Vector3.zero;
        
        private class TransformScaleInfo
        {
            public Transform transform;
            public Vector3 originalScale;
            public Vector3 originalLocalPosition;
            public Vector3 originalWorldPosition;
            public Vector3 newScale;
            public Vector3 newLocalPosition;
            public bool shouldRescale;
            public bool isRectTransform;
            public Vector2 originalSizeDelta;
            public Vector2 newSizeDelta;
            
            public TransformScaleInfo(Transform t)
            {
                transform = t;
                originalScale = t.localScale;
                originalLocalPosition = t.localPosition;
                originalWorldPosition = t.position;
                newScale = originalScale;
                newLocalPosition = originalLocalPosition;
                shouldRescale = true;
                
                // Check for RectTransform (UI elements)
                RectTransform rt = t as RectTransform;
                if (rt != null)
                {
                    isRectTransform = true;
                    originalSizeDelta = rt.sizeDelta;
                    newSizeDelta = originalSizeDelta;
                }
            }
            
            public float GetSmallestAxis()
            {
                return Mathf.Min(Mathf.Abs(originalScale.x), 
                                 Mathf.Abs(originalScale.y), 
                                 Mathf.Abs(originalScale.z));
            }
        }
        
        [MenuItem("Tools/Aviation UI/Object Rescaler")]
        public static void ShowWindow()
        {
            var window = GetWindow<ObjectRescalerWindow>("Object Rescaler");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            // Header
            EditorGUILayout.LabelField("Object Rescaler (Proportional + Position)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool rescales very small objects while preserving their relative proportions. " +
                "It can also scale positions to maintain proper spacing between elements.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // Parent Object Field
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            parentObject = (GameObject)EditorGUILayout.ObjectField(
                "Parent Object", 
                parentObject, 
                typeof(GameObject), 
                true);
            
            if (EditorGUI.EndChangeCheck())
            {
                foundObjects.Clear();
                calculatedMultiplier = 1f;
            }
            
            includeParent = EditorGUILayout.Toggle("Include Parent", includeParent);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(5);
            
            // Scale Settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scale Settings", EditorStyles.boldLabel);
            
            scaleThreshold = EditorGUILayout.FloatField(
                new GUIContent("Scale Threshold", 
                    "Objects with any scale axis below this value are considered 'small'"),
                scaleThreshold);
            
            targetBaseScale = EditorGUILayout.FloatField(
                new GUIContent("Target Base Scale", 
                    "The smallest found object will be scaled to approximately this value."),
                targetBaseScale);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Position Handling", EditorStyles.boldLabel);
            
            scalePositions = EditorGUILayout.Toggle(
                new GUIContent("Scale Positions",
                    "IMPORTANT: Also scale positions proportionally so elements stay properly spaced apart."),
                scalePositions);
            
            if (scalePositions)
            {
                EditorGUI.indentLevel++;
                useLocalPositions = EditorGUILayout.Toggle(
                    new GUIContent("Use Local Positions",
                        "Scale local positions relative to parent. If off, uses world positions relative to pivot."),
                    useLocalPositions);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Scan Button
            EditorGUI.BeginDisabledGroup(parentObject == null);
            
            if (GUILayout.Button("Scan for Small Objects", GUILayout.Height(30)))
            {
                ScanForSmallObjects();
                CalculateProportionalScales();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);
            
            // Results
            if (foundObjects.Count > 0)
            {
                // Show calculated multiplier info
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Calculated Values", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField($"Smallest scale found: {smallestScaleFound:F8}");
                EditorGUILayout.LabelField($"Calculated multiplier: {calculatedMultiplier:F4}x");
                
                if (scalePositions)
                {
                    EditorGUILayout.LabelField($"Pivot point: ({pivotPoint.x:F4}, {pivotPoint.y:F4}, {pivotPoint.z:F4})");
                }
                
                EditorGUILayout.Space(5);
                
                // Allow manual multiplier adjustment
                EditorGUI.BeginChangeCheck();
                float newMultiplier = EditorGUILayout.FloatField("Custom Multiplier", calculatedMultiplier);
                if (EditorGUI.EndChangeCheck() && newMultiplier > 0)
                {
                    calculatedMultiplier = newMultiplier;
                    RecalculateNewScales();
                }
                
                if (GUILayout.Button("Recalculate from Target"))
                {
                    CalculateProportionalScales();
                }
                
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Count UI vs regular transforms
                int uiCount = foundObjects.Count(o => o.isRectTransform);
                int regularCount = foundObjects.Count - uiCount;
                EditorGUILayout.LabelField($"Found Objects: {foundObjects.Count} ({uiCount} UI, {regularCount} regular)", EditorStyles.boldLabel);
                
                // Select/Deselect All
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                {
                    foreach (var info in foundObjects)
                        info.shouldRescale = true;
                }
                if (GUILayout.Button("Deselect All"))
                {
                    foreach (var info in foundObjects)
                        info.shouldRescale = false;
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Column headers
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(20));
                EditorGUILayout.LabelField("Name", GUILayout.Width(100));
                EditorGUILayout.LabelField("Original Scale", GUILayout.Width(160));
                EditorGUILayout.LabelField("→ New Scale", GUILayout.Width(140));
                if (scalePositions)
                {
                    EditorGUILayout.LabelField("Pos Change", GUILayout.Width(100));
                }
                EditorGUILayout.EndHorizontal();
                
                // Scrollable list of found objects
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(180));
                
                foreach (var info in foundObjects)
                {
                    if (info.transform == null) continue;
                    
                    EditorGUILayout.BeginHorizontal();
                    
                    info.shouldRescale = EditorGUILayout.Toggle(info.shouldRescale, GUILayout.Width(20));
                    
                    // Clickable name with UI indicator
                    string displayName = info.transform.name;
                    if (displayName.Length > 12) displayName = displayName.Substring(0, 12) + "...";
                    if (info.isRectTransform) displayName = "[UI] " + displayName;
                    
                    if (GUILayout.Button(displayName, EditorStyles.linkLabel, GUILayout.Width(100)))
                    {
                        Selection.activeTransform = info.transform;
                        EditorGUIUtility.PingObject(info.transform);
                    }
                    
                    // Original scale (compact)
                    EditorGUILayout.LabelField(
                        $"({info.originalScale.x:F5}, {info.originalScale.y:F5}, {info.originalScale.z:F5})",
                        GUILayout.Width(160));
                    
                    // New scale (highlighted if different)
                    GUI.color = info.newScale != info.originalScale ? Color.green : Color.white;
                    EditorGUILayout.LabelField(
                        $"({info.newScale.x:F3}, {info.newScale.y:F3}, {info.newScale.z:F3})",
                        GUILayout.Width(140));
                    GUI.color = Color.white;
                    
                    // Position change
                    if (scalePositions)
                    {
                        Vector3 posDelta = info.newLocalPosition - info.originalLocalPosition;
                        float posMag = posDelta.magnitude;
                        GUI.color = posMag > 0.001f ? Color.cyan : Color.gray;
                        EditorGUILayout.LabelField($"Δ{posMag:F3}", GUILayout.Width(100));
                        GUI.color = Color.white;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(10);
                
                // Warning for UI elements
                if (uiCount > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Found {uiCount} UI elements (RectTransform). For UI, consider whether you should " +
                        "adjust the Canvas scaler or parent scale instead of individual elements.",
                        MessageType.Warning);
                }
                
                // Apply Button
                int selectedCount = foundObjects.Count(info => info.shouldRescale);
                
                EditorGUI.BeginDisabledGroup(selectedCount == 0);
                
                string buttonText = scalePositions 
                    ? $"Apply Scale + Position to {selectedCount} Object(s)" 
                    : $"Apply Scale to {selectedCount} Object(s)";
                
                GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
                if (GUILayout.Button(buttonText, GUILayout.Height(35)))
                {
                    ApplyProportionalScale();
                }
                GUI.backgroundColor = Color.white;
                
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.Space(5);
                
                // Alternative: Scale parent instead
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Alternative: Scale Parent Only", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Instead of scaling all children, you can scale just the parent object. " +
                    "This automatically scales all children's world positions and sizes correctly.",
                    MessageType.Info);
                
                if (GUILayout.Button($"Scale Parent '{parentObject.name}' by {calculatedMultiplier:F4}x"))
                {
                    ScaleParentOnly();
                }
                EditorGUILayout.EndVertical();
            }
            else if (parentObject != null)
            {
                EditorGUILayout.HelpBox(
                    "Click 'Scan for Small Objects' to find objects with small scale values.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Assign a parent object to get started.",
                    MessageType.Warning);
            }
        }
        
        private void ScanForSmallObjects()
        {
            foundObjects.Clear();
            
            if (parentObject == null) return;
            
            // Calculate pivot point (center of parent)
            pivotPoint = parentObject.transform.position;
            
            // Check parent if requested
            if (includeParent)
            {
                CheckTransform(parentObject.transform);
            }
            
            // Recursively check all children
            ScanChildren(parentObject.transform);
            
            Debug.Log($"[ObjectRescaler] Found {foundObjects.Count} small object(s) under '{parentObject.name}'");
        }
        
        private void ScanChildren(Transform parent)
        {
            foreach (Transform child in parent)
            {
                CheckTransform(child);
                ScanChildren(child); // Recursive
            }
        }
        
        private void CheckTransform(Transform t)
        {
            Vector3 scale = t.localScale;
            
            // Check if any axis is below threshold
            if (Mathf.Abs(scale.x) < scaleThreshold || 
                Mathf.Abs(scale.y) < scaleThreshold || 
                Mathf.Abs(scale.z) < scaleThreshold)
            {
                foundObjects.Add(new TransformScaleInfo(t));
            }
        }
        
        private void CalculateProportionalScales()
        {
            if (foundObjects.Count == 0) return;
            
            // Find the smallest scale value across all objects
            smallestScaleFound = float.MaxValue;
            
            foreach (var info in foundObjects)
            {
                float smallest = info.GetSmallestAxis();
                if (smallest > 0 && smallest < smallestScaleFound)
                {
                    smallestScaleFound = smallest;
                }
            }
            
            if (smallestScaleFound <= 0 || smallestScaleFound == float.MaxValue)
            {
                Debug.LogWarning("[ObjectRescaler] Could not find valid scale values.");
                calculatedMultiplier = 1f;
                return;
            }
            
            // Calculate multiplier to bring smallest to target
            calculatedMultiplier = targetBaseScale / smallestScaleFound;
            
            Debug.Log($"[ObjectRescaler] Smallest scale: {smallestScaleFound:F8}, Multiplier: {calculatedMultiplier:F4}x");
            
            RecalculateNewScales();
        }
        
        private void RecalculateNewScales()
        {
            foreach (var info in foundObjects)
            {
                // Scale the object's scale
                info.newScale = info.originalScale * calculatedMultiplier;
                
                // Scale the position relative to parent/pivot
                if (scalePositions)
                {
                    if (useLocalPositions)
                    {
                        // Scale local position directly
                        info.newLocalPosition = info.originalLocalPosition * calculatedMultiplier;
                    }
                    else
                    {
                        // Scale world position relative to pivot
                        Vector3 offset = info.originalWorldPosition - pivotPoint;
                        Vector3 newWorldPos = pivotPoint + (offset * calculatedMultiplier);
                        
                        // Convert back to local position
                        if (info.transform.parent != null)
                        {
                            info.newLocalPosition = info.transform.parent.InverseTransformPoint(newWorldPos);
                        }
                        else
                        {
                            info.newLocalPosition = newWorldPos;
                        }
                    }
                }
                else
                {
                    info.newLocalPosition = info.originalLocalPosition;
                }
                
                // For UI elements, also scale size delta
                if (info.isRectTransform)
                {
                    info.newSizeDelta = info.originalSizeDelta * calculatedMultiplier;
                }
            }
        }
        
        private void ApplyProportionalScale()
        {
            int count = 0;
            
            Undo.SetCurrentGroupName("Rescale Objects Proportionally");
            int undoGroup = Undo.GetCurrentGroup();
            
            foreach (var info in foundObjects)
            {
                if (!info.shouldRescale || info.transform == null) continue;
                
                Undo.RecordObject(info.transform, "Rescale Object");
                
                // Apply scale
                info.transform.localScale = info.newScale;
                
                // Apply position
                if (scalePositions)
                {
                    info.transform.localPosition = info.newLocalPosition;
                }
                
                EditorUtility.SetDirty(info.transform);
                count++;
            }
            
            Undo.CollapseUndoOperations(undoGroup);
            
            string positionNote = scalePositions ? " (with positions)" : "";
            Debug.Log($"[ObjectRescaler] Rescaled {count} object(s) with multiplier {calculatedMultiplier:F4}x{positionNote}");
            
            // Refresh the list with new values
            ScanForSmallObjects();
            CalculateProportionalScales();
        }
        
        private void ScaleParentOnly()
        {
            if (parentObject == null) return;
            
            Undo.RecordObject(parentObject.transform, "Scale Parent");
            
            Vector3 currentScale = parentObject.transform.localScale;
            parentObject.transform.localScale = currentScale * calculatedMultiplier;
            
            EditorUtility.SetDirty(parentObject.transform);
            
            Debug.Log($"[ObjectRescaler] Scaled parent '{parentObject.name}' by {calculatedMultiplier:F4}x");
            
            // Refresh
            ScanForSmallObjects();
            CalculateProportionalScales();
        }
    }
}
