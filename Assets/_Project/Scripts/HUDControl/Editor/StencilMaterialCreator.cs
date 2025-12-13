using UnityEngine;
using UnityEditor;
using System.IO;

namespace HUDControl.Editor
{
    /// <summary>
    /// Editor utility to create stencil mask and content materials for HUD masking.
    /// </summary>
    public class StencilMaterialCreator : EditorWindow
    {
        private int stencilID = 1;
        private bool showMaskGraphic = false;
        private string materialName = "AttitudeStencil";
        
        [MenuItem("Tools/HUD Control/Create Stencil Materials")]
        public static void ShowWindow()
        {
            var window = GetWindow<StencilMaterialCreator>("Stencil Materials");
            window.minSize = new Vector2(350, 250);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HUD Stencil Material Creator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates matching Mask and Content materials for HUD element masking.\n\n" +
                "• Apply MASK material to the shape defining visible area\n" +
                "• Apply CONTENT material to elements that should be masked", 
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            materialName = EditorGUILayout.TextField("Material Name", materialName);
            stencilID = EditorGUILayout.IntSlider("Stencil ID", stencilID, 1, 255);
            showMaskGraphic = EditorGUILayout.Toggle("Show Mask Graphic", showMaskGraphic);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                $"Will create:\n• {materialName}_Mask.mat\n• {materialName}_Content.mat", 
                MessageType.None);
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Create Materials", GUILayout.Height(35)))
            {
                CreateMaterials();
            }
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Create Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Attitude Indicator"))
            {
                CreatePreset("AttitudeIndicator", 1);
            }
            if (GUILayout.Button("Airspeed Tape"))
            {
                CreatePreset("AirspeedTape", 2);
            }
            if (GUILayout.Button("Altimeter Tape"))
            {
                CreatePreset("AltimeterTape", 3);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Heading Tape"))
            {
                CreatePreset("HeadingTape", 4);
            }
            if (GUILayout.Button("VSI"))
            {
                CreatePreset("VSI", 5);
            }
            if (GUILayout.Button("CDI/Glidescope"))
            {
                CreatePreset("CDI", 6);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void CreatePreset(string name, int id)
        {
            materialName = name;
            stencilID = id;
            showMaskGraphic = false;
            CreateMaterials();
        }
        
        private void CreateMaterials()
        {
            // Find the shaders
            Shader maskShader = Shader.Find("HUD/StencilMask");
            Shader contentShader = Shader.Find("HUD/StencilContent");
            
            if (maskShader == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Could not find shader 'HUD/StencilMask'.\n\n" +
                    "Make sure the shader file exists at:\n" +
                    "Assets/_Project/Shaders/HUD/StencilMask.shader", "OK");
                return;
            }
            
            if (contentShader == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Could not find shader 'HUD/StencilContent'.\n\n" +
                    "Make sure the shader file exists at:\n" +
                    "Assets/_Project/Shaders/HUD/StencilContent.shader", "OK");
                return;
            }
            
            // Create output directory
            string outputPath = "Assets/_Project/Materials/HUD";
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "_Project/Materials/HUD"));
                AssetDatabase.Refresh();
            }
            
            // Create Mask material
            Material maskMat = new Material(maskShader);
            maskMat.SetFloat("_StencilID", stencilID);
            maskMat.SetFloat("_ShowMask", showMaskGraphic ? 1 : 0);
            
            string maskPath = $"{outputPath}/{materialName}_Mask.mat";
            AssetDatabase.CreateAsset(maskMat, maskPath);
            
            // Create Content material
            Material contentMat = new Material(contentShader);
            contentMat.SetFloat("_StencilID", stencilID);
            
            string contentPath = $"{outputPath}/{materialName}_Content.mat";
            AssetDatabase.CreateAsset(contentMat, contentPath);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Select the created materials
            Selection.objects = new Object[] { maskMat, contentMat };
            
            Debug.Log($"[StencilMaterialCreator] Created materials:\n• {maskPath}\n• {contentPath}");
            EditorUtility.DisplayDialog("Success", 
                $"Created materials:\n\n" +
                $"• {materialName}_Mask.mat (apply to mask shape)\n" +
                $"• {materialName}_Content.mat (apply to content)\n\n" +
                $"Stencil ID: {stencilID}", "OK");
        }
    }
}
