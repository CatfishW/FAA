using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;

public class texturereplace_grid : MonoBehaviour
{
    /* Copyright:      SA Photonics 2018 - All rights Reserved.
       Author:         Kulwinderjeet Singh

       Comments:       The script is intended to take raw warp data in the text format 
                       and load the data as a texture in Unity shader for X and Y positions 
    */
    public Material material;
    public bool leftSideLune;
    public bool rightSideLune;

    public TextAsset Xasset;
    public TextAsset Yasset;
    /* Following variables are specific to grab 4 rgba values at a time*/
    int sourceIndex = 0;
    string[] currentrgba_X = new string[4];
    string[] currentrgba_Y = new string[4];
    byte[] integerRGBA_X = new byte[4];
    byte[] integerRGBA_Y = new byte[4];
    /* Load final Raw RawData into byte array for X and Y
     * 36784 = (1920/16 + 1 = 121) * (1200/16 + 1 = 76) * 4                      */
    byte[] Rawdata_X = new byte[36784];
    byte[] Rawdata_Y = new byte[36784];

    void Start()
    {
        if (Xasset == null || Yasset == null)
        {
            Debug.LogError("[SA147 Prewarp] Missing X/Y warp table assets; prewarp disabled.", this);
            enabled = false;
            return;
        }

        Shader warpShader = Shader.Find("lars_viewer_grid_lune");
        if (warpShader == null)
        {
            Debug.LogError("[SA147 Prewarp] Shader 'lars_viewer_grid_lune' is not available. Add it to Always Included Shaders.", this);
            enabled = false;
            return;
        }

        //cam.aspect = 1920/1200;
        /* Load the dta from .txt file into individual array of string for X and Y  */
        string[] x_table = Xasset.text.Split("\n"[0]);
        string[] y_table = Yasset.text.Split("\n"[0]);
        int i = 0;
        /* Grab the four RGBA values at a time, convert into byte, and fill into the Rawdata arrays*/
        for (int index = 0; index < x_table.Length / 4; index++)
        {
            Array.Copy(x_table, sourceIndex, currentrgba_X, 0, 4);
            integerRGBA_X = Array.ConvertAll<string, byte>(currentrgba_X, byte.Parse);
            Array.Copy(integerRGBA_X, 0, Rawdata_X, sourceIndex, 4);

            Array.Copy(y_table, sourceIndex, currentrgba_Y, 0, 4);
            integerRGBA_Y = Array.ConvertAll<string, byte>(currentrgba_Y, byte.Parse);
            Array.Copy(integerRGBA_Y, 0, Rawdata_Y, sourceIndex, 4);
            sourceIndex += 4;
            i++;
        }

        /* Create individual Textures for X and Y and adjust filtering setting to NONE*/
        material = new Material(warpShader);
        if (leftSideLune){
            material.SetFloat("_leftSideLune", 1.0f);
        }
        if (rightSideLune)
        {
            material.SetFloat("_rightSideLune", 1.0f);
        }

        Texture2D Texture_X = new Texture2D(121, 76, TextureFormat.RGBA32, false, true);
        Texture2D Texture_Y = new Texture2D(121, 76, TextureFormat.RGBA32, false, true);
        Texture_X.filterMode = FilterMode.Point;
        Texture_Y.filterMode = FilterMode.Point;
        Texture_X.anisoLevel = 1;
        Texture_Y.anisoLevel = 1;
        Texture_X.wrapMode = TextureWrapMode.Clamp;
        Texture_Y.wrapMode = TextureWrapMode.Clamp;
        Texture_X.LoadRawTextureData(Rawdata_X);
        Texture_Y.LoadRawTextureData(Rawdata_Y);
        Texture_X.Apply();
        Texture_Y.Apply();

        material.SetTexture("greenTex_X", Texture_X);
        material.SetTexture("greenTex_Y", Texture_Y);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destnation)
    {
        if (material)
        {
            Graphics.Blit(source, destnation, material);
        }
        else
        {
            Graphics.Blit(source, destnation);
            print("Could not load texture!");
        } 
    }
}
