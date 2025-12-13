using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ScaleGroup
{
    public GameObject parent;
    public Slider scaleSlider;
    public Vector3 baseScale = Vector3.one;
}

public class ScaleManager : MonoBehaviour
{
    [SerializeField] private List<ScaleGroup> scaleGroups = new List<ScaleGroup>();

    void Start()
    {
        foreach (var group in scaleGroups)
        {
            if (group.scaleSlider != null)
            {
                // Store the initial scale as base scale if not set
                if (group.baseScale == Vector3.one && group.parent != null)
                {
                    group.baseScale = group.parent.transform.localScale;
                }
                
                group.scaleSlider.onValueChanged.AddListener(value => UpdateScale(group.parent, group.baseScale, value));
                UpdateScale(group.parent, group.baseScale, group.scaleSlider.value);
            }
        }
    }

    private void UpdateScale(GameObject parent, Vector3 baseScale, float scaleMultiplier)
    {
        if (parent == null) return;

        parent.transform.localScale = baseScale * scaleMultiplier;
    }
}
