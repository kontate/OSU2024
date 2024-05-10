using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetColor : MonoBehaviour
{
    private void OnEnable()
    {
        this.GetComponent<Renderer>().material.color = new Color(1f,0f,1f,0.8f);
    }
}
