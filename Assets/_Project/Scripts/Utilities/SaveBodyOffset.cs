using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class SaveBodyOffset : MonoBehaviour
{
    // Start is called before the first frame update
    bool coRoutRun = false; //boolean to check if coroutine is running
    int timeToSave = 4; //seconds of idleness until offset is saved

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    void Start()
    {
        //Check for x,y, and z position keys and loads them into BodyOffset transform if they exist
        if(PlayerPrefs.HasKey("xPos") && PlayerPrefs.HasKey("yPos") && PlayerPrefs.HasKey("zPos"))
        {
            transform.localPosition = new Vector3(PlayerPrefs.GetFloat("xPos"),
                                                  PlayerPrefs.GetFloat("yPos"),
                                                  PlayerPrefs.GetFloat("zPos"));
        }
        //Check for x,y,z, and w rotation keys and loads them into BodyOffset transform if they exist
        if (PlayerPrefs.HasKey("xRot") && PlayerPrefs.HasKey("yRot") && PlayerPrefs.HasKey("zRot") && PlayerPrefs.HasKey("wRot"))
        {
            transform.localRotation = new Quaternion(PlayerPrefs.GetFloat("xRot"),
                                                     PlayerPrefs.GetFloat("yRot"),
                                                     PlayerPrefs.GetFloat("zRot"),
                                                     PlayerPrefs.GetFloat("wRot"));
        }
    }

    // Update is called once per frame
    void Update()
    {
        //if transform has changed (since last save) and a coroutine for checking transform isnt running -> start coroutine
        if(transform.hasChanged && !coRoutRun)
        {
            //transform.hasChanged = false;
            StartCoroutine("CheckTransformChange");
        }
    }

    IEnumerator CheckTransformChange() //save values if constant for 5 seconds
    {
        coRoutRun = true; //coroutine is running
        int count = timeToSave;
        transform.hasChanged = false;
        while(count >= 0)
        {
            count--;
            yield return new WaitForSeconds(1f);
            if(transform.hasChanged) //if transform changes, reset coroutine loop
            {
                count = timeToSave; //restart save counter
                transform.hasChanged = false; 
            }
            if(count == 0) //BodyOffset has remained constant for timeToSave seconds
            {
                //Save Transform Data
                PlayerPrefs.SetFloat("xPos", transform.localPosition.x);
                PlayerPrefs.SetFloat("yPos", transform.localPosition.y);
                PlayerPrefs.SetFloat("zPos", transform.localPosition.z);
                PlayerPrefs.SetFloat("xRot", transform.localRotation.x);
                PlayerPrefs.SetFloat("yRot", transform.localRotation.y);
                PlayerPrefs.SetFloat("zRot", transform.localRotation.z);
                PlayerPrefs.SetFloat("wRot", transform.localRotation.w);
                PlayerPrefs.Save(); //Save PlayerPrefs
            }
        }
        if (transform.hasChanged)
            transform.hasChanged = false;
        coRoutRun = false;
    }

    public void ResetTransform() //Reset BodyOffset to 0
    {
        PlayerPrefs.SetFloat("xPos", 0);
        PlayerPrefs.SetFloat("yPos", 0);
        PlayerPrefs.SetFloat("zPos", 0);
        PlayerPrefs.SetFloat("xRot", 0);
        PlayerPrefs.SetFloat("yRot", 0);
        PlayerPrefs.SetFloat("zRot", 0);
        PlayerPrefs.SetFloat("wRot", 0);
        transform.localPosition = new Vector3(PlayerPrefs.GetFloat("xPos"),
                                              PlayerPrefs.GetFloat("yPos"),
                                              PlayerPrefs.GetFloat("zPos"));
        transform.localRotation = new Quaternion(PlayerPrefs.GetFloat("xRot"),
                                                 PlayerPrefs.GetFloat("yRot"),
                                                 PlayerPrefs.GetFloat("zRot"),
                                                 PlayerPrefs.GetFloat("wRot"));
    }
}
