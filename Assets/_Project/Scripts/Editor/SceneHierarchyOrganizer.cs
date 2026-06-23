using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FAA.Editor
{
    public class SceneHierarchyOrganizer : EditorWindow
    {
        private const string SceneRootName = "FAA_Scene";
        private const string SystemsRootName = "_Systems";
        private const string GameplayRootName = "_Gameplay";
        private const string UiRootName = "_UI";
        private const string EnvironmentRootName = "_Environment";
        private const string WeatherRootName = "_Weather";
        private const string MapsRootName = "_Maps";
        private const string LightingRootName = "_Lighting";

        [MenuItem("FAA/Organize Scene Hierarchy")]
        [MenuItem("FAA/Scene/Organize Experiment Scene Hierarchy")]
        public static void OrganizeHierarchy()
        {
            GameObject sceneRoot = CreateRoot(SceneRootName, 0);
            GameObject systemsRoot = CreateRoot(SystemsRootName, 0, sceneRoot);
            GameObject gameplayRoot = CreateRoot(GameplayRootName, 1, sceneRoot);
            GameObject uiRoot = CreateRoot(UiRootName, 2, sceneRoot);
            GameObject environmentRoot = CreateRoot(EnvironmentRootName, 3, sceneRoot);
            GameObject weatherRoot = CreateRoot(WeatherRootName, 0, environmentRoot);
            GameObject mapsRoot = CreateRoot(MapsRootName, 1, environmentRoot);
            GameObject lightingRoot = CreateRoot(LightingRootName, 2, environmentRoot);

            MoveToGroup("[MANAGERS]", systemsRoot);
            MoveToGroup("GeoPosUnityPosProjectManager", systemsRoot);
            MoveToGroup("EventSystem", systemsRoot);
            MoveToGroup("CesiumGeoreference", mapsRoot);

            MoveToGroup("OwnAircraft", gameplayRoot);
            MoveToGroup("TrafficEntities", gameplayRoot);

            MoveToGroup("FAASymbologyCanvas", uiRoot);
            MoveToGroup("HUDController", uiRoot);
            MoveToGroup("FAA HUD Mode Switcher", uiRoot);
            MoveToGroup("FAA UI Toolkit HUD", uiRoot);
            MoveToGroup("UI Toolkit Radial Menu (Advanced)", uiRoot);
            MoveToGroup("Visual Understanding Manager", systemsRoot);
            MoveToGroup("VoiceControlSystem", systemsRoot);

            MoveToGroup("WeatherVisualization3D", weatherRoot);
            MoveToGroup("Weather3D_System", weatherRoot);
            MoveToGroup("UniStorm System", weatherRoot);
            MoveToGroup("OnlineMap", mapsRoot);
            MoveToGroup("Directional Light", lightingRoot);
            MoveToGroup("Main Camera", gameplayRoot);

            MoveWeatherEffectUnderWeatherVisualization("LightningBolt");
            MoveWeatherEffectUnderWeatherVisualization("RainParticles");
            MoveWeatherEffectUnderWeatherVisualization("SnowParticles");

            RemoveEmptyLegacyGroups();
            MarkActiveSceneDirty();
            Debug.Log("[SceneHierarchyOrganizer] FAA scene hierarchy organized.");
        }

        private static GameObject CreateRoot(string name, int siblingIndex, GameObject parent = null)
        {
            GameObject group = FindInActiveScene(name);
            if (group == null)
            {
                group = new GameObject(name);
            }

            if (parent != null && group.transform.parent != parent.transform)
            {
                Undo.SetTransformParent(group.transform, parent.transform, "Organize FAA hierarchy");
            }

            group.transform.SetSiblingIndex(siblingIndex);
            return group;
        }

        private static void MoveToGroup(string objectName, GameObject parent)
        {
            GameObject obj = FindInActiveScene(objectName);
            if (obj != null && parent != null && obj != parent && obj.transform.parent != parent.transform)
            {
                Undo.SetTransformParent(obj.transform, parent.transform, "Organize FAA hierarchy");
            }
        }

        private static void MoveWeatherEffectUnderWeatherVisualization(string objectName)
        {
            GameObject weatherViz = FindInActiveScene("WeatherVisualization3D");
            GameObject obj = FindInActiveScene(objectName);
            if (obj != null && weatherViz != null && obj.transform.parent != weatherViz.transform)
            {
                Undo.SetTransformParent(obj.transform, weatherViz.transform, "Organize FAA hierarchy");
            }
        }

        private static void RemoveEmptyLegacyGroups()
        {
            string[] legacyGroups =
            {
                "Systems",
                "Lighting",
                "Weather",
                "Aircraft",
                "Map",
                "UI",
                "Audio"
            };

            foreach (string legacyGroup in legacyGroups)
            {
                GameObject group = FindInActiveScene(legacyGroup);
                if (group != null && group.transform.childCount == 0)
                {
                    Undo.DestroyObjectImmediate(group);
                }
            }
        }

        private static void MarkActiveSceneDirty()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static GameObject FindInActiveScene(string name)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name == name && obj.scene == activeScene && !EditorUtility.IsPersistent(obj))
                {
                    return obj;
                }
            }

            return null;
        }
    }
}
