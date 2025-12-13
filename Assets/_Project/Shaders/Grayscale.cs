using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Grayscale : MonoBehaviour
{
    Material mat;
    public Shader _shader;
    public float _isOn;
    void Start()
    {
        mat = new Material(_shader);

    }
    public void UpdateRenderTextureBrightness(float alpha)
    {
        mat.SetFloat("_Transparency", alpha);
    }
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, mat );
    }

    void Update()
    {
        mat.SetFloat("_isOn", _isOn);
    }
}
