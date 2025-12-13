using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateTest : MonoBehaviour
{
    public GameObject Rotatee;
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(SetRotateTester());
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public IEnumerator RotateTester()
    {
        for(int x = 0; x < 720; x++)
        {
            this.transform.Rotate(new Vector3(0.25f, 0f, 0f));
            Rotatee.transform.Rotate(new Vector3(0.25f, 0f, 0f));
            yield return null;
        }
        for (int y = 0; y < 720; y++)
        {
            this.transform.Rotate(new Vector3(0f, 0.25f, 0f));
            Rotatee.transform.Rotate(new Vector3(0f, 0.25f, 0f));
            yield return null;
        }
        for (int z = 0; z < 720; z++)
        {
            this.transform.Rotate(new Vector3(0f, 0f, 0.25f));
            Rotatee.transform.Rotate(new Vector3(0f, 0f, 0.25f));
            yield return null;
        }
    }

    public IEnumerator SetRotateTester()
    {
        this.transform.rotation = Quaternion.Euler(new Vector3(0f, 30f, 0f));
        Rotatee.transform.rotation = Quaternion.Euler(new Vector3(0f, 30f, 0f));
        yield return new WaitForSeconds(3f);

        this.transform.rotation = Quaternion.Euler(new Vector3(70f, 30f, 0f));
        Rotatee.transform.rotation = Quaternion.Euler(new Vector3(70f, 30f, 0f));
        yield return new WaitForSeconds(3f);

        this.transform.rotation = Quaternion.Euler(new Vector3(12f, 30f, 57f));
        Rotatee.transform.rotation = Quaternion.Euler(new Vector3(12f, 30f, 57f));
        yield return new WaitForSeconds(3f);

    }
}
