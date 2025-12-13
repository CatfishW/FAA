using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockHud : MonoBehaviour
{
    public GameObject body;
    public GameObject Dampener;
    public bool isLocked;

    public Vector3 startingPosition;
    public Quaternion startingRotation;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        isLocked = false;
        startingPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z);
        startingRotation = Quaternion.identity;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Lock()
    {
        if(isLocked== true)//Unlock
        {
            this.transform.SetParent(Dampener.transform, false);
            this.transform.localPosition = startingPosition;
            this.transform.localRotation = startingRotation;
            isLocked = false;
        }
        else //Lock
        {
            this.transform.SetParent(body.transform, true);
            isLocked = true;
        }
        
    }
}
