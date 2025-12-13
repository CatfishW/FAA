using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AirspeedHUD : MonoBehaviour
{
    public float indicatedAirspeed;
    public bool SetUpComplete;
    public GameObject TickPrefab;
    public Text MasterIndicator;
    public float tickSpeed = 0.17f; //Controls tick speed
    public float tickOffset = 0.17f; //Controls tick separation
    // Start is called before the first frame update
    
    public AudioClip AClip;
    private AudioSource AS;
    bool onSoundCooldown;

    public ColorPicker UIEditor;
    public float warningValue;

    public GameObject MasterTick;
    public GameObject KnotsBox;
    private Color returnColor;
    void Start()
    {
        AS = transform.GetComponent<AudioSource>();
        onSoundCooldown = false;

        SetUpComplete = false;
        indicatedAirspeed = 0;
 
    }

    // Update is called once per frame
    void Update()
    {

       // Color returnColor = WarningColor.Warning2(60f, 160f, UIEditor.hudColor, Color.red, indicatedAirspeed);
        try{
            returnColor = WarningColor.Warning3(159f, 160f, UIEditor.hudColor, Color.red, indicatedAirspeed, Color.yellow, .5f); //60 - 92.5 = green to yellow | 92.5 - 125 = yellow - red
        


            MasterTick.GetComponent<Text>().color = returnColor;
            KnotsBox.GetComponent<Image>().color = returnColor;

            if (!AS.isPlaying && !onSoundCooldown && indicatedAirspeed >= 160)
            {
                onSoundCooldown = true;
                StartCoroutine(PlaySound());
                //AS.clip = AClip;
                //AS.Play();
            }
        }
        catch{
            //Debug.Log("Warning Color Error");
        }

    }


    public void UpdateAirspeed(float NewAirspeed)
    {
        if (!SetUpComplete)
        {
            SetUp();
        }
        //Tracks to see if the aircraft has moved to the next 20 knot margin.
        //If it has, generate a new higher or lower tick mark.
        
        int OldAirspeedDivisor = (int)(indicatedAirspeed / 20);
        indicatedAirspeed = NewAirspeed;
        int NewAirspeedDivisor = (int)(indicatedAirspeed / 20);

        float YOffset = indicatedAirspeed / 20f * tickSpeed;//0.17 units of Y movement per 20 knots.
        //Debug.Log(YOffset);
        int intAir = (int)indicatedAirspeed; //Remove decimal places.
        MasterIndicator.text = intAir.ToString(); //Set exact airspeed indicator.
        
        this.transform.localPosition = new Vector3(this.transform.localPosition.x, -YOffset);//Move tape down when speeding up.

        if (this.transform.childCount > 0)
        {
            if (NewAirspeedDivisor > OldAirspeedDivisor)
            {
                int TickDiscrepancy = NewAirspeedDivisor - OldAirspeedDivisor; //In case of data flow interruption, detects how many divisions apart the gauge and data is.
                while (TickDiscrepancy > 0)
                {
                    GenerateNextHigherIndicator();
                    TickDiscrepancy--;
                }
            }
            else if (NewAirspeedDivisor < OldAirspeedDivisor)
            {
                int TickDiscrepancy = NewAirspeedDivisor - OldAirspeedDivisor; //In case of data flow interruption, detects how many divisions apart the gauge and data is.
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
        if (indicatedAirspeed != 0f)
        {
            int InitialYOffset = (int)indicatedAirspeed / 20;

            //Debug.Log(indicatedAirspeed);
            //Debug.Log(InitialYOffset);


            transform.GetChild(0).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 + 80);
            transform.GetChild(1).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 + 60);
            transform.GetChild(2).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 + 40);
            transform.GetChild(3).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 + 20);
            transform.GetChild(4).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 + 0);
            transform.GetChild(5).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 - 20);
            transform.GetChild(6).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 - 40);
            transform.GetChild(7).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 - 60);
            transform.GetChild(8).GetComponent<AirspeedTick>().setAirspeed(InitialYOffset * 20 - 80);
            SetUpComplete = true;
        }
    }

    private void GenerateNextHigherIndicator()
    {
        GameObject LowestTick = this.transform.GetChild(8).gameObject; //Lowest Tick used to create next higher tick.
        LowestTick.transform.localPosition += new Vector3(0f, 9 * tickOffset); //Move up to higher position.
        LowestTick.GetComponent<AirspeedTick>().setAirspeed(LowestTick.GetComponent<AirspeedTick>().getAirspeed() + 180); //Add speed to make it the new highest.
        LowestTick.transform.SetAsFirstSibling(); //First sibling should always be the highest number.
    }

    private void GenerateNextLowerIndicator()
    {
        GameObject HighestTick = this.transform.GetChild(0).gameObject; //Highest Tick used to create next lowest tick.
        HighestTick.transform.localPosition += new Vector3(0f, -9 * tickOffset); //Move down to lower position.
        HighestTick.GetComponent<AirspeedTick>().setAirspeed(HighestTick.GetComponent<AirspeedTick>().getAirspeed() - 180); //Lower speed to make it the new lowest.
        HighestTick.transform.SetAsLastSibling(); //Last sibling should always be the lowest number.
    }
    public IEnumerator PlaySound()
    {
        Debug.Log("Playing Sound");

        if (!AS.isPlaying)
        {
            AS.clip = AClip;
            AS.Play();

        }
        yield return new WaitForSeconds(1f);
        onSoundCooldown = false;
    }
}
