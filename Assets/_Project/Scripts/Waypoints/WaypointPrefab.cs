using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class WaypointPrefab : MonoBehaviour
{
    // Start is called before the first frame update
    GameObject track;
    public Text nameText;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        transform.LookAt(track.transform, transform.up);
        nameText.text = transform.parent.name;
    }
}
