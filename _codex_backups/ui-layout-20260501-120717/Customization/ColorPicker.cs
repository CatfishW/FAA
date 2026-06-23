using System.Collections.Generic;
using FAA.Customization;
using UnityEngine;
using UnityEngine.UI;

public class ColorPicker : MonoBehaviour
{
    public Color DesColor; //Default green color
    //public Color GreenColor;
    public Color WhiteColor;
    public Color hudColor;//Current HUD Color
  
    public List<Image> ImageExceptions;
    public List<Text> TextExceptions;
    [SerializeField] private bool tintOnlySymbologyElements = true;
    [SerializeField] private bool preserveElementAlpha = true;
    
 
    public int count;
    public Material material;
    public Grayscale grayscaleControl;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    void Awake()
    {

        if (material != null)
        {
            material.color = Color.green;
        }
        count = 2;
        Toggle();
        UpdateColor();
    }

    // Start is called before the first frame update
    /*void Start()
    {
        foreach (Transform image in Images)
        {
            image.GetComponent<Image>().color = hudColor;
        }
        foreach (Transform text in Texts)
        {
            text.GetComponent<Text>().color = hudColor;
        }
    }*/
   

    public void Toggle()
    {
        count++;
        float tempAlpha = hudColor.a; //Ensure brightness is the same after color toggle

        if (count == 3) //count reset
        {
            count = 1;
        }

        if (count == 1)     //default
        {
            hudColor = DesColor;
            if (material != null)
            {
                material.color = Color.green;
            }
        }
        if (count == 2)
        {
            hudColor = WhiteColor;
            if (material != null)
            {
                material.color = Color.white;
            }
            
        }

        if (tempAlpha > 0.001f)
        {
            hudColor.a = tempAlpha;
        }
        UpdateColor();
        //add another color:
        //if(count == 3)
        //{
        //    hudColor = color3;
        //    material.color = Color.red;
        //}
        
    }
    public Color GetCurrentUIColor()
    {
        return hudColor;
    }

    public void UpdateColor()
    {

        foreach (Image image in FindObjectsOfType<Image>())
        {
            if (ShouldTintImage(image))
            {
                image.color = SymbologyTintUtility.BuildTintColor(hudColor, image.color, preserveElementAlpha);
            }
        }
        foreach (Text text in FindObjectsOfType<Text>())
        {
            if (ShouldTintText(text))
            {
                text.color = SymbologyTintUtility.BuildTintColor(hudColor, text.color, preserveElementAlpha);
            }
        }

        GameObject obsObj = GameObject.FindWithTag("ObstacleMarker"); //Finds an obstacle and updates the material for all obstacles to the right color
        if (obsObj != null)
        {
            Renderer rend = obsObj.GetComponent<MeshRenderer>();
            //rend.sharedMaterial.shader = Shader.Find("Custom/S_PlayerColor"); // this is where the issue was had to add custom.
            if (rend != null && rend.sharedMaterial != null)
            {
                rend.sharedMaterial.SetColor("_Color", hudColor);
            }
        }

    }

   public void WarningColor(float newWarning)
    {
        Debug.Log("Working");
    }

    public void UpdateBrightness()
    {
        if (grayscaleControl != null)
        {
            grayscaleControl.UpdateRenderTextureBrightness(hudColor.a); //Update rendertexture brightness
        }
        foreach (Image image in FindObjectsOfType<Image>())
        {
            if (ShouldTintImage(image))
            {
                Color tempColor = image.color;
                tempColor.a = hudColor.a;
                image.color = tempColor;
            }
 
        }
        foreach (Text text in FindObjectsOfType<Text>())
        {
            if (ShouldTintText(text))
            {
                Color tempColor = text.color;
                tempColor.a = hudColor.a;
                text.color = tempColor;
            }
        }
    }
    // Update is called once per frame
    void Update()
    {
       
    }

    private bool ShouldTintImage(Image image)
    {
        if (image == null || (ImageExceptions != null && ImageExceptions.Contains(image)))
        {
            return false;
        }

        return !tintOnlySymbologyElements || SymbologyTintUtility.ShouldTintImage(image);
    }

    private bool ShouldTintText(Text text)
    {
        if (text == null || (TextExceptions != null && TextExceptions.Contains(text)))
        {
            return false;
        }

        return !tintOnlySymbologyElements || SymbologyTintUtility.ShouldTintText(text.transform);
    }
}
