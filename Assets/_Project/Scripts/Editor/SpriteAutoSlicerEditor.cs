using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.U2D.Sprites;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor tool to automatically slice selected sprite textures.
/// Select textures in the Project window and use Tools > Auto Slice Sprites.
/// </summary>
public class SpriteAutoSlicerEditor : EditorWindow
{
    private enum SliceType { Automatic, GridByCellSize, GridByCellCount }
    private enum SlicePivot { Center, TopLeft, Top, TopRight, Left, Right, BottomLeft, Bottom, BottomRight, Custom }
    private enum SliceMethod { DeleteExisting, Smart, Safe }
    private enum PivotUnitMode { Normalized, Pixels }

    private SliceType sliceType = SliceType.Automatic;
    private SlicePivot pivot = SlicePivot.Center;
    private PivotUnitMode pivotUnitMode = PivotUnitMode.Normalized;
    private SliceMethod method = SliceMethod.DeleteExisting;
    private Vector2 customPivot = new Vector2(0.5f, 0.5f);
    private Vector2Int gridCellSize = new Vector2Int(64, 64);
    private Vector2Int gridCellCount = new Vector2Int(4, 4);
    private Vector2Int gridOffset = Vector2Int.zero;
    private Vector2Int gridPadding = Vector2Int.zero;
    private int minimumSpriteSize = 4;  // Unity default
    private int extrudeEdges = 1;       // Match Unity inspector default

    [MenuItem("Tools/Auto Slice Sprites %#s")]
    public static void ShowWindow()
    {
        var window = GetWindow<SpriteAutoSlicerEditor>("Sprite Auto Slicer");
        window.minSize = new Vector2(350, 400);
    }

    [MenuItem("Tools/Auto Slice Sprites (Quick) %&s")]
    public static void QuickSliceSelected()
    {
        var textures = GetSelectedTextures();
        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog("Auto Slice", "Please select one or more textures in the Project window.", "OK");
            return;
        }

        int sliced = 0;
        foreach (var texture in textures)
        {
            if (AutoSliceTexture(texture, SlicePivot.Center, SliceMethod.DeleteExisting, 4, 0))
                sliced++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Auto Slice Complete", $"Successfully sliced {sliced} of {textures.Count} textures.", "OK");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sprite Auto Slicer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select textures in the Project window, configure settings, then click 'Slice Selected'.", MessageType.Info);

        EditorGUILayout.Space(10);
        DrawSliceSettings();

        EditorGUILayout.Space(10);
        DrawActionButtons();

        EditorGUILayout.Space(10);
        DrawSelectionInfo();
    }

    private void DrawSliceSettings()
    {
        EditorGUILayout.LabelField("Slice Settings", EditorStyles.boldLabel);

        sliceType = (SliceType)EditorGUILayout.EnumPopup("Type", sliceType);

        if (sliceType == SliceType.GridByCellSize)
        {
            gridCellSize = EditorGUILayout.Vector2IntField("Cell Size", gridCellSize);
            gridOffset = EditorGUILayout.Vector2IntField("Offset", gridOffset);
            gridPadding = EditorGUILayout.Vector2IntField("Padding", gridPadding);
        }
        else if (sliceType == SliceType.GridByCellCount)
        {
            gridCellCount = EditorGUILayout.Vector2IntField("Cell Count (Columns x Rows)", gridCellCount);
            gridOffset = EditorGUILayout.Vector2IntField("Offset", gridOffset);
            gridPadding = EditorGUILayout.Vector2IntField("Padding", gridPadding);
        }
        else // Automatic
        {
            minimumSpriteSize = EditorGUILayout.IntField("Minimum Size", minimumSpriteSize);
            extrudeEdges = EditorGUILayout.IntSlider("Extrude Edges", extrudeEdges, 0, 32);
        }

        EditorGUILayout.Space(5);
        pivot = (SlicePivot)EditorGUILayout.EnumPopup("Pivot", pivot);
        pivotUnitMode = (PivotUnitMode)EditorGUILayout.EnumPopup("Pivot Unit Mode", pivotUnitMode);

        if (pivot == SlicePivot.Custom)
        {
            if (pivotUnitMode == PivotUnitMode.Normalized)
            {
                customPivot = EditorGUILayout.Vector2Field("Custom Pivot", customPivot);
            }
            else
            {
                // Pixels mode - show as integer values
                var pixelPivot = EditorGUILayout.Vector2Field("Custom Pivot (Pixels)", customPivot);
                customPivot = pixelPivot;
            }
        }

        method = (SliceMethod)EditorGUILayout.EnumPopup("Method", method);

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(GetMethodDescription(method), MessageType.None);
    }

    private string GetMethodDescription(SliceMethod m)
    {
        return m switch
        {
            SliceMethod.DeleteExisting => "Delete Existing removes all existing sprites and recreates them from scratch.",
            SliceMethod.Smart => "Smart attempts to create new rectangles while retaining/adjusting existing ones.",
            SliceMethod.Safe => "Safe adds new rectangles without modifying any existing ones.",
            _ => ""
        };
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Slice Selected", GUILayout.Height(35)))
        {
            SliceSelectedTextures();
        }

        GUI.backgroundColor = new Color(0.8f, 0.6f, 0.3f);
        if (GUILayout.Button("Slice All in Folder", GUILayout.Height(35)))
        {
            SliceAllInSelectedFolder();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSelectionInfo()
    {
        var textures = GetSelectedTextures();
        EditorGUILayout.LabelField($"Selected Textures: {textures.Count}", EditorStyles.helpBox);

        if (textures.Count > 0 && textures.Count <= 10)
        {
            EditorGUI.indentLevel++;
            foreach (var tex in textures)
            {
                EditorGUILayout.LabelField($"• {tex.name}");
            }
            EditorGUI.indentLevel--;
        }
        else if (textures.Count > 10)
        {
            EditorGUILayout.LabelField($"  (First 10 shown)");
            EditorGUI.indentLevel++;
            foreach (var tex in textures.Take(10))
            {
                EditorGUILayout.LabelField($"• {tex.name}");
            }
            EditorGUI.indentLevel--;
        }
    }

    private void SliceSelectedTextures()
    {
        var textures = GetSelectedTextures();
        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog("Auto Slice", "Please select one or more textures in the Project window.", "OK");
            return;
        }

        int sliced = 0;
        int total = textures.Count;

        try
        {
            for (int i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                EditorUtility.DisplayProgressBar("Auto Slicing Sprites", $"Processing {texture.name}...", (float)i / total);

                bool success = sliceType switch
                {
                    SliceType.Automatic => AutoSliceTexture(texture, pivot, method, minimumSpriteSize, extrudeEdges),
                    SliceType.GridByCellSize => GridSliceTexture(texture, gridCellSize, gridOffset, gridPadding, pivot, method),
                    SliceType.GridByCellCount => GridSliceByCount(texture, gridCellCount, gridOffset, gridPadding, pivot, method),
                    _ => false
                };

                if (success) sliced++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Auto Slice Complete", $"Successfully sliced {sliced} of {total} textures.", "OK");
    }

    private void SliceAllInSelectedFolder()
    {
        string folderPath = GetSelectedFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog("Auto Slice", "Please select a folder in the Project window.", "OK");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        var textures = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(t => t != null)
            .ToList();

        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog("Auto Slice", $"No textures found in {folderPath}", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Confirm", $"Slice {textures.Count} textures in {folderPath}?", "Yes", "Cancel"))
            return;

        int sliced = 0;
        try
        {
            for (int i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                EditorUtility.DisplayProgressBar("Auto Slicing Sprites", $"Processing {texture.name}...", (float)i / textures.Count);

                bool success = sliceType switch
                {
                    SliceType.Automatic => AutoSliceTexture(texture, pivot, method, minimumSpriteSize, extrudeEdges),
                    SliceType.GridByCellSize => GridSliceTexture(texture, gridCellSize, gridOffset, gridPadding, pivot, method),
                    SliceType.GridByCellCount => GridSliceByCount(texture, gridCellCount, gridOffset, gridPadding, pivot, method),
                    _ => false
                };

                if (success) sliced++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Auto Slice Complete", $"Successfully sliced {sliced} of {textures.Count} textures.", "OK");
    }

    private static List<Texture2D> GetSelectedTextures()
    {
        return Selection.objects
            .OfType<Texture2D>()
            .ToList();
    }

    private static string GetSelectedFolderPath()
    {
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path))
                return path;
        }
        return null;
    }

    private static Vector2 GetPivotValue(SlicePivot pivot, Vector2 customPivot)
    {
        return pivot switch
        {
            SlicePivot.Center => new Vector2(0.5f, 0.5f),
            SlicePivot.TopLeft => new Vector2(0f, 1f),
            SlicePivot.Top => new Vector2(0.5f, 1f),
            SlicePivot.TopRight => new Vector2(1f, 1f),
            SlicePivot.Left => new Vector2(0f, 0.5f),
            SlicePivot.Right => new Vector2(1f, 0.5f),
            SlicePivot.BottomLeft => new Vector2(0f, 0f),
            SlicePivot.Bottom => new Vector2(0.5f, 0f),
            SlicePivot.BottomRight => new Vector2(1f, 0f),
            SlicePivot.Custom => customPivot,
            _ => new Vector2(0.5f, 0.5f)
        };
    }

    private static bool AutoSliceTexture(Texture2D texture, SlicePivot pivot, SliceMethod method, int minSize, int extrude)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            Debug.LogWarning($"Could not get TextureImporter for {texture.name}");
            return false;
        }

        // Ensure it's set to sprite mode
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable = true;

        // Apply initial settings to make texture readable
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        // Reload texture after reimport
        texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        // Use custom alpha detection algorithm (matches Unity's Sprite Editor behavior)
        var rects = DetectSpriteRects(texture, minSize, 0.01f);

        if (rects == null || rects.Count == 0)
        {
            Debug.LogWarning($"No sprites detected in {texture.name}");
            return false;
        }

        // Apply extrude by expanding rects
        if (extrude > 0)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                rects[i] = new Rect(
                    Mathf.Max(0, r.x - extrude),
                    Mathf.Max(0, r.y - extrude),
                    Mathf.Min(texture.width - r.x + extrude, r.width + extrude * 2),
                    Mathf.Min(texture.height - r.y + extrude, r.height + extrude * 2)
                );
            }
        }

        // Use ISpriteEditorDataProvider for proper sprite sheet modification
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        
        if (dataProvider == null)
        {
            Debug.LogWarning($"Could not get SpriteEditorDataProvider for {texture.name}");
            return false;
        }
        
        dataProvider.InitSpriteEditorDataProvider();

        // Get existing sprite rects if using Smart or Safe method
        var existingRects = new List<SpriteRect>();
        if (method != SliceMethod.DeleteExisting)
        {
            var currentRects = dataProvider.GetSpriteRects();
            if (currentRects != null)
            {
                existingRects.AddRange(currentRects);
            }
        }

        // Create new sprite rects
        var newSpriteRects = new List<SpriteRect>();
        var pivotValue = GetPivotValue(pivot, Vector2.one * 0.5f);
        var alignment = pivot == SlicePivot.Custom ? SpriteAlignment.Custom : (SpriteAlignment)GetAlignment(pivot);

        for (int i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];

            // For Smart method, check if there's an existing sprite that overlaps
            if (method == SliceMethod.Smart)
            {
                var existing = existingRects.FirstOrDefault(e => e.rect.Overlaps(rect));
                if (existing != null)
                {
                    // Adjust existing rect
                    existing.rect = rect;
                    newSpriteRects.Add(existing);
                    existingRects.Remove(existing);
                    continue;
                }
            }

            var spriteRect = new SpriteRect
            {
                name = $"{texture.name}_{i}",
                rect = rect,
                pivot = pivotValue,
                alignment = alignment,
                spriteID = GUID.Generate()
            };
            newSpriteRects.Add(spriteRect);
        }

        // For Safe method, keep all existing sprites too
        if (method == SliceMethod.Safe)
        {
            newSpriteRects.AddRange(existingRects);
        }

        // Apply the sprite rects
        dataProvider.SetSpriteRects(newSpriteRects.ToArray());
        dataProvider.Apply();

        // Reimport to apply changes
        var assetImporterEditor = dataProvider.targetObject as AssetImporter;
        if (assetImporterEditor != null)
        {
            EditorUtility.SetDirty(assetImporterEditor);
            assetImporterEditor.SaveAndReimport();
        }
        else
        {
            importer.SaveAndReimport();
        }

        Debug.Log($"Sliced {texture.name} into {newSpriteRects.Count} sprites");
        return true;
    }

    /// <summary>
    /// Custom alpha-based sprite detection algorithm that matches Unity's Sprite Editor behavior.
    /// Uses flood fill to find contiguous opaque regions.
    /// </summary>
    private static List<Rect> DetectSpriteRects(Texture2D texture, int minSize, float alphaThreshold = 0.01f)
    {
        int width = texture.width;
        int height = texture.height;
        Color32[] pixels = texture.GetPixels32();
        bool[,] visited = new bool[width, height];
        var rects = new List<Rect>();

        // Scan the entire texture for opaque regions
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (visited[x, y])
                    continue;

                int pixelIndex = y * width + x;
                float alpha = pixels[pixelIndex].a / 255f;

                if (alpha > alphaThreshold)
                {
                    // Found an unvisited opaque pixel - flood fill to find the bounding rect
                    var bounds = FloodFillBounds(pixels, visited, width, height, x, y, alphaThreshold);
                    
                    // Only add if it meets minimum size
                    if (bounds.width >= minSize && bounds.height >= minSize)
                    {
                        rects.Add(bounds);
                    }
                }
                else
                {
                    visited[x, y] = true;
                }
            }
        }

        // Sort rects by position (top-left to bottom-right) for consistent naming
        rects = rects.OrderByDescending(r => r.y).ThenBy(r => r.x).ToList();

        return rects;
    }

    /// <summary>
    /// Flood fill algorithm to find the bounding rectangle of a contiguous opaque region.
    /// Uses an iterative approach with a queue to avoid stack overflow on large regions.
    /// </summary>
    private static Rect FloodFillBounds(Color32[] pixels, bool[,] visited, int width, int height, int startX, int startY, float alphaThreshold)
    {
        int minX = startX, maxX = startX;
        int minY = startY, maxY = startY;

        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // Update bounds
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);

            // Check 4-connected neighbors (up, down, left, right)
            CheckAndEnqueue(pixels, visited, queue, width, height, x - 1, y, alphaThreshold);
            CheckAndEnqueue(pixels, visited, queue, width, height, x + 1, y, alphaThreshold);
            CheckAndEnqueue(pixels, visited, queue, width, height, x, y - 1, alphaThreshold);
            CheckAndEnqueue(pixels, visited, queue, width, height, x, y + 1, alphaThreshold);
        }

        // Return the bounding rect (convert to Unity's bottom-left origin)
        return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static void CheckAndEnqueue(Color32[] pixels, bool[,] visited, Queue<(int x, int y)> queue, 
        int width, int height, int x, int y, float alphaThreshold)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        if (visited[x, y])
            return;

        int pixelIndex = y * width + x;
        float alpha = pixels[pixelIndex].a / 255f;

        visited[x, y] = true;

        if (alpha > alphaThreshold)
        {
            queue.Enqueue((x, y));
        }
    }

    private static bool GridSliceTexture(Texture2D texture, Vector2Int cellSize, Vector2Int offset, Vector2Int padding, SlicePivot pivot, SliceMethod method)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null) return false;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;

        int cols = Mathf.Max(1, (texture.width - offset.x) / (cellSize.x + padding.x));
        int rows = Mathf.Max(1, (texture.height - offset.y) / (cellSize.y + padding.y));

        var spritesheet = new List<SpriteMetaData>();
        var pivotValue = GetPivotValue(pivot, Vector2.one * 0.5f);

        int index = 0;
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int col = 0; col < cols; col++)
            {
                float x = offset.x + col * (cellSize.x + padding.x);
                float y = offset.y + row * (cellSize.y + padding.y);

                if (x + cellSize.x > texture.width || y + cellSize.y > texture.height)
                    continue;

                var spriteData = new SpriteMetaData
                {
                    name = $"{texture.name}_{index++}",
                    rect = new Rect(x, y, cellSize.x, cellSize.y),
                    pivot = pivotValue,
                    alignment = pivot == SlicePivot.Custom ? (int)SpriteAlignment.Custom : GetAlignment(pivot)
                };
                spritesheet.Add(spriteData);
            }
        }

        importer.spritesheet = spritesheet.ToArray();
        importer.SaveAndReimport();

        Debug.Log($"Grid sliced {texture.name} into {spritesheet.Count} sprites ({cols}x{rows})");
        return true;
    }

    private static bool GridSliceByCount(Texture2D texture, Vector2Int cellCount, Vector2Int offset, Vector2Int padding, SlicePivot pivot, SliceMethod method)
    {
        int availableWidth = texture.width - offset.x - (padding.x * (cellCount.x - 1));
        int availableHeight = texture.height - offset.y - (padding.y * (cellCount.y - 1));

        var cellSize = new Vector2Int(
            Mathf.Max(1, availableWidth / cellCount.x),
            Mathf.Max(1, availableHeight / cellCount.y)
        );

        return GridSliceTexture(texture, cellSize, offset, padding, pivot, method);
    }

    private static int GetAlignment(SlicePivot pivot)
    {
        return pivot switch
        {
            SlicePivot.Center => (int)SpriteAlignment.Center,
            SlicePivot.TopLeft => (int)SpriteAlignment.TopLeft,
            SlicePivot.Top => (int)SpriteAlignment.TopCenter,
            SlicePivot.TopRight => (int)SpriteAlignment.TopRight,
            SlicePivot.Left => (int)SpriteAlignment.LeftCenter,
            SlicePivot.Right => (int)SpriteAlignment.RightCenter,
            SlicePivot.BottomLeft => (int)SpriteAlignment.BottomLeft,
            SlicePivot.Bottom => (int)SpriteAlignment.BottomCenter,
            SlicePivot.BottomRight => (int)SpriteAlignment.BottomRight,
            _ => (int)SpriteAlignment.Center
        };
    }
}
