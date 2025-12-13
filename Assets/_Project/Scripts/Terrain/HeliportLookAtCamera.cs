using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeliportLookAtCamera : MonoBehaviour
{
    //public GameObject cameraFollow;
    // Start is called before the first frame update
    void Start()
    {
        //cameraFollow = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        //Color color = new Color(0.0f, 0.0f, 1.0f);
        //Debug.DrawLine(Vector3.zero, cameraFollow.transform.position, color);
        //Debug.DrawLine(transform.forward, transform.position, color);
        //Debug.DrawLine(cameraFollow.transform.position, transform.position);
        //Debug.DrawLine(Vector3.zero, Vector3.up);

        var pos = transform.position;
        var camPos = Camera.main.transform.position;

        pos.y = 0;
        camPos.y = 0;

        var targetRot = Quaternion.LookRotation(camPos - pos);

        transform.rotation = targetRot;
        //Debug.Log(camera.transform.rotation* Vector3.down);
    }
}
