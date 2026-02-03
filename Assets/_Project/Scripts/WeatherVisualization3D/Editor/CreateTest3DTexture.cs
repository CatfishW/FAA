using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D.Editor
{
    /// <summary>
    /// Creates a test 3D density texture for volumetric clouds
    /// </summary>
    public class CreateTest3DTexture : EditorWindow
    {
        [MenuItem("Tools/Weather Visualization/Create Test 3D Texture")]
        public static void CreateTexture()
        {
            int size = 64;
            Texture3D texture = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            texture.name = "TestDensityVolume";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            
            Color[] colors = new Color[size * size * size];
            
            // Create several cloud formations
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int index = z * size * size + y * size + x;
                        float density = 0;
                        float type = 0; // Weather type
                        float turbulence = 0;
                        
                        // Cloud 1: Large center storm (heavy intensity)
                        float d1 = Vector3.Distance(
                            new Vector3(x, y, z), 
                            new Vector3(size * 0.5f, size * 0.6f, size * 0.5f));
                        if (d1 < size * 0.2f)
                        {
                            float falloff = 1f - (d1 / (size * 0.2f));
                            density = Mathf.Max(density, falloff * falloff);
                            type = 4f / 255f; // Thunderstorm
                            turbulence = falloff * 0.8f;
                        }
                        
                        // Cloud 2: Side cell (moderate intensity)
                        float d2 = Vector3.Distance(
                            new Vector3(x, y, z), 
                            new Vector3(size * 0.25f, size * 0.5f, size * 0.75f));
                        if (d2 < size * 0.12f)
                        {
                            float falloff = 1f - (d2 / (size * 0.12f));
                            density = Mathf.Max(density, falloff * 0.8f);
                            type = 2f / 255f; // Moderate rain
                            turbulence = falloff * 0.5f;
                        }
                        
                        // Cloud 3: Another side cell (light intensity)
                        float d3 = Vector3.Distance(
                            new Vector3(x, y, z), 
                            new Vector3(size * 0.75f, size * 0.7f, size * 0.25f));
                        if (d3 < size * 0.1f)
                        {
                            float falloff = 1f - (d3 / (size * 0.1f));
                            density = Mathf.Max(density, falloff * 0.6f);
                            type = 1f / 255f; // Light rain
                            turbulence = falloff * 0.3f;
                        }
                        
                        colors[index] = new Color(density, type, turbulence, density > 0.5f ? 1f : 0f);
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            // Save as asset
            string folder = "Assets/_Project/Textures/WeatherVisualization";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            
            string path = folder + "/TestDensityVolume.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[CreateTest3DTexture] Created 3D texture at: {path}");
            
            // Assign to material
            AssignToMaterial(texture);
            
            EditorUtility.DisplayDialog("Success", 
                $"Created 3D texture at:\n{path}\n\nAnd assigned to VolumetricCloud material.", "OK");
        }
        
        static void AssignToMaterial(Texture3D texture)
        {
            string matPath = "Assets/_Project/Materials/WeatherVisualization/VolumetricCloud.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            
            if (mat != null)
            {
                mat.SetTexture("_DensityVolume", texture);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                Debug.Log("[CreateTest3DTexture] Assigned texture to VolumetricCloud material");
            }
            else
            {
                Debug.LogError($"[CreateTest3DTexture] Material not found at: {matPath}");
            }
        }
    }
}
