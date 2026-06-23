using System.Collections;
using System.Collections.Generic;
using FAA.Customization;
using UnityEngine;
using UnityEngine.UI;

public class UpdatePrefabColor : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        try
        {
            Image image = GetComponent<Image>();
            ColorPicker picker = GameObject.Find("UIEditor").GetComponent<ColorPicker>();
            if (image != null && picker != null && SymbologyTintUtility.ShouldTintImage(image))
            {
                image.color = SymbologyTintUtility.BuildTintColor(picker.GetCurrentUIColor(), image.color, true);
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
