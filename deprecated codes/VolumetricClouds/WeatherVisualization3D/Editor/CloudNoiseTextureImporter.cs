using UnityEngine;
using UnityEditor;
using System.IO;

namespace WeatherVisualization3D
{
    /// <summary>
    /// Imports 2D noise texture slices as 3D textures for volumetric clouds
    /// Based on UnityVolumetricCloudsURP approach
    /// </summary>
    public class CloudNoiseTextureImporter : AssetPostprocessor
    {
        [MenuItem("Weather/Import Cloud Noise Textures")]
        public static void ImportAllNoiseTextures()
        {
            string noisePath = "Assets/_Project/Textures/CloudNoise";

            // Import Worley 128 RGBA as 3D texture
            ImportWorley128RGBA(noisePath + "/WorleyNoise128RGBA.png");

            // Import Worley 32 RGB as 3D texture
            ImportWorley32RGB(noisePath + "/WorleyNoise32RGB.png");

            // Import Perlin 32 RGB as 3D texture
            ImportPerlin32RGB(noisePath + "/PerlinNoise32RGB.png");

            AssetDatabase.Refresh();
            Debug.Log("[CloudNoiseTextureImporter] All noise textures imported successfully!");
        }

        /// <summary>
        /// Worley 128 RGBA is a 128x128x128 3D noise packed into a 2D image
        /// Layout: 128x(128*128) pixels - each column is a Z slice
        /// </summary>
        private static void ImportWorley128RGBA(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[CloudNoiseTextureImporter] File not found: {path}");
                return;
            }

            byte[] pngData = File.ReadAllBytes(path);
            Texture2D sourceTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sourceTex.LoadImage(pngData);

            int size = 128;
            Texture3D tex3D = new Texture3D(size, size, size, TextureFormat.RGBA32, false);
            tex3D.name = "WorleyNoise128RGBA";
            tex3D.wrapMode = TextureWrapMode.Repeat;
            tex3D.filterMode = FilterMode.Trilinear;

            Color[] pixels3D = new Color[size * size * size];
            Color[] sourcePixels = sourceTex.GetPixels();

            // Unpack 2D image into 3D texture
            // Layout: X = u, Y = v, Z slices are stored vertically
            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx3D = x + y * size + z * size * size;
                        int idx2D = x + (y + z * size) * size;

                        if (idx2D < sourcePixels.Length)
                            pixels3D[idx3D] = sourcePixels[idx2D];
                    }
                }
            }

            tex3D.SetPixels(pixels3D);
            tex3D.Apply();

            // Save as asset
            string assetPath = "Assets/_Project/Textures/CloudNoise/WorleyNoise128RGBA_3D.asset";
            AssetDatabase.CreateAsset(tex3D, assetPath);

            Object.DestroyImmediate(sourceTex);

            Debug.Log($"[CloudNoiseTextureImporter] Created 3D texture: {assetPath}");
        }

        /// <summary>
        /// Worley 32 RGB is a 32x32x32 3D noise
        /// </summary>
        private static void ImportWorley32RGB(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[CloudNoiseTextureImporter] File not found: {path}");
                return;
            }

            byte[] pngData = File.ReadAllBytes(path);
            Texture2D sourceTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            sourceTex.LoadImage(pngData);

            int size = 32;
            Texture3D tex3D = new Texture3D(size, size, size, TextureFormat.RGB24, false);
            tex3D.name = "WorleyNoise32RGB";
            tex3D.wrapMode = TextureWrapMode.Repeat;
            tex3D.filterMode = FilterMode.Trilinear;

            Color[] pixels3D = new Color[size * size * size];
            Color[] sourcePixels = sourceTex.GetPixels();

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx3D = x + y * size + z * size * size;
                        int idx2D = x + (y + z * size) * size;

                        if (idx2D < sourcePixels.Length)
                            pixels3D[idx3D] = sourcePixels[idx2D];
                    }
                }
            }

            tex3D.SetPixels(pixels3D);
            tex3D.Apply();

            string assetPath = "Assets/_Project/Textures/CloudNoise/WorleyNoise32RGB_3D.asset";
            AssetDatabase.CreateAsset(tex3D, assetPath);

            Object.DestroyImmediate(sourceTex);

            Debug.Log($"[CloudNoiseTextureImporter] Created 3D texture: {assetPath}");
        }

        /// <summary>
        /// Perlin 32 RGB is a 32x32x32 3D noise
        /// </summary>
        private static void ImportPerlin32RGB(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[CloudNoiseTextureImporter] File not found: {path}");
                return;
            }

            byte[] pngData = File.ReadAllBytes(path);
            Texture2D sourceTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            sourceTex.LoadImage(pngData);

            int size = 32;
            Texture3D tex3D = new Texture3D(size, size, size, TextureFormat.RGB24, false);
            tex3D.name = "PerlinNoise32RGB";
            tex3D.wrapMode = TextureWrapMode.Repeat;
            tex3D.filterMode = FilterMode.Trilinear;

            Color[] pixels3D = new Color[size * size * size];
            Color[] sourcePixels = sourceTex.GetPixels();

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int idx3D = x + y * size + z * size * size;
                        int idx2D = x + (y + z * size) * size;

                        if (idx2D < sourcePixels.Length)
                            pixels3D[idx3D] = sourcePixels[idx2D];
                    }
                }
            }

            tex3D.SetPixels(pixels3D);
            tex3D.Apply();

            string assetPath = "Assets/_Project/Textures/CloudNoise/PerlinNoise32RGB_3D.asset";
            AssetDatabase.CreateAsset(tex3D, assetPath);

            Object.DestroyImmediate(sourceTex);

            Debug.Log($"[CloudNoiseTextureImporter] Created 3D texture: {assetPath}");
        }

        [MenuItem("Weather/Create Cloud Material")]
        public static void CreateCloudMaterial()
        {
            // Find the shader
            Shader shader = Shader.Find("WeatherVisualization3D/VolumetricCloudWeatherURP");
            if (shader == null)
            {
                Debug.LogError("[CloudNoiseTextureImporter] Shader not found! Make sure the shader file exists and has no errors.");
                return;
            }

            // Create material
            Material material = new Material(shader);
            material.name = "VolumetricCloudWeatherMaterial";

            // Try to assign 3D textures if they exist
            Texture3D worley128 = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Project/Textures/CloudNoise/WorleyNoise128RGBA_3D.asset");
            Texture3D erosion = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Project/Textures/CloudNoise/WorleyNoise32RGB_3D.asset");
            Texture3D perlin = AssetDatabase.LoadAssetAtPath<Texture3D>("Assets/_Project/Textures/CloudNoise/PerlinNoise32RGB_3D.asset");

            if (worley128 != null)
                material.SetTexture("_WorleyNoise", worley128);
            if (erosion != null)
                material.SetTexture("_ErosionNoise", erosion);
            if (perlin != null)
                material.SetTexture("_PerlinNoise", perlin);

            // Default settings for good visibility
            material.SetFloat("_DensityMultiplier", 0.5f);
            material.SetFloat("_ShapeScale", 3.0f);
            material.SetFloat("_ErosionScale", 50.0f);
            material.SetFloat("_RaymarchSteps", 48);
            material.SetFloat("_StepSize", 500);
            material.SetFloat("_LightAbsorption", 0.3f);
            material.SetFloat("_SunIntensity", 1.5f);

            // Save material
            string materialPath = "Assets/_Project/Materials/VolumetricCloudWeatherMaterial.mat";
            AssetDatabase.CreateAsset(material, materialPath);

            Debug.Log($"[CloudNoiseTextureImporter] Created material: {materialPath}");

            // Ping the material in Project window
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = material;
        }
    }
}
