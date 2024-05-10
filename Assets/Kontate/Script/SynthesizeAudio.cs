using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class SynthesizeAudio : MonoBehaviour
{
    private double frequency;
    public double gain = 0.02;
    private double increment;
    private double phase;
    private double sampling_frequency;


    [SerializeField] private Text text;
    [SerializeField] private Transform transform;
    [SerializeField] private float offset, multi;

    private void Start()
    {
        sampling_frequency = AudioSettings.outputSampleRate;
        frequency = 440.0;
    }

    private void Update()
    {
        frequency = (Math.Pow(transform.position.y, 3) * multi + offset);
        increment = frequency * 2 * Math.PI / sampling_frequency;
        text.text = Math.Floor(frequency).ToString() + "Hz （" + GetNotes(frequency) + "）";
    }

    List<string> noteChar = new List<string>()
        {"ラ", "ラ＃", "シ", "ド", "ド＃", "レ", "レ＃", "ミ", "ファ", "ファ＃", "ソ", "ソ＃"};
    
    private string GetNotes(double f)
    {
        return noteChar[(int)Math.Floor(Math.Log(frequency / 440.0f) * 12 * (1 / Math.Log(2.0f))) % 12];
    }

void OnAudioFilterRead(float[] data, int channels)
    {
        for (var i = 0; i < data.Length; i = i + channels)
        {
            phase = phase + increment;
            data[i] = (float)(gain*Math.Sin(phase));
            if (channels == 2) data[i + 1] = data[i];
            if (phase > 2 * Math.PI) phase = 0;
        }
    }
}
