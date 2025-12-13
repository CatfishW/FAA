using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeadingHUD : MonoBehaviour
{
    [Header("Heading Display")]
    public float currentHeading;
    public Text headingText;
    
    [Header("Heading Sources")]
    public bool useTransformRotation = true;
    public bool allowManualUpdate = true;
    
    [Header("Events")]
    public UnityEngine.Events.UnityEvent<float> OnHeadingChanged;
    
    private float lastHeading = -1f;
    
    // Start is called before the first frame update
    void Start()
    {
        // Initialize heading display
        if (headingText != null && useTransformRotation)
        {
            UpdateHeading(transform.localRotation.eulerAngles.z);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (useTransformRotation)
        {
            float newHeading = transform.localRotation.eulerAngles.z;
            if (Mathf.Abs(newHeading - currentHeading) > 0.1f)
            {
                SetCurrentHeading(newHeading);
            }
        }
    }

    /// <summary>
    /// Set the current heading value and update display
    /// </summary>
    /// <param name="newHeading">New heading value in degrees (0-360)</param>
    public void SetCurrentHeading(float newHeading)
    {
        // Normalize heading to 0-360 range
        newHeading = Mathf.Repeat(newHeading, 360f);
        
        if (Mathf.Abs(newHeading - currentHeading) > 0.1f)
        {
            currentHeading = newHeading;
            
            // Update text display
            if (headingText != null)
            {
                headingText.text = newHeading.ToString("F0") + "°";
            }
            
            // Invoke events
            if (OnHeadingChanged != null)
            {
                OnHeadingChanged.Invoke(currentHeading);
            }
            
            lastHeading = currentHeading;
        }
    }

    /// <summary>
    /// Update heading and optionally rotate transform
    /// </summary>
    /// <param name="newHeading">New heading in degrees</param>
    /// <param name="updateTransform">Whether to update the transform rotation</param>
    public void UpdateHeading(float newHeading, bool updateTransform = true)
    {
        if (allowManualUpdate)
        {
            SetCurrentHeading(newHeading);
            
            if (updateTransform)
            {
                transform.localRotation = Quaternion.Euler(0, 0, newHeading);
            }
        }
    }

    /// <summary>
    /// Update heading (legacy method for compatibility)
    /// </summary>
    /// <param name="newHeading">New heading in degrees</param>
    public void UpdateHeading(float newHeading)
    {
        UpdateHeading(newHeading, true);
    }
    
    /// <summary>
    /// Get the current heading value
    /// </summary>
    /// <returns>Current heading in degrees (0-360)</returns>
    public float GetCurrentHeading()
    {
        return currentHeading;
    }
}
