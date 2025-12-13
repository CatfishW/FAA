using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using System.IO;

public class DebugUpdate : MonoBehaviour
{
    [SerializeField]
    public string fileName;
    public string path;
    public string date;
    public string time;
    private StreamWriter sw;
    // Start is called before the first frame update
    void Awake()
    {
        fileName = "/log";
        date = System.DateTime.Now.ToString("MM_dd_hh_mm_ss");
        string fullFile = fileName + date + ".txt"; 
        path = @Application.persistentDataPath + fullFile;
        sw = File.CreateText(path);
        sw.Write("Log: \n \n");
        
        Application.logMessageReceived += LogMessage;

    }

    // Update is called once per frame
    void Update()
    {

    }

    void WriteString(string inputString)
    {
        sw.WriteLine(inputString);
        sw.Flush();
    }

    void LogMessage(string logString, string stackTrace, LogType type)
    {
        string combine = logString + stackTrace + type + "\n";
        WriteString(combine);
    }



    public IEnumerator BeginCooldown()
    {
        yield return new WaitForSeconds(0.5f);
    }

    public IEnumerator HideDebug()
    {
        yield return new WaitForSeconds(5f);
        this.gameObject.GetComponent<Text>().enabled = false;
    }
}
