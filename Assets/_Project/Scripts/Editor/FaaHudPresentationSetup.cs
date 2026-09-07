using System.Linq;
using FAA.Customization;
using HUDControl.Elements;
using TrafficRadar;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VoiceControl.UI;

/// <summary>Selective, repeatable presentation migration; no scene rebuild.</summary>
public static class FaaHudPresentationSetup
{
    [MenuItem("FAA/HUD/Apply Clean Pilot Presentation")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Apply the HUD presentation in edit mode so it can be saved.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene).ToArray();
        foreach (TMP_Text value in texts)
        {
            bool altitude = value.name == "AltReadoutText";
            if (value.name != "AirspeedReadoutText" && !altitude) continue;
            TMP_Text units = value.transform.parent.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t => t != value && (t.text.Contains("IAS") || t.text.Contains("ALT")));
            if (units == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(value.transform.parent.gameObject, "Style flight readout");
            var style = value.transform.parent.GetComponent<FaaPrimaryFlightReadout>() ??
                Undo.AddComponent<FaaPrimaryFlightReadout>(value.transform.parent.gameObject);
            style.Configure(value, units, altitude);
            // Fixed character cells prevent changing digit widths from shifting
            // the readout. Altitude grouping improves rapid thousands recognition.
            var element = value.GetComponentsInParent<MonoBehaviour>(true).FirstOrDefault(c =>
                c != null && c.GetType().Name == (altitude ? "AltimeterElement" : "AirspeedIndicatorElement"));
            if (element != null)
            {
                SerializedObject serialized = new SerializedObject(element);
                serialized.FindProperty("displayFormat").stringValue = altitude
                    ? "{0:#,##0}" : "<mspace=0.62em>{0:000}</mspace>";
                serialized.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(style);
        }
        foreach (var heading in Object.FindObjectsByType<FaaHeadingTapeOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (heading.gameObject.scene != scene) continue;
            Undo.RegisterFullObjectHierarchyUndo(heading.gameObject, "Position heading tape");
            heading.Configure(new Vector2(0f, -180f), new Vector2(520f, 64f),
                new Color(.2f, 1f, .2f, 1f), new Color(.2f, 1f, .2f, .60f));
            EditorUtility.SetDirty(heading);
        }
        foreach (var controls in Object.FindObjectsByType<FaaRadarControlsOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (controls.gameObject.scene != scene) continue;
            SerializedObject serialized = new SerializedObject(controls);
            serialized.FindProperty("showConfigurationButtonsOnStart").boolValue = false;
            serialized.FindProperty("startExpanded").boolValue = true;
            serialized.ApplyModifiedProperties();
            controls.SetRadarConfigurationVisible(FaaRadarKind.Weather, false, true);
            controls.SetRadarConfigurationVisible(FaaRadarKind.Traffic, false, true);
        }
        foreach (var menu in Object.FindObjectsByType<UIToolkitRadialMenuAdvanced>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (menu.gameObject.scene != scene) continue;
            Undo.RecordObject(menu, "Style HUD wheel menu");
            menu.ApplyAviationHudPreset();
            EditorUtility.SetDirty(menu);
        }
        ConfigureNavigationScales(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FAA HUD] Clean pilot presentation applied to " + scene.name + ". Save the scene to retain it.");
    }

    private static void ConfigureNavigationScales(Scene scene)
    {
        TrafficRadarDisplay radar = Object.FindAnyObjectByType<TrafficRadarDisplay>();
        foreach (var element in Object.FindObjectsByType<HUDControl.Core.HUDElementBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (element.gameObject.scene != scene) continue;
            bool vertical = element is GlidescopeElement;
            if (!vertical && !(element is LocalizerElement)) continue;
            SerializedObject serialized = new SerializedObject(element);
            RectTransform needle = serialized.FindProperty(vertical ? "glidescopeNeedle" : "cdiNeedle")?.objectReferenceValue as RectTransform;
            if (needle == null) continue; // Ignore the legacy component copies on individual dots.
            Undo.RegisterFullObjectHierarchyUndo(element.gameObject, "Style navigation scales");
            serialized.FindProperty("showNavigationTargetCue").boolValue = false;
            serialized.FindProperty("simulateDeviation").boolValue = false;
            serialized.ApplyModifiedProperties();
            foreach (Graphic legacy in element.GetComponentsInChildren<Graphic>(true))
            {
                if (legacy.GetComponentInParent<FaaNavigationScaleGraphic>() == null)
                {
                    legacy.enabled = false;
                    EditorUtility.SetDirty(legacy);
                }
            }
            Transform existing = element.transform.Find("FAA Clean Navigation Scale");
            GameObject go = existing != null ? existing.gameObject :
                new GameObject("FAA Clean Navigation Scale", typeof(RectTransform), typeof(FaaNavigationScaleGraphic));
            go.transform.SetParent(element.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = vertical ? new Vector2(needle.anchoredPosition.x, 0f) : Vector2.zero;
            rect.sizeDelta = vertical ? new Vector2(100f, 220f) : new Vector2(320f, 64f);
            rect.localScale = Vector3.one / 540f;
            var graphic = go.GetComponent<FaaNavigationScaleGraphic>();
            graphic.Configure(vertical, element as LocalizerElement, element as GlidescopeElement, radar);
            EditorUtility.SetDirty(graphic);
        }
    }
}
