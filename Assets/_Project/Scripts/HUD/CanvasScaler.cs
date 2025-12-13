using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class CanvasScaler : MonoBehaviour
{
    public GameObject Canvas1;
    public GameObject Canvas2;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(this.transform.localRotation.eulerAngles.x < 320f && this.transform.localRotation.eulerAngles.x > 40f)
        {
            Debug.Log(this.transform.localRotation.eulerAngles.x);
            Canvas1.transform.localScale = new Vector3(0f, 0f, 0f);
            Canvas2.transform.localScale = new Vector3(0f, 0f, 0f);
        }
        else
        {
            Debug.Log(this.transform.localRotation.eulerAngles.x);
            Canvas1.transform.localScale = new Vector3(1f, 1f, 1f);
            Canvas2.transform.localScale = new Vector3(1f, 1f, 1f);
        }
    }
}
