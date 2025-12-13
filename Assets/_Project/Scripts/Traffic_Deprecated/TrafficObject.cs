using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TrafficObject : MonoBehaviour
{
    public float radarRadius = 30f; // Radius of the radar circle
    public float speed = 5f;      // Movement speed
    public float rotationChangeInterval = 1f; // Time interval for rotation change

    private Vector2 direction;     // Current movement direction
    private float rotationChangeTimer; // Timer for rotation change
    private List<Vector3> positions; // List to store positions

    void Start()
    {
        // Assign a random direction for the blip
        direction = Random.insideUnitCircle.normalized;
        // Initialize the rotation change timer
        rotationChangeTimer = rotationChangeInterval;
        // Initialize the positions list
        positions = new List<Vector3>();
    }

    void Update()
    {
        // Move the blip
        transform.localPosition += (Vector3)(direction * speed * Time.deltaTime);
        //Change Rotation
        transform.rotation = Quaternion.LookRotation(Vector3.forward, direction);

        // Store the current position
        positions.Add(transform.localPosition);

        // Check if the blip is outside the radar radius
        if (Vector2.Distance(Vector2.zero, transform.localPosition) > radarRadius)
        {
            // Reflect the direction if it hits the boundary
            direction = Vector2.Reflect(direction, (Vector2.zero - (Vector2)transform.localPosition).normalized);
        }

        // Decrement the rotation change timer
        rotationChangeTimer -= Time.deltaTime;

        // Change DIRECTION if the timer reaches zero
        if (rotationChangeTimer <= 0f)
        {
            float randomAngle = Random.Range(0f, 360f);
            direction = Quaternion.Euler(0f, 0f, randomAngle) * direction;
            rotationChangeTimer = rotationChangeInterval;
        }
    }
}