#if UNITY_EDITOR
using FAA.Customization;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit reference authoring on existing scene geometry. Never silently saves the user's scene.</summary>
public static class FaaRotorcraftCueAuthoring
{
    [MenuItem("FAA/HUD/Scene Reference/Landing Area on Selected Object")]
    private static void Landing() => Add(FaaRotorcraftCueAnchor.CueType.LandingArea);
    [MenuItem("FAA/HUD/Scene Reference/Waypoint on Selected Object")]
    private static void Waypoint() => Add(FaaRotorcraftCueAnchor.CueType.Waypoint);
    [MenuItem("FAA/HUD/Scene Reference/Obstacle on Selected Object")]
    private static void Obstacle() => Add(FaaRotorcraftCueAnchor.CueType.Obstacle);
    [MenuItem("FAA/HUD/Scene Reference/Hover Reference on Selected Object")]
    private static void Hover() => Add(FaaRotorcraftCueAnchor.CueType.HoverReference);

    private static void Add(FaaRotorcraftCueAnchor.CueType kind)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null || !selected.scene.IsValid())
        {
            Debug.LogWarning("[FAA HUD] Select an existing scene object with a known reference position first.");
            return;
        }
        var cue = selected.GetComponent<FaaRotorcraftCueAnchor>() ?? Undo.AddComponent<FaaRotorcraftCueAnchor>(selected);
        Undo.RecordObject(cue, "Configure rotorcraft scene reference");
        cue.kind = kind;
        cue.identifier = selected.name;
        // Deliberately not auto-selected: the inspector/pilot must identify the intended landing/hover point.
        EditorUtility.SetDirty(cue);
        Selection.activeObject = cue;
    }
}
#endif
