using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AltitudeHUD : MonoBehaviour
{
    public float indicatedAltitude;
    public GameObject TickPrefab;
    public GameObject MasterIndicator;
    public GameObject KnotsBox;
    public GameObject NumText;
    public float tickSpeed = 0.17f; //Controls tick speed
    public float tickOffset = 0.17f; //Controls tick separation
    public ColorPicker UIEditor;
    private Color returnColor;

    // Start is called before the first frame update
    void Start()
    {
        indicatedAltitude = 0;
    }

    // Update is called once per frame
    void Update()
    {
        try{
            returnColor = WarningColor.Warning3(10000, 19000f, UIEditor.hudColor, Color.red, indicatedAltitude, Color.yellow, .5f); //0-1000 green | 1000 -> 2500 yellow | 2500 -> 4000 red
        

            KnotsBox.GetComponent<Image>().color = returnColor;
            NumText.GetComponent<Text>().color = returnColor;
        }
        catch{

        }

    }

    public void UpdateAltitude(float NewAltitude)
    {
        //Tracks to see if the aircraft has moved to the next 200 ft margin.
        //If it has, generate a new higher or lower tick mark.
        int OldAltitudeDivisor = (int)(indicatedAltitude / 200); //Checks to see if tape has moved down one segment.
        indicatedAltitude = NewAltitude;
        int NewAltitudeDivisor = (int)(indicatedAltitude / 200); 

        float YOffset = indicatedAltitude / 200 * tickSpeed;//0.17 units of Y movement offset.
        int intAlt = (int)NewAltitude; //Remove decimal places.
        MasterIndicator.GetComponent<TickHUD>().SetExactAltitude(intAlt); //Set exact altitude indicator.
        this.transform.localPosition = new Vector3(this.transform.localPosition.x, -YOffset);//Move tape down when moving up.
        if (this.transform.childCount > 0)
        {
            if (NewAltitudeDivisor > OldAltitudeDivisor)
            {
                int TickDiscrepancy = NewAltitudeDivisor - OldAltitudeDivisor; //In case of data flow interruption, detects how many divisions apart the gauge and data is.
                while (TickDiscrepancy > 0)
                {
                    GenerateNextHigherIndicator();
                    TickDiscrepancy--;
                }
            }
            else if (NewAltitudeDivisor < OldAltitudeDivisor)
            {
                int TickDiscrepancy = NewAltitudeDivisor - OldAltitudeDivisor; //In case of data flow interruption, detects how many divisions apart the gauge and data is.
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
        
        if (indicatedAltitude != 0f)
        {
            int InitialYOffset = (int)indicatedAltitude / 200;

            //Debug.Log(indicatedAltitude);
            //Debug.Log(InitialYOffset);
        
            transform.GetChild(0).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 + 600);
            transform.GetChild(1).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 + 400);
            transform.GetChild(2).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 + 200);
            transform.GetChild(3).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 + 0);
            transform.GetChild(4).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 - 200);
            transform.GetChild(5).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 - 400);
            transform.GetChild(6).GetComponent<TickHUD>().SetAltitude(InitialYOffset * 200 - 600);
        }
     
    }
    private void GenerateNextHigherIndicator()
    {
        GameObject LowestTick = this.transform.GetChild(6).gameObject; //Lowest Tick used to create next higher tick.
        LowestTick.transform.localPosition += new Vector3(0f, 7 * tickOffset); //Move up to higher position.
        //LowestTick.GetComponent<TickHUD>().SetAltitude(LowestTick.GetComponent<TickHUD>().GetShownAltitude() + 1400); //Add height to make it the new highest.
        LowestTick.transform.SetAsFirstSibling(); //First sibling should always be the highest number.
    }

    private void GenerateNextLowerIndicator()
    {
        GameObject HighestTick = this.transform.GetChild(0).gameObject; //Highest Tick used to create next lowest tick.
        HighestTick.transform.localPosition += new Vector3(0f, -7 * tickOffset); //Move down to lower position.
        //HighestTick.GetComponent<TickHUD>().SetAltitude(HighestTick.GetComponent<TickHUD>().GetShownAltitude() - 1400); //Lower height to make it the new lowest.
        HighestTick.transform.SetAsLastSibling(); //Last sibling should always be the lowest number.
    }
}
