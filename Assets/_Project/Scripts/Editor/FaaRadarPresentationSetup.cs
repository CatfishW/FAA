using System.Linq;
using FAA.Customization;
using FAA.XPlaneIntegration.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeatherRadar;

public static class FaaRadarPresentationSetup
{
    [MenuItem("FAA/HUD/Apply Clean Radar Presentation")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        var bridge = Object.FindObjectsByType<XPlane12ApiHudBridge>(FindObjectsInactive.Include)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        foreach (var root in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include))
        {
            if (root.gameObject.scene != scene) continue;
            bool weather = root.name == "X-Plane Weather Radar System";
            if (!weather && root.name != "Traffic Radar System") continue;
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Style radar instrument");
            var presentation = root.GetComponent<FaaRadarPresentation>() ?? Undo.AddComponent<FaaRadarPresentation>(root.gameObject);
            presentation.Configure(weather ? FaaRadarKind.Weather : FaaRadarKind.Traffic, bridge);
            presentation.SetDisplayEnabled(true);
            if (weather) root.GetComponentInChildren<XPlaneOriginalWeatherRadarDisplay>(true)?.PreparePilotPreview();
            EditorUtility.SetDirty(presentation);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FAA HUD] Clean radar presentation applied. Save the scene to retain the editor preview.");
    }
}
