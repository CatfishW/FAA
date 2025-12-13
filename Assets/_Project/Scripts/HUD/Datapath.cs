using System;
using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.UI;
public class Datapath : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        String ValidIP = "";
        bool found = false;
        String PersistentPath = Application.persistentDataPath;
        DirectoryInfo dir = new DirectoryInfo(Application.persistentDataPath);
        FileInfo[] info = dir.GetFiles("*.*");
        foreach (FileInfo f in info)
        {
            String[] FilePath = f.ToString().Split('\\');
            String FileName = FilePath[FilePath.Length - 1]; //Get just end of file path.
            //Debug.Log(FileName);

            if (FileName.Split('.').Length == 4)//Valid IPs have 3 periods in it.
            {
                found = true;
                ValidIP = FileName;
            }
        }
        StartCoroutine("displayDataPath");
        if(found)
        {
            //Debug.Log("IP Detected! IP is " + ValidIP);
            this.GetComponent<Text>().text = ValidIP;
        }
        else
        {
            this.GetComponent<Text>().text = "192.168.72.249";
            //Debug.Log("No valid IP detected in persistent data path.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator displayDataPath()
    {
        yield return new WaitForSeconds(5f);
        Destroy(this.gameObject);
    }
}
