using UnityEngine;
using UnityEditor;

namespace FAA.Editor
{
    public class SceneHierarchyOrganizer : EditorWindow
    {
        [MenuItem("FAA/Organize Scene Hierarchy")]
        public static void OrganizeHierarchy()
        {
            // Create parent groups
            GameObject systemsGroup = CreateGroup("⚙️ Systems", 0);
            GameObject lightingGroup = CreateGroup("💡 Lighting", 1);
            GameObject weatherGroup = CreateGroup("🌦️ Weather", 2);
            GameObject aircraftGroup = CreateGroup("✈️ Aircraft", 3);
            GameObject mapGroup = CreateGroup("🗺️ Map", 4);
            GameObject uiGroup = CreateGroup("🎮 UI", 5);
            GameObject audioGroup = CreateGroup("🎤 Audio", 6);

            // Move objects to their groups
            MoveToGroup("EventSystem", systemsGroup);
            MoveToGroup("GeoPosUnityPosProjectManager", systemsGroup);
            MoveToGroup("CesiumGeoreference", systemsGroup);

            MoveToGroup("Directional Light", lightingGroup);

            MoveToGroup("Weather3D_System", weatherGroup);
            MoveToGroup("WeatherVisualization3D", weatherGroup);
            
            // Move weather particles under WeatherVisualization3D
            GameObject weatherViz = GameObject.Find("WeatherVisualization3D");
            if (weatherViz != null)
            {
                MoveToParent("LightningBolt", weatherViz);
                MoveToParent("RainParticles", weatherViz);
                MoveToParent("SnowParticles", weatherViz);
            }

            MoveToGroup("OwnAircraft", aircraftGroup);

            MoveToGroup("OnlineMap", mapGroup);

            MoveToGroup("FAASymbologyCanvas", uiGroup);
            MoveToGroup("HUDController", uiGroup);
            MoveToGroup("Visual Understanding Manager", uiGroup);

            MoveToGroup("VoiceControlSystem", audioGroup);

            Debug.Log("Scene hierarchy organized successfully!");
        }

        private static GameObject CreateGroup(string name, int siblingIndex)
        {
            GameObject group = GameObject.Find(name);
            if (group == null)
            {
                group = new GameObject(name);
                group.transform.SetSiblingIndex(siblingIndex);
            }
            return group;
        }

        private static void MoveToGroup(string objectName, GameObject parent)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null && parent != null)
            {
                Undo.SetTransformParent(obj.transform, parent.transform, "Organize Hierarchy");
            }
        }

        private static void MoveToParent(string objectName, GameObject parent)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null && parent != null)
            {
                Undo.SetTransformParent(obj.transform, parent.transform, "Organize Hierarchy");
            }
        }
    }
}
