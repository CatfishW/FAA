using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideHud : MonoBehaviour
{
    //public Transform Body;

    public Transform UICamera;
    public Transform BoundA;
    public Transform BoundB;
    bool hasShown = true;
    [SerializeField]
    float dotProdA;
    [SerializeField]
    float dotProdB;
    public bool FullHide;
    public bool Alock;
    public bool Block;
    Vector3 vectorA;
    Vector3 vectorB;
    //float dotProdABound;
    //float dotProdBBound;
    //================
    float angle1 = 70f;
    float angle2 = 110f;
    public float tempA;
    public float tempB;
    //1: Up, 2: Down, 3: Center
    //================
    //135 degrees from Vector.up of the transform is 90 degrees down from the 45 degree mark


    private void OnEnable()
    {
        //NumControl.OnEvent2 += FullHideHUD;//Color picker does this now
    }
    private void OnDisable()
    {
        //NumControl.OnEvent2 -= FullHideHUD;//Color picker does this now
    }
    // Start is called before the first frame update
    void Start()
    {
        FullHide = false;
        Alock = false;
        Block = false;
        vectorA = (Quaternion.AngleAxis(angle1, transform.right) * Vector3.up).normalized;
        vectorB = (Quaternion.AngleAxis(angle2, transform.right) * Vector3.up).normalized;
        BoundA.position = vectorA;
        BoundB.position = vectorB;
        Debug.DrawLine(new Vector3(0f, 0f, 0f), vectorA, new Color(1f, 0f, 0f), 100f, false);
        Debug.DrawLine(new Vector3(0f, 0f, 0f), vectorB, new Color(1f, 0f, 0f), 100f, false);

    }

    // Update is called once per frame
    void Update()
    {
        //1: Up, 2: Down, 3: Center
        dotProdA = Vector3.Dot(BoundA.localPosition.normalized, UICamera.transform.parent.InverseTransformDirection(UICamera.forward).normalized);
        dotProdB = Vector3.Dot(BoundB.localPosition.normalized, UICamera.transform.parent.InverseTransformDirection(UICamera.forward).normalized);
            if (System.Math.Round(dotProdA, 3) > 0.998 && hasShown)
            {
                Alock = true;
                //Look Up
                tempB = dotProdB;
                this.GetComponent<Brightness>().HideHUD();
                Debug.Log("HIDING HUD");
                hasShown = false;
            }

            if (System.Math.Round(dotProdB, 3) > 0.998 && hasShown)
            {
                Block = true;
                //Look Down
                tempA = dotProdA;
                this.GetComponent<Brightness>().HideHUD();
                Debug.Log("HIDING HUD");
                hasShown = false;
            }
            if (System.Math.Round(dotProdA, 3) < 0.995 && Alock && !hasShown && (dotProdB >= tempB))
            {
                //Look center
                this.GetComponent<Brightness>().ShowHUD();
                Debug.Log("Showing HUD");
                Alock = false;
                hasShown = true;
            }
            if (System.Math.Round(dotProdB, 3) < 0.995 && Block && !hasShown && (dotProdA >= tempA))
            {
                //Look center
                this.GetComponent<Brightness>().ShowHUD();
                Debug.Log("Showing HUD");
                Block = false;
                hasShown = true;
            }
    }

    public void FullHideHUD()
    {
        FullHide = !FullHide;
        if (FullHide)
        {
            this.GetComponent<Brightness>().HideHUD();
            Debug.Log("FullHide");
        }
        else
        {
            this.GetComponent<Brightness>().ShowHUD();
            Debug.Log("Full UnHide");
        }

    }


}
