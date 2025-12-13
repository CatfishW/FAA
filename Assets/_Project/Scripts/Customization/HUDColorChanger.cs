using System.Collections;
using System.Collections.Generic;
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
    
    private List<Image> imageComponents = new List<Image>();
    private List<Text> textComponents = new List<Text>();
    private List<TMP_Text> tmpTextComponents = new List<TMP_Text>(); // Add TMP_Text list
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
            imageComponents.Add(img);
        }

        // Find all Text components (excluding exceptions)
        Text[] texts = FindObjectsOfType<Text>();
        foreach (Text txt in texts)
        {
            if (IsUnderExceptionParent(txt.transform))
                continue;
            textComponents.Add(txt);
        }

        // Find all TMP_Text components (excluding exceptions)
        TMP_Text[] tmps = FindObjectsOfType<TMP_Text>();
        foreach (TMP_Text tmp in tmps)
        {
            if (IsUnderExceptionParent(tmp.transform))
                continue;
            tmpTextComponents.Add(tmp);
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
            if (img != null) img.color = targetColor;
        }
        
        // Update all Text components
        foreach (Text txt in textComponents)
        {
            if (txt != null) txt.color = targetColor;
        }

        // Update all TMP_Text components
        foreach (TMP_Text tmp in tmpTextComponents)
        {
            if (tmp != null) tmp.color = targetColor;
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
}
