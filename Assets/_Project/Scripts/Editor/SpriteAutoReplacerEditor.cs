using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Editor tool to automatically replace Image source sprites based on GameObject name matching.
/// Matches UI Image components with sprite assets using keyword/name similarity.
/// </summary>
public class SpriteAutoReplacerEditor : EditorWindow
{
    private enum MatchMode { Exact, Contains, StartsWith, Fuzzy }
    private enum SearchScope { SelectedOnly, SelectedWithChildren, EntireScene }

    private MatchMode matchMode = MatchMode.Contains;
    private SearchScope searchScope = SearchScope.SelectedWithChildren;
    private string[] spriteFolders = new string[] { "Assets" };
    private string spriteFolderPath = "Assets/Resources";
    private bool caseSensitive = false;
    private bool previewMode = true;
    private bool includeInactive = true;
    private float fuzzyThreshold = 0.5f;

    private Vector2 scrollPosition;
    private List<MatchResult> matchResults = new List<MatchResult>();
    private Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private class MatchResult
    {
        public GameObject gameObject;
        public Image imageComponent;
        public Sprite currentSprite;
        public Sprite matchedSprite;
        public string matchedSpriteName;
        public float matchScore;
        public bool shouldReplace = true;
    }

    [MenuItem("Tools/Sprite Auto Replacer")]
    public static void ShowWindow()
    {
        var window = GetWindow<SpriteAutoReplacerEditor>("Sprite Auto Replacer");
        window.minSize = new Vector2(500, 600);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sprite Auto Replacer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Automatically match and replace Image sprites based on GameObject names.", MessageType.Info);

        EditorGUILayout.Space(10);
        DrawSettings();

        EditorGUILayout.Space(10);
        DrawActionButtons();

        EditorGUILayout.Space(10);
        DrawMatchResults();
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

        searchScope = (SearchScope)EditorGUILayout.EnumPopup("Search Scope", searchScope);
        matchMode = (MatchMode)EditorGUILayout.EnumPopup("Match Mode", matchMode);

        if (matchMode == MatchMode.Fuzzy)
        {
            fuzzyThreshold = EditorGUILayout.Slider("Fuzzy Threshold", fuzzyThreshold, 0.3f, 1f);
        }

        caseSensitive = EditorGUILayout.Toggle("Case Sensitive", caseSensitive);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Sprite Search Folder", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        spriteFolderPath = EditorGUILayout.TextField(spriteFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Sprite Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                {
                    spriteFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        previewMode = EditorGUILayout.Toggle("Preview Before Apply", previewMode);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
        if (GUILayout.Button("Find Matches", GUILayout.Height(30)))
        {
            FindMatches();
        }

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        GUI.enabled = matchResults.Any(m => m.shouldReplace && m.matchedSprite != null);
        if (GUILayout.Button("Apply Selected", GUILayout.Height(30)))
        {
            ApplyMatches();
        }
        GUI.enabled = true;

        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
        if (GUILayout.Button("Clear", GUILayout.Height(30)))
        {
            matchResults.Clear();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMatchResults()
    {
        EditorGUILayout.LabelField($"Match Results ({matchResults.Count})", EditorStyles.boldLabel);

        if (matchResults.Count == 0)
        {
            EditorGUILayout.HelpBox("No matches found. Click 'Find Matches' to search.", MessageType.None);
            return;
        }

        // Summary
        int withMatch = matchResults.Count(m => m.matchedSprite != null);
        int selected = matchResults.Count(m => m.shouldReplace && m.matchedSprite != null);
        EditorGUILayout.LabelField($"Found: {withMatch} matches, {selected} selected for replacement", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        // Select/Deselect All
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            foreach (var m in matchResults) m.shouldReplace = true;
        }
        if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            foreach (var m in matchResults) m.shouldReplace = false;
        }
        if (GUILayout.Button("Select Matches Only", EditorStyles.miniButton, GUILayout.Width(120)))
        {
            foreach (var m in matchResults) m.shouldReplace = m.matchedSprite != null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(350));

        foreach (var match in matchResults)
        {
            DrawMatchRow(match);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMatchRow(MatchResult match)
    {
        bool hasMatch = match.matchedSprite != null;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        // Checkbox
        GUI.enabled = hasMatch;
        match.shouldReplace = EditorGUILayout.Toggle(match.shouldReplace, GUILayout.Width(20));
        GUI.enabled = true;

        // GameObject reference
        EditorGUILayout.ObjectField(match.gameObject, typeof(GameObject), true, GUILayout.Width(150));

        // Arrow
        EditorGUILayout.LabelField("→", GUILayout.Width(20));

        // Matched sprite (editable)
        var newSprite = (Sprite)EditorGUILayout.ObjectField(match.matchedSprite, typeof(Sprite), false, GUILayout.Width(150));
        if (newSprite != match.matchedSprite)
        {
            match.matchedSprite = newSprite;
            match.matchedSpriteName = newSprite != null ? newSprite.name : "(none)";
            match.matchScore = 1f;
        }

        // Match info
        if (hasMatch)
        {
            GUI.color = Color.green;
            EditorGUILayout.LabelField($"({match.matchScore:P0})", GUILayout.Width(50));
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField("No match", GUILayout.Width(50));
            GUI.color = Color.white;
        }

        // Ping button
        if (GUILayout.Button("◎", GUILayout.Width(25)))
        {
            EditorGUIUtility.PingObject(match.gameObject);
            Selection.activeGameObject = match.gameObject;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void FindMatches()
    {
        matchResults.Clear();
        LoadSpriteCache();

        var images = GetTargetImages();

        foreach (var image in images)
        {
            var result = new MatchResult
            {
                gameObject = image.gameObject,
                imageComponent = image,
                currentSprite = image.sprite
            };

            // Find best matching sprite
            var (sprite, score) = FindBestMatch(image.gameObject.name);
            result.matchedSprite = sprite;
            result.matchedSpriteName = sprite != null ? sprite.name : "(none)";
            result.matchScore = score;
            result.shouldReplace = sprite != null;

            matchResults.Add(result);
        }

        // Sort by match score descending
        matchResults = matchResults.OrderByDescending(m => m.matchScore).ToList();

        Debug.Log($"Found {matchResults.Count} Image components, {matchResults.Count(m => m.matchedSprite != null)} with matches");
    }

    private void LoadSpriteCache()
    {
        spriteCache.Clear();

        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { spriteFolderPath });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();

            foreach (var sprite in sprites)
            {
                string key = caseSensitive ? sprite.name : sprite.name.ToLowerInvariant();
                if (!spriteCache.ContainsKey(key))
                {
                    spriteCache[key] = sprite;
                }
            }
        }

        Debug.Log($"Loaded {spriteCache.Count} sprites from {spriteFolderPath}");
    }

    private List<Image> GetTargetImages()
    {
        var images = new List<Image>();

        switch (searchScope)
        {
            case SearchScope.SelectedOnly:
                foreach (var obj in Selection.gameObjects)
                {
                    var img = obj.GetComponent<Image>();
                    if (img != null) images.Add(img);
                }
                break;

            case SearchScope.SelectedWithChildren:
                foreach (var obj in Selection.gameObjects)
                {
                    images.AddRange(obj.GetComponentsInChildren<Image>(includeInactive));
                }
                break;

            case SearchScope.EntireScene:
                images.AddRange(FindObjectsOfType<Image>(includeInactive));
                break;
        }

        return images.Distinct().ToList();
    }

    private (Sprite sprite, float score) FindBestMatch(string gameObjectName)
    {
        string searchName = caseSensitive ? gameObjectName : gameObjectName.ToLowerInvariant();
        searchName = NormalizeName(searchName);

        Sprite bestSprite = null;
        float bestScore = 0f;

        foreach (var kvp in spriteCache)
        {
            string spriteName = NormalizeName(kvp.Key);
            float score = CalculateMatchScore(searchName, spriteName);

            if (score > bestScore)
            {
                bestScore = score;
                bestSprite = kvp.Value;
            }
        }

        // Apply threshold for fuzzy matching
        if (matchMode == MatchMode.Fuzzy && bestScore < fuzzyThreshold)
        {
            return (null, 0f);
        }

        return (bestSprite, bestScore);
    }

    private string NormalizeName(string name)
    {
        // Remove common suffixes/prefixes
        name = Regex.Replace(name, @"[\s_\-\.]+", "");
        name = Regex.Replace(name, @"(Design\d*|White|Black|Alt|New|Old|Copy|\(\d+\))", "", RegexOptions.IgnoreCase);
        return name.Trim();
    }

    private float CalculateMatchScore(string gameObjectName, string spriteName)
    {
        switch (matchMode)
        {
            case MatchMode.Exact:
                return gameObjectName.Equals(spriteName, 
                    caseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f;

            case MatchMode.Contains:
                if (spriteName.Contains(gameObjectName) || gameObjectName.Contains(spriteName))
                {
                    int longer = Mathf.Max(gameObjectName.Length, spriteName.Length);
                    int shorter = Mathf.Min(gameObjectName.Length, spriteName.Length);
                    return (float)shorter / longer;
                }
                return 0f;

            case MatchMode.StartsWith:
                if (spriteName.StartsWith(gameObjectName) || gameObjectName.StartsWith(spriteName))
                {
                    int longer = Mathf.Max(gameObjectName.Length, spriteName.Length);
                    int shorter = Mathf.Min(gameObjectName.Length, spriteName.Length);
                    return (float)shorter / longer;
                }
                return 0f;

            case MatchMode.Fuzzy:
                return CalculateFuzzyScore(gameObjectName, spriteName);

            default:
                return 0f;
        }
    }

    private float CalculateFuzzyScore(string s1, string s2)
    {
        // Levenshtein distance-based similarity
        int maxLen = Mathf.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1f;

        int distance = LevenshteinDistance(s1, s2);
        float similarity = 1f - (float)distance / maxLen;

        // Bonus for substring matches
        if (s1.Contains(s2) || s2.Contains(s1))
        {
            similarity = Mathf.Max(similarity, 0.7f);
        }

        // Bonus for same starting characters
        int commonPrefix = 0;
        for (int i = 0; i < Mathf.Min(s1.Length, s2.Length); i++)
        {
            if (char.ToLower(s1[i]) == char.ToLower(s2[i]))
                commonPrefix++;
            else
                break;
        }
        if (commonPrefix >= 3)
        {
            similarity = Mathf.Max(similarity, 0.5f + commonPrefix * 0.05f);
        }

        return similarity;
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = char.ToLower(s1[i - 1]) == char.ToLower(s2[j - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(
                    d[i - 1, j] + 1,      // deletion
                    Mathf.Min(
                        d[i, j - 1] + 1,  // insertion
                        d[i - 1, j - 1] + cost // substitution
                    )
                );
            }
        }

        return d[s1.Length, s2.Length];
    }

    private void ApplyMatches()
    {
        int applied = 0;
        Undo.SetCurrentGroupName("Sprite Auto Replace");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var match in matchResults)
        {
            if (match.shouldReplace && match.matchedSprite != null && match.imageComponent != null)
            {
                Undo.RecordObject(match.imageComponent, "Replace Sprite");
                match.imageComponent.sprite = match.matchedSprite;
                EditorUtility.SetDirty(match.imageComponent);
                applied++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"Applied {applied} sprite replacements");
        EditorUtility.DisplayDialog("Sprite Auto Replacer", $"Replaced {applied} sprites.\n\nUse Ctrl+Z to undo if needed.", "OK");
    }
}
