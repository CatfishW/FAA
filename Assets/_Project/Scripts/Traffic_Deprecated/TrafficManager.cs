using UnityEngine;

public class TrafficManager : MonoBehaviour
{
    public GameObject blipPrefab; // Prefab for the radar blips
    public Transform radarCenter; // Center of the radar
    public int blipCount = 10;    // Number of blips to spawn

    public float radarRadius = 50f; // Radius of the radar circle

    void Start()
{
    float minDistance = radarRadius / 2; // Minimum distance from the center

    for (int i = 0; i < blipCount; i++)
    {
        // Spawn a blip at a random position within the radar radius
        GameObject blip = Instantiate(blipPrefab, radarCenter);
        Vector2 randomPosition;

        // Ensure the random position is at least minDistance away from the center
        do
        {
            randomPosition = Random.insideUnitCircle * radarRadius;
        } while (randomPosition.magnitude < minDistance);

        blip.transform.localPosition = randomPosition;
    }
}
}
