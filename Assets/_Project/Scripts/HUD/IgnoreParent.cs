using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IgnoreParent : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.localEulerAngles = new Vector3(transform.parent.parent.eulerAngles.x * -1.0f,
                                                      transform.parent.parent.eulerAngles.y * -1.0f,
                                                      transform.parent.parent.eulerAngles.z * -1.0f);
    }
}
