using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class VisibilityIndicator : MonoBehaviour
{
    public TMP_Text visibilityText;
    public Text dataText;

    void Update()
    {
        try{
        string jsonData = dataText.text;
        WeatherData windData = JsonUtility.FromJson<WeatherData>(jsonData);
        int visibility = windData.Visibility;
        visibilityText.text = $"{visibility}m";
        }
        catch{
            //Debug.Log("Visibility Error, mqtt not connected?");
        }
    }

    [System.Serializable]
    public class WeatherData
    {
        public float WindSpeed;
        public float WindDirection;
        public int Ceiling;
        public string SkyCondition;
        public int Visibility;
        public int OutsideAirTemperature;
        public float DewPoint;
        public float Latitude;
        public float Longitude;
        public int elevation;
        public string Time;
    }
}