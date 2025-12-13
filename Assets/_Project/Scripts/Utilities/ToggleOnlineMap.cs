using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleOnlineMap : MonoBehaviour
{
    [Header("Raw Image Settings")]
    public RawImage targetRawImage;
    
    [Header("Animation Settings")]
    public float animationDuration = 0.5f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    private bool isTransparent = false;
    private bool isAnimating = false;
    
    void Start()
    {
        // If no raw image is assigned, try to find one in the scene
        if (targetRawImage == null)
        {
            targetRawImage = FindObjectOfType<RawImage>();
        }
        
        // Initialize the raw image to full opacity
        if (targetRawImage != null)
        {
            SetImageAlpha(1f);
        }
    }
    
    public void ToggleTransparency()
    {
        if (targetRawImage == null || isAnimating)
            return;
            
        float targetAlpha = isTransparent ? 1f : 0f;
        StartCoroutine(AnimateTransparency(targetAlpha));
        isTransparent = !isTransparent;
    }
    
    private IEnumerator AnimateTransparency(float targetAlpha)
    {
        isAnimating = true;
        
        float startAlpha = targetRawImage.color.a;
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / animationDuration;
            float curveValue = animationCurve.Evaluate(progress);
            
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, curveValue);
            SetImageAlpha(currentAlpha);
            
            yield return null;
        }
        
        // Ensure we end exactly at the target alpha
        SetImageAlpha(targetAlpha);
        isAnimating = false;
    }
    
    private void SetImageAlpha(float alpha)
    {
        if (targetRawImage != null)
        {
            Color color = targetRawImage.color;
            color.a = alpha;
            targetRawImage.color = color;
        }
    }
}
