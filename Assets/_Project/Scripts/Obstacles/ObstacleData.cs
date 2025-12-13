using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
[CreateAssetMenu(fileName = "ObstacleData", menuName = "ScriptableObjects/ObstacleData", order = 1)]
public class ObstacleData : ScriptableObject
{
    public List<float> Latitudes = new List<float>();
    public List<float> Longitudes = new List<float>();
    public List<float> Altitudes = new List<float>();
    public List<string> Types = new List<string>();

    public Stopwatch timer;

    public void ReadCSV() //Call function in editor to read CSV and update obstacle data. Will not run during scene
    {
        timer = new Stopwatch();
        timer.Start();

        Latitudes.Clear();
        Longitudes.Clear();
        Types.Clear();
        Altitudes.Clear();
        var path = @"C:\Users\grant\Documents\College\Grad\HoloHUD2\Assets\Scripts\ObstacleController\DOF.csv";
        string fileData = System.IO.File.ReadAllText(path);
        string[] lines = fileData.Split('\n'); //Splits data by line. Each line is seperate index
        int objTrack = new int();
        for (int i = 1; i < lines.Length; i++) //Loop the list of lines. Starting @ 1 to skip the header
            {

            string[] singleLineData = (lines[i].Trim()).Split(','); //Seperates lines by commas

           if (singleLineData.Length > 1)
            {
                    Latitudes.Add(float.Parse(singleLineData[5]));
                    Longitudes.Add(float.Parse(singleLineData[6]));
                    Altitudes.Add(float.Parse(singleLineData[11]));
                    Types.Add(singleLineData[9].Trim()); //Types have extra whitespace
                objTrack++;
            }
        

        }

        timer.Stop();
        UnityEngine.Debug.Log("TIME ELAPSED: " + timer.Elapsed);
        UnityEngine.Debug.Log("NO OBJ: " + objTrack);

         //Splits each line of data by commas

        
   
    }

}
