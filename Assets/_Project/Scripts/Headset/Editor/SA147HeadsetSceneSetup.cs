#if UNITY_EDITOR
using System.Collections.Generic;
using FAA.Headset;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FAA.Headset.Editor
{
    public static class SA147HeadsetSceneSetup
    {
        private const string ExperimentScenePath = "Assets/_Project/Scenes/ExperimentScene.unity";
        private const string SceneRootName = "FAA_Scene";
        private const string CompatibilityObjectName = "SA-147 Headset Compatibility";
        private const string RigObjectName = "SA_147_Prefab";
        private const string ArcherObjectName = "SA-147 Archer Head Tracker";
        private const string SA147PrefabPath = "Assets/VisionProducts/SA_147_Prefab.prefab";

        [MenuItem("FAA/Headset/Configure SA-147S In Experiment Scene")]
        public static void ConfigureExperimentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ExperimentScenePath)
            {
                scene = EditorSceneManager.OpenScene(ExperimentScenePath, OpenSceneMode.Single);
            }

            GameObject parent = FindSingleSceneObject(SceneRootName, scene);
            if (parent == null)
            {
                parent = new GameObject(SceneRootName);
            }

            GameObject compatibilityObject = FindSingleSceneObject(CompatibilityObjectName, scene);
            if (compatibilityObject == null)
            {
                compatibilityObject = new GameObject(CompatibilityObjectName);
            }

            compatibilityObject.transform.SetParent(parent.transform, false);
            SA147HeadsetCompatibility compatibility =
                compatibilityObject.GetComponent<SA147HeadsetCompatibility>() ??
                compatibilityObject.AddComponent<SA147HeadsetCompatibility>();

            GameObject rig = EnsureRig(parent.transform, scene);
            GameObject archerBridge = EnsureArcherBridge(parent.transform, scene, rig);
            ConfigureCompatibility(compatibility, rig, archerBridge);
            ConfigureRigCameras(rig);

            EditorUtility.SetDirty(compatibilityObject);
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(archerBridge);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SA147] ExperimentScene configured for SA Photonics / Vision Products SA-147/S headset output.");
        }

        private static GameObject EnsureRig(Transform parent, Scene scene)
        {
            GameObject rig = FindSingleSceneObject(RigObjectName, scene);
            if (rig != null)
            {
                rig.transform.SetParent(parent, false);
                return rig;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SA147PrefabPath);
            if (prefab == null)
            {
                throw new System.IO.FileNotFoundException("SA-147 prefab missing", SA147PrefabPath);
            }

            rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.name = RigObjectName;
            rig.transform.SetParent(parent, false);
            rig.transform.localPosition = Vector3.zero;
            rig.transform.localRotation = Quaternion.identity;
            rig.transform.localScale = Vector3.one;
            rig.SetActive(false);
            return rig;
        }

        private static GameObject EnsureArcherBridge(Transform parent, Scene scene, GameObject rig)
        {
            GameObject bridge = FindSingleSceneObject(ArcherObjectName, scene);
            if (bridge == null)
            {
                bridge = new GameObject(ArcherObjectName);
            }

            bridge.transform.SetParent(parent, false);
            bridge.transform.localPosition = Vector3.zero;
            bridge.transform.localRotation = Quaternion.identity;
            bridge.transform.localScale = Vector3.one;

            global::ArcherInterface archer =
                bridge.GetComponent<global::ArcherInterface>() ??
                bridge.AddComponent<global::ArcherInterface>();
            archer.SAPrefab = rig;
            bridge.SetActive(false);
            return bridge;
        }

        private static GameObject FindSingleSceneObject(string objectName, Scene scene)
        {
            List<GameObject> matches = FindSceneObjects(objectName, scene);
            if (matches.Count == 0)
            {
                return null;
            }

            GameObject keep = matches[0];
            for (int i = 1; i < matches.Count; i++)
            {
                Object.DestroyImmediate(matches[i]);
            }

            return keep;
        }

        private static List<GameObject> FindSceneObjects(string objectName, Scene scene)
        {
            List<GameObject> matches = new List<GameObject>();
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject gameObject in allObjects)
            {
                if (gameObject == null || EditorUtility.IsPersistent(gameObject))
                {
                    continue;
                }

                if (gameObject.scene != scene || gameObject.name != objectName)
                {
                    continue;
                }

                matches.Add(gameObject);
            }

            return matches;
        }

        private static void ConfigureCompatibility(SA147HeadsetCompatibility compatibility, GameObject rig, GameObject archerBridge)
        {
            SerializedObject serialized = new SerializedObject(compatibility);
            SetBool(serialized, "enableOnStart", false);
            SetBool(serialized, "autoEnableWhenHeadsetDisplaysPresent", true);
            SetBool(serialized, "activateAdditionalDisplays", true);
            SetBool(serialized, "setFullscreenResolution", false);
            SetObject(serialized, "sa147Rig", rig);
            SetObject(serialized, "archerBridge", archerBridge);
            SetBool(serialized, "enableArcherTracker", true);
            SetInt(serialized, "leftDisplayIndex", 1);
            SetInt(serialized, "rightDisplayIndex", 2);
            SetInt(serialized, "perEyeWidth", 1920);
            SetInt(serialized, "perEyeHeight", 1200);
            SetFloat(serialized, "verticalFovDegrees", 33f);
            SetFloat(serialized, "horizontalFovDegrees", 53f);
            SetBool(serialized, "mirrorOverlayCanvasesToRightEye", true);
            SetBool(serialized, "routeOverlayCanvasesToLeftEye", true);
            SetBool(serialized, "renderHudThroughHeadsetPrewarp", true);
            SetInt(serialized, "hudCaptureLayer", 31);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRigCameras(GameObject rig)
        {
            Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
            {
                string cameraName = camera.gameObject.name;
                if (cameraName.Contains("Left"))
                {
                    camera.targetDisplay = 1;
                }
                else if (cameraName.Contains("Right"))
                {
                    camera.targetDisplay = 2;
                }

                camera.stereoTargetEye = StereoTargetEyeMask.None;
                camera.allowHDR = false;
                camera.fieldOfView = 33f;
                camera.aspect = Mathf.Abs(Mathf.Tan(Mathf.Deg2Rad * 53f * 0.5f) / Mathf.Tan(Mathf.Deg2Rad * 33f * 0.5f));
                EditorUtility.SetDirty(camera);
            }
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
#endif
