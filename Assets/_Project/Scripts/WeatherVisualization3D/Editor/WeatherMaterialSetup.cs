using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D.Editor
{
    /// <summary>
    /// Sets up weather visualization materials with proper textures and settings
    /// </summary>
    public class WeatherMaterialSetup : MonoBehaviour
    {
        [MenuItem("Tools/Weather Visualization/Setup Materials")]
        public static void SetupMaterials()
        {
            // Lightning Material
            var lightningMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/WeatherVisualization/LightningMaterial.mat");
            if (lightningMat != null)
            {
                var lightningTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/_Project/Textures/WeatherVisualization/LightningBolt.png");
                if (lightningTex != null)
                {
                    lightningMat.SetTexture("_MainTex", lightningTex);
                    lightningMat.SetColor("_Color", new Color(1f, 0.95f, 0.8f, 1f));
                    lightningMat.SetFloat("_Mode", 2); // Fade
                    lightningMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    lightningMat.SetFloat("_ZWrite", 0);
                    lightningMat.EnableKeyword("_ALPHABLEND_ON");
                    lightningMat.renderQueue = 3000;
                    EditorUtility.SetDirty(lightningMat);
                    Debug.Log("[WeatherMaterialSetup] Configured LightningMaterial");
                }
            }

            // Rain Material
            var rainMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/WeatherVisualization/RainMaterial.mat");
            if (rainMat != null)
            {
                var rainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/_Project/Textures/WeatherVisualization/RainDrop.png");
                if (rainTex != null)
                {
                    rainMat.SetTexture("_MainTex", rainTex);
                    rainMat.SetColor("_Color", new Color(0.8f, 0.9f, 1f, 0.6f));
                    rainMat.SetFloat("_Mode", 2); // Fade
                    rainMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    rainMat.SetFloat("_ZWrite", 0);
                    rainMat.EnableKeyword("_ALPHABLEND_ON");
                    rainMat.renderQueue = 3000;
                    EditorUtility.SetDirty(rainMat);
                    Debug.Log("[WeatherMaterialSetup] Configured RainMaterial");
                }
            }

            // Snow Material
            var snowMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/WeatherVisualization/SnowMaterial.mat");
            if (snowMat != null)
            {
                var snowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/_Project/Textures/WeatherVisualization/Snowflake.png");
                if (snowTex != null)
                {
                    snowMat.SetTexture("_MainTex", snowTex);
                    snowMat.SetColor("_Color", new Color(1f, 1f, 1f, 0.8f));
                    snowMat.SetFloat("_Mode", 2); // Fade
                    snowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    snowMat.SetFloat("_ZWrite", 0);
                    snowMat.EnableKeyword("_ALPHABLEND_ON");
                    snowMat.renderQueue = 3000;
                    EditorUtility.SetDirty(snowMat);
                    Debug.Log("[WeatherMaterialSetup] Configured SnowMaterial");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WeatherMaterialSetup] All materials configured successfully!");
        }
    }
}
