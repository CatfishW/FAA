using System.Linq;
using FAA.Customization;
using FAA.XPlaneIntegration.Runtime;
using HUDControl.Elements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FaaEngineInstrumentSetup
{
    [MenuItem("FAA/HUD/Apply Vector Engine Instruments")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var bridge = Object.FindObjectsByType<XPlane12ApiHudBridge>(FindObjectsInactive.Include).FirstOrDefault(x => x.gameObject.scene == scene);
        foreach (var root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (root.gameObject.scene != scene) continue;
            FaaEngineInstrument kind;
            Vector2 offset, size;
            if (root.name == "Torque Panel" && root.GetComponentInChildren<TorquePanelElement>(true) != null)
            { kind = FaaEngineInstrument.Torque; offset = new(.017f, .088f); size = new(178, 234); }
            else if (root.name == "NR/ENG Ind" && root.GetComponentInChildren<NRIndicatorElement>(true) != null)
            { kind = FaaEngineInstrument.EngineSpeed; offset = new(0, .105f); size = new(178, 234); }
            else if (root.name == "VSI" && root.GetComponent<VSIElement>() != null)
            { kind = FaaEngineInstrument.VerticalSpeed; offset = new(.0463f, 0); size = new(158, 272); }
            else continue;
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Refine engine and vertical speed instruments");
            var existing = root.Find("FAA Vector Instrument");
            var go = existing != null ? existing.gameObject : new GameObject("FAA Vector Instrument", typeof(RectTransform));
            if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Create vector instrument");
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one * .5f;
            rt.anchoredPosition = offset; rt.sizeDelta = size;
            rt.localScale = Vector3.one * FaaPitchLadderGraphic.LegacyUnitsPerReferencePixel;
            var graphic = go.GetComponent<FaaEngineInstrumentGraphic>() ?? Undo.AddComponent<FaaEngineInstrumentGraphic>(go);
            var legacy = root.GetComponentsInChildren<Graphic>(true).Where(g => !g.transform.IsChildOf(go.transform)).ToArray();
            foreach (var component in root.GetComponents<MonoBehaviour>())
            {
                var serialized = new SerializedObject(component);
                foreach (string field in new[] { "showNumericReadouts", "showBarLabels", "showScaleLabels" })
                {
                    var property = serialized.FindProperty(field);
                    if (property != null && property.propertyType == SerializedPropertyType.Boolean) property.boolValue = false;
                }
                serialized.ApplyModifiedProperties();
            }
            graphic.Configure(kind, bridge, legacy);
            EditorUtility.SetDirty(graphic);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
