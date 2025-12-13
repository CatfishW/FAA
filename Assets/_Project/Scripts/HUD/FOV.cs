using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FOV : MonoBehaviour
{
    // Start is called before the first frame update
    public bool isHidden;
    private Vector3 originalScale;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
    }
    void Start()
    {
        originalScale = new Vector3(transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void IncreaseFOV()
    {
        this.transform.GetComponent<RectTransform>().sizeDelta =
        new Vector2(this.transform.GetComponent<RectTransform>().rect.width + 1.00f, this.transform.GetComponent<RectTransform>().rect.height + 1.00f);
        Debug.Log("Increasing FOV");
    }
    public void DecreaseFOV()
    {
        if (this.transform.GetComponent<RectTransform>().rect.width > 1)
        {
            this.transform.GetComponent<RectTransform>().sizeDelta =
            new Vector2(this.transform.GetComponent<RectTransform>().rect.width - 1.00f, this.transform.GetComponent<RectTransform>().rect.height - 1.00f);
            Debug.Log("Decreasing FOV");
        }
    }

    public void Hide()
    {
        if(isHidden == true)
        {
            this.transform.localScale = originalScale;
            isHidden = false;
        }
        else
        {
            this.transform.localScale = new Vector3(0f, 0f, 0f);
            isHidden = true;
        }
    }
}
