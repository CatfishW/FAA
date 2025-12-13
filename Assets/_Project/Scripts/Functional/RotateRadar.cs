using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateRadar : MonoBehaviour
{
    // Speed of rotation
    public float rotationSpeed = 100f;

    // Update is called once per frame
    void Update()
    {
        // Rotate the pivot point around its Z axis
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}