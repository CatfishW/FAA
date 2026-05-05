using System.Collections;
using System.Collections.Generic;
using FAA.Customization;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add TMP namespace
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HUDColorChanger : MonoBehaviour
{
    [Header("Theme Settings")]
    [SerializeField] private Button themeToggleButton;
    [SerializeField] private Image buttonIcon;
    [SerializeField] private Sprite moonIcon;
    [SerializeField] private Sprite sunIcon;
    
    [Header("Theme Colors")]
    [SerializeField] private Color lightThemeColor = Color.black;
    [SerializeField] private Color darkThemeColor = Color.white;
    [SerializeField] private Color activeButtonColor = Color.yellow;
    [SerializeField] private Color inactiveButtonColor = Color.gray;
    
    // Add exception parent list
    [Header("Exceptions")]
    [SerializeField] private List<Transform> exceptionParents = new List<Transform>();
    [SerializeField] private bool tintOnlySymbologyElements = true;
    [SerializeField] private bool preserveElementAlpha = true;
    
    private List<Image> imageComponents = new List<Image>();
    private List<Text> textComponents = new List<Text>();
    private List<TMP_Text> tmpTextComponents = new List<TMP_Text>(); // Add TMP_Text list
    private readonly Dictionary<Image, Color> imageBaseColors = new Dictionary<Image, Color>();
    private readonly Dictionary<Text, Color> textBaseColors = new Dictionary<Text, Color>();
    private readonly Dictionary<TMP_Text, Color> tmpTextBaseColors = new Dictionary<TMP_Text, Color>();
    private bool isDarkTheme = false;
    private Color originalButtonColor;

    // Start is called before the first frame update
    void Start()
    {
        Initialize();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // Small delay to ensure all components are ready
            EditorApplication.delayCall += Initialize;
        }
    }
#endif

    private void Initialize()
    {
        FindAllUIComponents();
        if (themeToggleButton != null)
        {
            originalButtonColor = themeToggleButton.image.color;
            if (Application.isPlaying)
            {
                themeToggleButton.onClick.RemoveListener(ToggleTheme);
                themeToggleButton.onClick.AddListener(ToggleTheme);
            }
        }
        UpdateTheme();
    }

    private void FindAllUIComponents()
    {
        imageComponents.Clear();
        textComponents.Clear();
        tmpTextComponents.Clear(); // Clear TMP_Text list

        // Find all Image components (excluding the toggle button and exceptions)
        Image[] images = FindObjectsOfType<Image>();
        foreach (Image img in images)
        {
            if (img == themeToggleButton?.image || img == buttonIcon)
                continue;
            if (IsUnderExceptionParent(img.transform))
                continue;
            if (tintOnlySymbologyElements && !SymbologyTintUtility.ShouldTintImage(img))
                continue;
            imageComponents.Add(img);
            RememberBaseColor(img);
        }

        // Find all Text components (excluding exceptions)
        Text[] texts = FindObjectsOfType<Text>();
        foreach (Text txt in texts)
        {
            if (IsUnderExceptionParent(txt.transform))
                continue;
            if (tintOnlySymbologyElements && !SymbologyTintUtility.ShouldTintText(txt.transform))
                continue;
            textComponents.Add(txt);
            RememberBaseColor(txt);
        }

        // Find all TMP_Text components (excluding exceptions)
        TMP_Text[] tmps = FindObjectsOfType<TMP_Text>();
        foreach (TMP_Text tmp in tmps)
        {
            if (IsUnderExceptionParent(tmp.transform))
                continue;
            if (tintOnlySymbologyElements && !SymbologyTintUtility.ShouldTintText(tmp.transform))
                continue;
            tmpTextComponents.Add(tmp);
            RememberBaseColor(tmp);
        }
    }

    // Helper to check if a transform is under any exception parent
    private bool IsUnderExceptionParent(Transform t)
    {
        foreach (var parent in exceptionParents)
        {
            if (parent == null) continue;
            if (t == parent || t.IsChildOf(parent))
                return true;
        }
        return false;
    }

    public void ToggleTheme()
    {
        isDarkTheme = !isDarkTheme;
        UpdateTheme();
        
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private void UpdateTheme()
    {
        Color targetColor = isDarkTheme ? darkThemeColor : lightThemeColor;
        
        // Update all images
        foreach (Image img in imageComponents)
        {
            if (img != null) img.color = SymbologyTintUtility.BuildTintColor(targetColor, GetBaseColor(img), preserveElementAlpha);
        }
        
        // Update all Text components
        foreach (Text txt in textComponents)
        {
            if (txt != null) txt.color = SymbologyTintUtility.BuildTintColor(targetColor, GetBaseColor(txt), preserveElementAlpha);
        }

        // Update all TMP_Text components
        foreach (TMP_Text tmp in tmpTextComponents)
        {
            if (tmp != null) tmp.color = SymbologyTintUtility.BuildTintColor(targetColor, GetBaseColor(tmp), preserveElementAlpha);
        }
        
        // Update button appearance
        if (themeToggleButton != null)
        {
            themeToggleButton.image.color = isDarkTheme ? activeButtonColor : originalButtonColor;
            if (buttonIcon != null)
                buttonIcon.sprite = isDarkTheme ? sunIcon : moonIcon;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Mark scene as dirty to save changes
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void RememberBaseColor(Image image)
    {
        if (!imageBaseColors.ContainsKey(image))
        {
            imageBaseColors[image] = image.color;
        }
    }

    private void RememberBaseColor(Text text)
    {
        if (!textBaseColors.ContainsKey(text))
        {
            textBaseColors[text] = text.color;
        }
    }

    private void RememberBaseColor(TMP_Text text)
    {
        if (!tmpTextBaseColors.ContainsKey(text))
        {
            tmpTextBaseColors[text] = text.color;
        }
    }

    private Color GetBaseColor(Image image)
    {
        return imageBaseColors.TryGetValue(image, out Color color) ? color : image.color;
    }

    private Color GetBaseColor(Text text)
    {
        return textBaseColors.TryGetValue(text, out Color color) ? color : text.color;
    }

    private Color GetBaseColor(TMP_Text text)
    {
        return tmpTextBaseColors.TryGetValue(text, out Color color) ? color : text.color;
    }
}
