using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class manualIP : MonoBehaviour
{
    bool onVar = false;
    string ipStr = "";
    private InputField myInputField;
    public Text IPEnterText;
    public string path;
    public string currentIP = "";
    private StreamWriter ipFileWriter;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    // Start is called before the first frame update
    void Start()
    {
        myInputField = transform.GetComponent<InputField>();
        myInputField.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f); //update scale
        IPEnterText.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void UpdateIPInfo()
    {
        onVar = !onVar;
        if (onVar)
        {
            //change scale to enable
            myInputField.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            IPEnterText.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            IPEnterText.text = "Enter IP:";
            myInputField.ActivateInputField();

        }
        else
        {
            //change scale to disable
            myInputField.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f); //update scale
            IPEnterText.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f);
            myInputField.DeactivateInputField();
            ipStr = "/" + myInputField.text.ToString();
            if (CheckValidIP(ipStr) && CheckPrevIP(ipStr))//currentIP != ipStr)
            {
                ClearOldIP();


                Debug.Log("New IP Entered: " + ipStr);
                path = @Application.persistentDataPath + ipStr;
                ipFileWriter = File.CreateText(path);
            }
            else
                Debug.Log("IP address, " + ipStr + " is invalid or the current IP already.");
        }
    }

    public bool CheckValidIP(string ipStr)
    {
        if (ipStr.Split('.').Length == 4) //Valid IPs have 4 periods in it.
            return true;
        return false;
    }

    public void ClearOldIP() //returns true of file was deleted
    {
        string PersistentPath = Application.persistentDataPath;
        DirectoryInfo dir = new DirectoryInfo(Application.persistentDataPath);
        FileInfo[] info = dir.GetFiles("*.*");

        foreach (FileInfo f in info)
        {
            string[] FilePath = f.ToString().Split('\\');
            string FileName = FilePath[FilePath.Length - 1]; //Get just end of file path.
            if (CheckValidIP(FileName))
            {
                File.Delete(f.ToString());
            }
        }
    }

    public bool CheckPrevIP(string ipStr)
    {
        string PersistentPath = Application.persistentDataPath;
        DirectoryInfo dir = new DirectoryInfo(Application.persistentDataPath);
        FileInfo[] info = dir.GetFiles("*.*");

        foreach (FileInfo f in info)
        {
            string[] FilePath = f.ToString().Split('\\');
            string FileName = FilePath[FilePath.Length - 1]; //Get just end of file path.
            if (FileName.Equals(ipStr))
            {
                Debug.Log("IP: " + ipStr + "Already active");
                return false;
            }
        }
        return true;
    }
}
