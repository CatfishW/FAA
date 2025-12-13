using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CompassNavigatorPro;
public class TrafficSpawn : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] aircraftPrefabs;
    public string[] titles;
    public GameObject aircraftPOI;
    [Header("Spawning Settings")]
    public float spawnInterval = 5f;
    public int maxAircraft = 5;
    public Vector2 spawnXRange = new Vector2(-50f, 50f);
    public Vector2 spawnZRange = new Vector2(-50f, 50f);
    public Vector2 spawnYRange = new Vector2(100f, 200f);
    
    [Header("Movement Settings")]
    // If set, this vector will override the prefab's own movement.
    public Vector3 moveCoordinates;
    // When moveCoordinates is zero and this is true, assign a random movement direction that changes over time.
    public bool useRandomMovement = true;
    public GameObject SpawnParent;

    void Start()
    {
        StartCoroutine(SpawnAircraft());
    }

    IEnumerator SpawnAircraft()
    {
        while (true)
        {
            if (SpawnParent.transform.childCount < maxAircraft)
            {
                SpawnOneAircraft();
            }
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnOneAircraft()
    {
        if (aircraftPrefabs == null || aircraftPrefabs.Length == 0)
        {
            Debug.LogWarning("No aircraftPrefabs assigned.");
            return;
        }

        GameObject prefab = aircraftPrefabs[Random.Range(0, aircraftPrefabs.Length)];

        float xPos = Random.Range(spawnXRange.x, spawnXRange.y);
        float yPos = Random.Range(spawnYRange.x, spawnYRange.y);
        float zPos = Random.Range(spawnZRange.x, spawnZRange.y);
        Vector3 spawnPosition = new Vector3(xPos, yPos, zPos);

        GameObject aircraftInstance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        //set parent to this one.
        aircraftInstance.transform.SetParent(SpawnParent.transform);
        GameObject poi_object = Instantiate(aircraftPOI, aircraftInstance.transform.position, Quaternion.identity);
        CompassProPOI poi = poi_object.GetComponent<CompassProPOI>();
        poi.id = Random.Range(0, 10000);
        poi.title = titles[Random.Range(0, titles.Length)];
        poi_object.transform.SetParent(aircraftInstance.transform);
        poi_object.transform.localPosition = new Vector3(0, 0, 0);
        poi_object.SetActive(true);
        // Try to override movement if the prefab has an AircraftMovement component.
        // AircraftMovement movementComponent = aircraftInstance.GetComponent<AircraftMovement>();
        // if (movementComponent != null)
        // {
        //     if (moveCoordinates != Vector3.zero)
        //     {
        //         movementComponent.moveDirection = moveCoordinates.normalized;
        //     }
        //     else if (useRandomMovement)
        //     {
        //         // Attach the RandomMovement component if not already present.
        //         RandomMovement randomMovement = aircraftInstance.GetComponent<RandomMovement>();
        //         if (randomMovement == null)
        //         {
        //             randomMovement = aircraftInstance.AddComponent<RandomMovement>();
        //         }
        //         // Use the current random direction as the movement.
        //         movementComponent.moveDirection = randomMovement.GetCurrentDirection();
        //     }
        //     // Otherwise, leave the prefab's own movement intact.
        // }

        

    }
}