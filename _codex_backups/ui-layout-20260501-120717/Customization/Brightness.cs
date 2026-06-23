using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Brightness : MonoBehaviour
{
    //[SerializeField]
    //private Color color;
    public bool hidden;
    public float changeAmount;
    public float previousValue; //Utilized for hiding the HUD
    public float alpha;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        hidden = false;
        alpha = 1f;
        changeAmount = 0.05f;
    }

    // Update is called once per frame
    void Update()
    {
      
    }

    public void IncreaseBrightness()
    {
        if (this.GetComponent<ColorPicker>().hudColor.a < 1f)
        {
            Debug.Log("Increasing Brightness");
            this.GetComponent<ColorPicker>().hudColor.a += changeAmount;
            this.GetComponent<ColorPicker>().UpdateBrightness();
            alpha = this.GetComponent<ColorPicker>().hudColor.a;
        }

    }

    public void DecreaseBrightness()
    {
        if (this.GetComponent<ColorPicker>().hudColor.a > 0f)
        {
            Debug.Log("Decreasing Brightness");
            this.GetComponent<ColorPicker>().hudColor.a -= changeAmount;
            this.GetComponent<ColorPicker>().UpdateBrightness();
            alpha = this.GetComponent<ColorPicker>().hudColor.a;
        }
        
    }

    public void ToggleHUD()
    {
        if(!hidden)
        {
            HideHUD();
            hidden = true;
        }
        else
        {
            ShowHUD();
            hidden = false;
        }
    }

    public void HideHUD()
    {
        if (this.GetComponent<ColorPicker>().DesColor.a != 0)
        {
            previousValue = this.GetComponent<ColorPicker>().DesColor.a;
        }
        this.GetComponent<ColorPicker>().hudColor.a = 0f;
        this.GetComponent<ColorPicker>().UpdateBrightness();
    }

    public void ShowHUD()
    {
        this.GetComponent<ColorPicker>().hudColor.a = previousValue;
        this.GetComponent<ColorPicker>().UpdateBrightness();
    }

}
