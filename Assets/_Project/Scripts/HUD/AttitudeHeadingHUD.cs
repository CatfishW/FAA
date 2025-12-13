using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttitudeHeadingHUD : MonoBehaviour
{
    public float indicatedHeading;
    public GameObject TickPrefab;
    public bool SetUpComplete;
    public float tickSpeed = 0.15f; //Controls tick speed
    public float tickOffset = 0.15f; //Controls tick separation
    // Start is called before the first frame update
    void Start()
    {
        indicatedHeading = 0;
        SetUpComplete = false;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateAttitudeHeading(float newHeading)
    {
        if (!SetUpComplete)
        {
            SetUp();
        }
        //Debug.Log(newHeading);
        int OldAltitudeHeadingDivisor = (int)((indicatedHeading) / 10);//Check to see if Tape has moved left or right one segment. Segments are 10 units long.
        indicatedHeading = newHeading;
        int newAltitudeHeadingDivisor = (int)((indicatedHeading) / 10);
        float Xoffset = indicatedHeading / 10 * tickSpeed;//0.15 units of movement per 10 degrees.
        transform.localPosition = new Vector3(-Xoffset, transform.localPosition.y);


        if (this.transform.childCount > 0)
        {
            if (newAltitudeHeadingDivisor > OldAltitudeHeadingDivisor)
            {
                int TickDiscrepancy = newAltitudeHeadingDivisor - OldAltitudeHeadingDivisor; //In case of data flow interruption, detects how many divisions apart the gauge and data is.
                //Debug.Log(TickDiscrepancy);
                while (TickDiscrepancy > 0)
                {
                    GenerateNextHigherIndicator();
                    TickDiscrepancy--;
                }
            }
            else if (newAltitudeHeadingDivisor < OldAltitudeHeadingDivisor)
            {
                int TickDiscrepancy = newAltitudeHeadingDivisor - OldAltitudeHeadingDivisor; //In case of data flow interruption, detects how many divisions apart the gauge and data is.
                //Debug.Log(TickDiscrepancy);
                while (TickDiscrepancy < 0)
                {
                    GenerateNextLowerIndicator();
                    TickDiscrepancy++;
                }
            }
        }
    }

    private void SetUp() //Place initial ticks marks depending on altitude at the moment.
    {
        if (indicatedHeading != 0)
        {
            int RoundedHeading = (int)indicatedHeading / 10;

            /*GameObject NewTick1 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick2 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick3 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick4 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick5 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick6 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick7 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick8 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);
            GameObject NewTick9 = Instantiate(TickPrefab, new Vector3(0f, 0f, 0f), Quaternion.identity, this.transform);

            NewTick1.transform.localPosition = new Vector3(-40f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick2.transform.localPosition = new Vector3(-30f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick3.transform.localPosition = new Vector3(-20f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick4.transform.localPosition = new Vector3(-10f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick5.transform.localPosition = new Vector3(0f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick6.transform.localPosition = new Vector3(10f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick7.transform.localPosition = new Vector3(20f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick8.transform.localPosition = new Vector3(30f + InitialXOffset, TickPrefab.transform.localPosition.y);
            NewTick9.transform.localPosition = new Vector3(40f + InitialXOffset, TickPrefab.transform.localPosition.y);

            NewTick1.transform.localRotation = Quaternion.identity;
            NewTick2.transform.localRotation = Quaternion.identity;
            NewTick3.transform.localRotation = Quaternion.identity;
            NewTick4.transform.localRotation = Quaternion.identity;
            NewTick5.transform.localRotation = Quaternion.identity;
            NewTick6.transform.localRotation = Quaternion.identity;
            NewTick7.transform.localRotation = Quaternion.identity;
            NewTick8.transform.localRotation = Quaternion.identity;
            NewTick9.transform.localRotation = Quaternion.identity;*/

            transform.GetChild(0).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 - 40);
            transform.GetChild(1).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 - 30);
            transform.GetChild(2).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 - 20);
            transform.GetChild(3).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 - 10);
            transform.GetChild(4).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 + 0);
            transform.GetChild(5).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 + 10);
            transform.GetChild(6).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 + 20);
            transform.GetChild(7).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 + 30);
            transform.GetChild(8).GetComponent<AttitudeHeadingTick>().SetText(RoundedHeading * 10 + 40);

            SetUpComplete = true;
        }
    }

    private void GenerateNextHigherIndicator()
    {
        GameObject HighestTick = this.transform.GetChild(0).gameObject; //Leftmost Tick used to create next right tick.
        HighestTick.transform.localPosition += new Vector3(9*tickOffset, 0f); //Move to rightmost position. 
        HighestTick.GetComponent<AttitudeHeadingTick>().SetText(HighestTick.GetComponent<AttitudeHeadingTick>().GetNum() + 90);//Add span to lowest current tick make it the new highest.
        HighestTick.transform.SetAsLastSibling(); //Last sibling should always be the rightmost tick.
    }

    private void GenerateNextLowerIndicator()
    {
        GameObject LowestTick = this.transform.GetChild(8).gameObject; //Rightmost Tick used to create next left tick.
        LowestTick.transform.localPosition -= new Vector3(9*tickOffset, 0f); //Move to leftmost position.
        LowestTick.GetComponent<AttitudeHeadingTick>().SetText(LowestTick.GetComponent<AttitudeHeadingTick>().GetNum() - 90 + 360);//subtract span to highest current make it the new lowest. 
        //Add 360 so that we avoid a negative heading.
        LowestTick.transform.SetAsFirstSibling(); //First sibling should always be the leftmost tick.
    }
}
