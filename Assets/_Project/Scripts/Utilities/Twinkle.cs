using UnityEngine;
using UnityEngine.UI;

public class TwinkleEffect : MonoBehaviour
{
    private Image imageComponent;
    private Color originalColor;
    private bool isTwinkling = false;
    public float twinkleSpeed = 1f; // Speed of twinkling
    public float minAlpha = 0.2f;   // Minimum transparency
    public float maxAlpha = 1f;     // Maximum transparency

    void Start()
    {
        // Get the Image component attached to this GameObject
        imageComponent = GetComponent<Image>();
        if (imageComponent != null)
        {
            // Store the original color
            originalColor = imageComponent.color;
        }
        StartTwinkling(); // Start the twinkling effect
    }

    void Update()
    {
        // If the Image component is found, proceed with the twinkling effect
        if (imageComponent != null && isTwinkling)
        {
            // Use Mathf.PingPong to oscillate between minAlpha and maxAlpha
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, Mathf.PingPong(Time.time * twinkleSpeed, 1));
            Color newColor = originalColor;
            newColor.a = alpha; // Set the new alpha value
            imageComponent.color = newColor;
        }
    }

    // Call this function to start the twinkling effect
    public void StartTwinkling()
    {
        isTwinkling = true;
    }

    // Call this function to stop the twinkling effect
    public void StopTwinkling()
    {
        isTwinkling = false;
        // Set the image back to full opacity
        if (imageComponent != null)
        {
            Color newColor = originalColor;
            newColor.a = 1f;
            imageComponent.color = newColor;
        }
    }
}
