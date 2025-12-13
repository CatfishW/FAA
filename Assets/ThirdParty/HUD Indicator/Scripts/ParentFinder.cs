using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HUDIndicator
{
    public class ParentFinder : MonoBehaviour
    {
        // Start is called before the first frame update
        public string parentname = "IndicatorOnScreen:Waypoint";
        private GameObject parent;
        public GameObject child;
        public Vector3 offset = new Vector3(0, 1, 0);
        void Update()
        {
            if(parent == null)
                parent = GameObject.Find(parentname);
            else{
                if(child!=null)
                {
                    child.transform.parent = parent.transform;
                    child.transform.localPosition = Vector3.zero + offset;
                }
            }
        
        }
    }
}
