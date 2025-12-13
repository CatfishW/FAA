using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class YRotationMimic : MonoBehaviour
{
    public GameObject toFollow;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        this.transform.localRotation = Quaternion.Euler(this.transform.localEulerAngles.x, toFollow.transform.localEulerAngles.y, this.transform.localEulerAngles.z);
    }
}
