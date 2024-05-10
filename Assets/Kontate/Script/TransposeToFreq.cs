using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class TransposeToFreq : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private SynthesizeAudio synthesizeAudio;
    [SerializeField] private float offset = 440;
    [SerializeField] private float mult = 1;
    private Transform transform;
    void Start()
    {
        if(!synthesizeAudio) Debug.LogError("No Synthesizer Object");

        transform = this.GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        //synthesizeAudio.SetFrequency(transform.position.y * mult + offset);
        Debug.Log(transform.position.y * mult + offset);
    }
}
