using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WindSpeedIndicator : MonoBehaviour
{
    public Text dataText;
    public GameObject wind_less_than_1;
    public GameObject wind23_27;
    public GameObject wind28_32;
    public GameObject wind33_37;
    public GameObject wind88_92;
    public GameObject wind93_97;
    public GameObject wind98_102;
    public GameObject wind103_107;
    public WindDirection windDirection;
    public GameObject helicopter;
    // Add more GameObjects as needed

    void Update()
    {
        try{
        // Deactivate all wind GameObjects
        wind_less_than_1.SetActive(false);
        wind23_27.SetActive(false);
        wind28_32.SetActive(false);
        wind33_37.SetActive(false);
        wind88_92.SetActive(false);
        wind93_97.SetActive(false);
        wind98_102.SetActive(false);
        wind103_107.SetActive(false);
        // Deactivate additional GameObjects as needed

        string jsonData = dataText.text;
        WeatherData windData = JsonUtility.FromJson<WeatherData>(jsonData);
        float heliHeading = helicopter.transform.eulerAngles.y;
        try{
            windDirection.UpdateWind(windData.WindDirection, heliHeading, windData.WindSpeed);
        }
        catch{
            //Debug.Log("Wind Direction Error, mqtt not connected?");
        }
        float windSpeed = windData.WindSpeed;
        if (windSpeed < 23)
        {
            wind_less_than_1.SetActive(true);
        }
        else if (windSpeed >= 23 && windSpeed <= 27)
        {
            wind23_27.SetActive(true);
        }
        else if (windSpeed >= 28 && windSpeed <= 32)
        {
            wind28_32.SetActive(true);
        }
        else if (windSpeed >= 33 && windSpeed <= 37)
        {
            wind33_37.SetActive(true);
        }
        else if (windSpeed >= 88 && windSpeed <= 92)
        {
            wind88_92.SetActive(true);
        }
        else if (windSpeed >= 93 && windSpeed <= 97)
        {
            wind93_97.SetActive(true);
        }
        else if (windSpeed >= 98 && windSpeed <= 102)
        {
            wind98_102.SetActive(true);
        }
        else if (windSpeed >= 103 && windSpeed <= 107)
        {
            wind103_107.SetActive(true);
        }
        // Add more conditions as needed
        }
        catch{
            //Debug.Log("Wind Speed Error, mqtt not connected?");
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