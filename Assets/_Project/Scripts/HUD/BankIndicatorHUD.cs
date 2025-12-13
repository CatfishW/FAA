using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BankIndicatorHUD : MonoBehaviour
{
    public GameObject rotator;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateBank(float roll)
    {
        rotator.transform.localRotation = Quaternion.Euler(0, 0, roll);
    }
}
