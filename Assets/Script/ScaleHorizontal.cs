using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleHorizontal : MonoBehaviour {
	private Camera _cam;

	// Use this for initialization
	void Start () {
		_cam = GetComponent<Camera>();
        		_cam.projectionMatrix *= Matrix4x4.Scale(new Vector3(0.5f, 1, 1));	
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
