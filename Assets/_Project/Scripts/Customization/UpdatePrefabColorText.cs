using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class UpdatePrefabColorText : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        try{
            this.GetComponent<Text>().color = GameObject.Find("UIEditor").GetComponent<ColorPicker>().GetCurrentUIColor();
        }
        catch{
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
