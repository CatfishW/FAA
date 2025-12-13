using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class FPS : MonoBehaviour
{
    public Text FPSText;
    // Start is called before the first frame update
    void Start()
    {
        FPSText = GetComponent<Text>();   
    }

    // Update is called once per frame
    void Update()
    {
        FPSText.text = "FPS: " + (int)(1f / Time.unscaledDeltaTime);
    }
}
