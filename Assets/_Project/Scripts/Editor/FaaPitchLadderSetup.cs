using System.Linq;
using FAA.Customization;
using FAA.XPlaneIntegration.Runtime;
using HUDControl.Elements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Retires the stretched bitmap without rebuilding the user's HUD.</summary>
public static class FaaPitchLadderSetup
{
    [MenuItem("FAA/HUD/Apply Vector Pitch Ladder")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        var bridge = Object.FindObjectsByType<XPlane12ApiHudBridge>(FindObjectsInactive.Include)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        foreach (var attitude in Object.FindObjectsByType<AttitudeIndicatorElement>(FindObjectsInactive.Include))
        {
            if (attitude.gameObject.scene != scene) continue;
            RectTransform mask = attitude.transform.Find("ScaleMasker") as RectTransform;
            if (mask == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(attitude.gameObject, "Replace bitmap pitch ladder");
            // Space for a real drift angle without stretching the pitch marks
            // or clipping labels at a small bank. The IAS/ALT readout columns
            // remain outside this 400-reference-pixel attitude viewport.
            mask.sizeDelta = new Vector2(Mathf.Max(mask.sizeDelta.x, 400f * FaaPitchLadderGraphic.LegacyUnitsPerReferencePixel), mask.sizeDelta.y);
            var legacy = new[] { mask.Find("Scale"), mask.Find("ScaleIteration2"), mask.Find("FPV"), attitude.transform.Find("Miniature Aircraft") }
                .Where(t => t != null).Select(t => t.GetComponent<Graphic>()).Where(g => g != null).ToArray();
            Transform existing = mask.Find("FAA Vector Pitch Ladder");
            GameObject go = existing != null ? existing.gameObject : new GameObject("FAA Vector Pitch Ladder", typeof(RectTransform));
            if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Create vector pitch ladder");
            go.transform.SetParent(mask, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = mask.rect.size / FaaPitchLadderGraphic.LegacyUnitsPerReferencePixel;
            rect.localScale = Vector3.one * FaaPitchLadderGraphic.LegacyUnitsPerReferencePixel;
            rect.localRotation = Quaternion.identity;
            var graphic = go.GetComponent<FaaPitchLadderGraphic>() ?? Undo.AddComponent<FaaPitchLadderGraphic>(go);
            graphic.Configure(bridge, Camera.main, legacy);
            foreach (Graphic old in legacy) EditorUtility.SetDirty(old);
            EditorUtility.SetDirty(graphic);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FAA HUD] Vector pitch ladder applied in the editor. Save the scene to retain it. XR optical calibration is still required.");
    }
}
