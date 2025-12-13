using UnityEngine;

public class RandomMovement : MonoBehaviour
{
    [Header("Random Movement Settings")]
    public float changeInterval = 3f;
    public float rotationSpeed = 5f;

    private Vector3 targetDirection;
    private float timer;

    void Start()
    {
        ChangeDirection();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= changeInterval)
        {
            ChangeDirection();
            timer = 0f;
        }
        // Smoothly update the facing direction.
        transform.forward = Vector3.Lerp(transform.forward, targetDirection, rotationSpeed * Time.deltaTime);
    }

    void ChangeDirection()
    {
        targetDirection = Random.insideUnitSphere.normalized;
    }

    public Vector3 GetCurrentDirection()
    {
        return targetDirection;
    }
}