using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VerticalSpeedHUD : MonoBehaviour
{
    public bool flipped; //Is the arrow flipped?
    public float IndicatorLocation;
    public float BarScale;
    public float IndicatorMult;
    public float BarScaleMult;
    public GameObject Indicator;
    public GameObject Arrow;
    public GameObject Bar;
    public float indicatedVerticalSpeed;
    public GameObject VSpeedHudBox;
    public GameObject VerticalSpeedText;
    public ColorPicker UIEditor;
    public GameObject KnotsBox;
    public GameObject NumText;
    public float indicatedAlt;
    public Color returnColor;   


    public AudioClip AClip;
    private AudioSource AS;
    bool onSoundCooldown;

    // Start is called before the first frame update
    void Start()
    {
        AS = transform.GetComponent<AudioSource>();
        onSoundCooldown = false;

        //Initalize at 2000 marker
        flipped = false;
        BarScale = Bar.transform.localScale.y;
        IndicatorLocation = Indicator.transform.localPosition.y;
    }

    // Update is called once per frame
    void Update()
    {
        // Color returnColor = WarningColor.Warning2(60f, 160f, UIEditor.hudColor, Color.red, indicatedAirspeed);
        try{
            returnColor = WarningColor.Warning3(1000f, 5000f, UIEditor.hudColor, Color.red, Mathf.Abs(indicatedVerticalSpeed), Color.yellow, .5f); //0-1000 green | 1000 -> 2500 yellow | 2500 -> 4000 red
        

            VerticalSpeedText.GetComponent<Text>().color = returnColor;
            VSpeedHudBox.GetComponent<Image>().color = returnColor;
            Indicator.GetComponent<Image>().color = returnColor;
            Bar.GetComponent<Image>().color = returnColor;
            Arrow.GetComponent<Image>().color = returnColor;

            if (indicatedVerticalSpeed < -1100f && indicatedAlt < 1000)              //test if the speed is too fast when close to the ground
            {
                Color dangerColor = Color.red;

                KnotsBox.GetComponent<Image>().color = dangerColor;
                NumText.GetComponent<Text>().color = dangerColor;
            }

            if (!AS.isPlaying && !onSoundCooldown && indicatedVerticalSpeed >= 2500)
            {
                onSoundCooldown = true;
                //StartCoroutine(PlaySound());
            }
        }
        catch{

        }
    }

    public void UpdateVerticalSpeed(float newVerticalSpeed, float newAlt)
    {
        indicatedVerticalSpeed = newVerticalSpeed;

        indicatedAlt = newAlt;

        if (newVerticalSpeed >= 0)
        {
            if(flipped)
            {
                flipped = false;
                Indicator.transform.localScale = new Vector3(Indicator.transform.localScale.x, Indicator.transform.localScale.y * -1, Indicator.transform.localScale.z);
            }
            if (newVerticalSpeed < 2000)
            {
                BarScaleMult = newVerticalSpeed / 2000f;
                IndicatorMult = newVerticalSpeed / 2000f;

            }
            else if(newVerticalSpeed < 6000)
            {
                BarScaleMult = ((newVerticalSpeed - 2000f) / 10800f) + 1; //Will place at 1.37 scale at 6000ft.
                IndicatorMult = ((newVerticalSpeed - 2000f) / 10800f) + 1; //Will place at 1.37 scale at 6000ft.
            }
            else
            {
                BarScaleMult = 1.38f;
                IndicatorMult = 1.38f; //Limit positive value at 6000 feet.

            }
        }
        else
        {
            if (!flipped)
            {
                flipped = true;
                Indicator.transform.localScale = new Vector3(Indicator.transform.localScale.x, Indicator.transform.localScale.y * -1, Indicator.transform.localScale.z);
            }
            if (newVerticalSpeed > -2000)
            {
                BarScaleMult = newVerticalSpeed / 2000f;
                IndicatorMult = newVerticalSpeed / 2000f;
            }
            else if(newVerticalSpeed > - 6000)
            {
                BarScaleMult = ((newVerticalSpeed + 2000f) / 10800f) - 1; //Will place at 1.37 scale at 6000ft.
                IndicatorMult = ((newVerticalSpeed + 2000f) / 10800f) - 1; //Will place at 1.37 scale at 6000ft.
            }
            else
            {
                BarScaleMult = -1.38f;
                IndicatorMult = -1.38f; //Limit negative value at 6000 feet.
            }
        }
        Indicator.transform.localPosition = new Vector3(Indicator.transform.localPosition.x, IndicatorLocation*IndicatorMult);
        Bar.transform.localScale = new Vector3(Bar.transform.localScale.x, BarScale*BarScaleMult);
    }
    public IEnumerator PlaySound()
    {
        Debug.Log("Playing Sound");

        if (!AS.isPlaying)
        {
            Debug.Log("Check 2");
            AS.clip = AClip;
            AS.Play();

        }
        yield return new WaitForSeconds(1f);
        onSoundCooldown = false;
    }

}
