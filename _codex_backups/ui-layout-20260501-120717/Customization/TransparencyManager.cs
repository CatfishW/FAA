using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class TransparencyGroup
{
    public GameObject parent;
    public Slider transparencySlider;
}

public class TransparencyManager : MonoBehaviour
{
    [SerializeField] private List<TransparencyGroup> transparencyGroups = new List<TransparencyGroup>();

    void Start()
    {
        foreach (var group in transparencyGroups)
        {
            if (group.transparencySlider != null)
            {
                group.transparencySlider.onValueChanged.AddListener(value => UpdateTransparency(group.parent, value));
                UpdateTransparency(group.parent, group.transparencySlider.value);
            }
        }
    }

    private void UpdateTransparency(GameObject parent, float alpha)
    {
        if (parent == null) return;

        // Handle Image components
        Image[] images = parent.GetComponentsInChildren<Image>();
        foreach (Image image in images)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        // Handle RawImage components
        RawImage[] rawImages = parent.GetComponentsInChildren<RawImage>();
        foreach (RawImage rawImage in rawImages)
        {
            Color color = rawImage.color;
            color.a = alpha;
            rawImage.color = color;
        }

        // Handle Text components
        Text[] texts = parent.GetComponentsInChildren<Text>();
        foreach (Text text in texts)
        {
            Color color = text.color;
            color.a = alpha;
            text.color = color;
        }

        // Handle TextMeshPro components
        TMP_Text[] tmpTexts = parent.GetComponentsInChildren<TMP_Text>();
        foreach (TMP_Text tmpText in tmpTexts)
        {
            Color color = tmpText.color;
            color.a = alpha;
            tmpText.color = color;
        }
    }
}
