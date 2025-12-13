using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CourseDeviation : MonoBehaviour
{
    public GameObject localizerDeviation;
    //public Text windspeedText;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    //UpdateDeviation((int)navsourceseleced,(float)nav1course, (float)nav2course, (float)gpscourse)
    public void UpdateDeviation(int navsourceselected, float nav1deviation, float nav2deviation, float gpsdeviation)
    {
        //Debug.Log(navsourceselected);
        switch (navsourceselected)
        {
            case 0:
                float devnum1 = nav1deviation * 12f;
                localizerDeviation.transform.localPosition = new Vector3(devnum1, 0f, 0f);
                
                break;
            case 1:
                float devnum2 = nav2deviation * 12f;
                localizerDeviation.transform.localPosition = new Vector3(devnum2, 0f, 0f);
             
                break;
            case 2:
                float devnum3 = gpsdeviation * 12f;
                localizerDeviation.transform.localPosition = new Vector3(devnum3, 0f, 0f);
              
                break;
        }

    }

    public void UpdateCourse(int navsourceseleced, float nav1course, float nav2course, float gpscourse) //For if we need course heading
    {
        //Current implementation does not utilize course heading
        switch (navsourceseleced)
        {
            case 0:
                float num1 = nav1course * -1f;
                this.transform.localRotation = Quaternion.Euler(0, 0, num1);
                break;
            case 1:
                float num2 = nav2course * -1f;
                this.transform.localRotation = Quaternion.Euler(0, 0, num2);

                break;
            case 2:
                float num3 = gpscourse * -1f;
                this.transform.localRotation = Quaternion.Euler(0, 0, num3);
                //Debug.Log(gpscourse);
                break;

        }

        //float num = deg * -1f;   
        //this.transform.localRotation = Quaternion.Euler(0, 0, num);

    }
}
