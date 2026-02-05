using UnityEngine;
using UnityEditor;

namespace WeatherVisualization3D.Editor
{
    /// <summary>
    /// Creates prefabs for weather visualization effects
    /// </summary>
    public class WeatherPrefabCreator : MonoBehaviour
    {
        [MenuItem("Tools/Weather Visualization/Create Prefabs")]
        public static void CreatePrefabs()
        {
            string prefabPath = "Assets/_Project/Prefabs/WeatherVisualization";
            
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                string[] parts = prefabPath.Split('/');
                string currentPath = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }

            // Create Lightning Bolt Prefab
            CreateLightningPrefab(prefabPath);
            
            // Create Rain Particles Prefab
            CreateRainPrefab(prefabPath);
            
            // Create Snow Particles Prefab
            CreateSnowPrefab(prefabPath);

            AssetDatabase.SaveAssets();
            Debug.Log("[WeatherPrefabCreator] All prefabs created successfully!");
        }

        static void CreateLightningPrefab(string prefabPath)
        {
            GameObject go = new GameObject("LightningBolt");
            
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = new Color(1f, 0.95f, 0.8f, 1f);
            main.maxParticles = 10;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 1)
            });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.5f;
            
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/WeatherVisualization/LightningMaterial.mat");
            if (mat != null)
                renderer.material = mat;

            string path = prefabPath + "/LightningBolt.prefab";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
            
            Debug.Log($"[WeatherPrefabCreator] Created LightningBolt prefab at {path}");
        }

        static void CreateRainPrefab(string prefabPath)
        {
            GameObject go = new GameObject("RainParticles");
            
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 2f;
            main.startSpeed = 20f;
            main.startSize = 0.5f;
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.6f);
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 1000;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(100, 100, 100);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = -20f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.3f;
            
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/WeatherVisualization/RainMaterial.mat");
            if (mat != null)
                renderer.material = mat;

            string path = prefabPath + "/RainParticles.prefab";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
            
            Debug.Log($"[WeatherPrefabCreator] Created RainParticles prefab at {path}");
        }

        static void CreateSnowPrefab(string prefabPath)
        {
            GameObject go = new GameObject("SnowParticles");
            
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 4f;
            main.startSpeed = 2f;
            main.startSize = 0.3f;
            main.startColor = new Color(1f, 1f, 1f, 0.8f);
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.1f;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 500;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(100, 100, 100);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-2f, -1f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.3f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/WeatherVisualization/SnowMaterial.mat");
            if (mat != null)
                renderer.material = mat;

            string path = prefabPath + "/SnowParticles.prefab";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
            
            Debug.Log($"[WeatherPrefabCreator] Created SnowParticles prefab at {path}");
        }
    }
}
