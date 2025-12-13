using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DewPointIndicator : MonoBehaviour
{
    public TMP_Text dewPointText;
    public Text dataText;

    void Update()
    {
        try{
            string jsonData = dataText.text;
            WeatherData weatherData = JsonUtility.FromJson<WeatherData>(jsonData);
            float dewPoint = weatherData.DewPoint;
            dewPointText.text = $"{dewPoint}°C";
        }
        catch{
            //Debug.Log("Dew Point Error, mqtt not connected?");
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