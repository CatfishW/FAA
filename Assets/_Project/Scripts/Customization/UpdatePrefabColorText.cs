using System.Collections;
using System.Collections.Generic;
using FAA.Customization;
using UnityEngine;
using UnityEngine.UI;


public class UpdatePrefabColorText : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        try
        {
            Text text = GetComponent<Text>();
            ColorPicker picker = GameObject.Find("UIEditor").GetComponent<ColorPicker>();
            if (text != null && picker != null && SymbologyTintUtility.ShouldTintText(text.transform))
            {
                text.color = SymbologyTintUtility.BuildTintColor(picker.GetCurrentUIColor(), text.color, true);
            }
        }
        catch{
            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
