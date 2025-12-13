using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadarManager : MonoBehaviour
{
    [Header("Radar Settings")]
    public Transform Aircraft;
    public RectTransform radarUI;
    public float radarRadius = 200f; // Pixels
    public float worldRadius = 50f; // World units

    [Header("Icon Prefabs")]
    public GameObject onScreenPrefab;
    public GameObject offScreenPrefab;

    [Header("Scaling & Zoom")]
    public float zoomSpeed = 10f;
    public float minZoom = 30f;
    public float maxZoom = 100f;

    [Header("Performance Settings")]
    public float updateInterval = 0.1f;

    private List<RadarTarget> targets = new List<RadarTarget>();
    private Queue<GameObject> iconPool = new Queue<GameObject>();
    private float nextUpdateTime;

    private void Update()
    {
        // Zoom with mouse scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        worldRadius = Mathf.Clamp(worldRadius - scroll * zoomSpeed, minZoom, maxZoom);

        // Update radar at intervals
        if (Time.time >= nextUpdateTime)
        {
            UpdateRadar();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    public void RegisterTarget(Transform target, Color iconColor)
    {
        var radarTarget = new RadarTarget { target = target, color = iconColor };
        targets.Add(radarTarget);
    }

    public void RemoveTarget(Transform target)
    {
        targets.RemoveAll(t => t.target == target);
    }

    private void UpdateRadar()
    {
        // Clear the radar UI
        foreach (Transform child in radarUI)
        {
            child.gameObject.SetActive(false);
            iconPool.Enqueue(child.gameObject);
        }

        // Process each target
        foreach (var radarTarget in targets)
        {
            if (radarTarget.target == null) continue;

            Vector3 direction = radarTarget.target.position - Aircraft.position;
            float distance = direction.magnitude;

            if (distance > worldRadius)
            {
                // Off-screen indicator
                CreateOffScreenIndicator(direction, radarTarget.color);
            }
            else
            {
                // On-screen indicator
                CreateOnScreenIndicator(direction, distance / worldRadius, radarTarget.color);
            }
        }
    }

    private void CreateOnScreenIndicator(Vector3 direction, float scale, Color color)
    {
        var icon = GetIcon(onScreenPrefab);
        Vector2 screenPosition = new Vector2(direction.x, direction.z) / worldRadius * radarRadius;

        icon.transform.localPosition = screenPosition;
        icon.GetComponent<Image>().color = color;
        icon.transform.localScale = Vector3.one * scale;
    }

    private void CreateOffScreenIndicator(Vector3 direction, Color color)
    {
        try{
            var icon = GetIcon(offScreenPrefab);
            Vector2 clampedPosition = Vector2.ClampMagnitude(new Vector2(direction.x, direction.z), radarRadius);
            icon.transform.localPosition = clampedPosition;
            icon.GetComponent<Image>().color = color;
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            icon.transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        catch{
            
        }
    }

    private GameObject GetIcon(GameObject prefab)
    {
        if (iconPool.Count > 0)
        {
            var icon = iconPool.Dequeue();
            icon.SetActive(true);
            return icon;
        }

        return Instantiate(prefab, radarUI);
    }

    private class RadarTarget
    {
        public Transform target;
        public Color color;
    }
}
