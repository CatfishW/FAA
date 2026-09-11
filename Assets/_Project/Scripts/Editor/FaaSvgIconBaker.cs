using System;
using System.IO;
using System.Linq;
using FAA.Customization;
using Unity.VectorGraphics;
using UnityEditor;
using UnityEngine;

public static class FaaSvgIconBaker
{
    private const string Source = "Assets/_Project/UI/Icons/Radar/";
    private const string Destination = "Assets/_Project/Resources/HudIcons/FaaRadarIconLibrary.asset";

    [MenuItem("FAA/HUD/Rebuild SVG Radar Icons")]
    public static void Bake()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/HudIcons"))
            AssetDatabase.CreateFolder("Assets/_Project/Resources", "HudIcons");
        var library = AssetDatabase.LoadAssetAtPath<FaaSvgIconLibrary>(Destination);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<FaaSvgIconLibrary>();
            AssetDatabase.CreateAsset(library, Destination);
        }
        library.entries = Enum.GetValues(typeof(FaaRadarIcon)).Cast<FaaRadarIcon>().Select(BakeIcon).ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FAA HUD] {library.entries.Length} SVG menu and screen-cue icons tessellated.");
    }

    private static FaaSvgIconLibrary.Entry BakeIcon(FaaRadarIcon icon)
    {
        using var reader = new StringReader(File.ReadAllText(Source + icon.ToString().ToLowerInvariant() + ".svg"));
        var scene = SVGParser.ImportSVG(reader);
        var geometry = VectorUtils.TessellateScene(scene.Scene, new VectorUtils.TessellationOptions
        {
            StepDistance = 1f, MaxCordDeviation = .025f, MaxTanAngleDeviation = .05f, SamplingStepSize = .01f
        });
        var mesh = new Mesh();
        try
        {
            VectorUtils.FillMesh(mesh, geometry, 1f, true);
            // FillMesh keeps the SVG origin. Preserve the common 24x24 viewBox
            // rather than fitting each icon to its varying visible bounds.
            return new FaaSvgIconLibrary.Entry
            {
                icon = icon,
                vertices = mesh.vertices.Select(v => new Vector2(v.x - 12f, v.y - 12f)).ToArray(),
                triangles = mesh.triangles
            };
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }
}
