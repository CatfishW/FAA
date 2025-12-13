using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Indicator : MonoBehaviour
{
    public Text uiText; // Reference to the UI Text component
    public GameObject Button;
    public string fullText; // The full text to display
    public float delay = 0.1f; // Delay between each character
    public float timer_for_display = 10.0f; // Time for the text to display
    private float timer = 0.0f;
    private string currentText = "";
    public GameObject Target2BActivated;

    void Start()
    {
        //uiText = this.GetComponent<Text>();
    }
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= timer_for_display)
        {
            if(Target2BActivated != null && Target2BActivated.activeSelf == false)
            {
                Target2BActivated.SetActive(true);
            }
            StartCoroutine(ShowText());
            Button.SetActive(true);
            timer = 0f;
        }
    }

    IEnumerator ShowText()
    {
        for (int i = 0; i <= fullText.Length; i++)
        {
            currentText = fullText.Substring(0, i);
            uiText.text = currentText;
            yield return new WaitForSeconds(delay);
        }
    }
}