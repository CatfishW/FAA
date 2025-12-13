using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetMinMaxY : MonoBehaviour
{
    public TerrainData td;
    Bounds bounds;
    float max;
    float min;
    Material mat;
    // Start is called before the first frame update
    void Start()
    {
        td = this.GetComponent<TerrainCollider>().terrainData;
        bounds = td.bounds;
        max = bounds.center.y + bounds.extents.y;
        min = bounds.center.y - bounds.extents.y;
        mat = this.GetComponent<Terrain>().materialTemplate;
        //mat.SetFloat("_Max", max);
        //mat.SetFloat("_Min", min);
        //Debug.Log(this.gameObject.name);
        //Debug.Log(max);
        //Debug.Log(min);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
