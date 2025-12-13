using UnityEngine;

public class TrailReferenceRender : MonoBehaviour
{
    public Transform targetObject; // Reference to the target object

    void Update()
    {
        if (targetObject != null)
        {
            // Update the position of the renderer to match the target object's position
            transform.position = targetObject.position;
        }
    }
}