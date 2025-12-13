using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayTestSound : MonoBehaviour
{
    public AudioSource ASource;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PlaySound()
    {
        ASource.Play();
    }
}
