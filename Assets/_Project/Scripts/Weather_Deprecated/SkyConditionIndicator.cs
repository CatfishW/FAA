using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class SkyConditionIndicator : MonoBehaviour
{
    public Text dataText;
    // Add more GameObjects as needed
    public TMP_Text skyConditionText;
    public TMP_Text Cloud_CeilingText;

    void Update()
    {
        // Deactivate all sky condition GameObjects
        // Deactivate additional GameObjects as needed
        try{
        string jsonData = dataText.text;
        WeatherData skyData = JsonUtility.FromJson<WeatherData>(jsonData);
        string skyCondition = skyData.SkyCondition;
        string cloudCeiling = skyData.Ceiling.ToString();
        skyConditionText.text = skyCondition;
        Cloud_CeilingText.text = cloudCeiling;
        }
        catch{
            
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