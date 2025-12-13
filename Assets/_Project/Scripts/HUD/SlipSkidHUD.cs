using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlipSkidHUD : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void UpdateSlip(float slip)
    {
        this.gameObject.transform.localPosition = new Vector3(-5 * slip, -10, 0);
    }
}
