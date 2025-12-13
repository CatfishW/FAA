using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasShader : MonoBehaviour
{
    // Start is called before the first frame update
    public Shader unlitUIShader;
    void Start()
    {
        //unlitUIShader = Shader.Find("UI/Unlit/Transparent");

        Canvas.GetDefaultCanvasMaterial().shader = unlitUIShader;
    }

    // Update is called once per frame

}