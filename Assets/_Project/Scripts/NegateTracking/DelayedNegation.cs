using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class DelayedNegation : MonoBehaviour
{
    private GameObject Canvas;
    // Start is called before the first frame update
    void Start()
    {
        Canvas = this.transform.GetChild(0).transform.gameObject;
        StartCoroutine(DelayedDisable());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator DelayedDisable()
    {
        yield return new WaitForSeconds(15f);
        Canvas.transform.rotation = Quaternion.identity;
        Canvas.transform.SetParent(null, true);
    }
}
