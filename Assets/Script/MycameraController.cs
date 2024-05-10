using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.components;

public class MycameraController : MonoBehaviour {
    [Tooltip("On if the second display is portrait")]
    public bool secondDispPortrait = false;

    [Tooltip("Aspect ratio for preventing 3Mitsu. 1 is default and 2 is wider separation between avatars")]
	public float scaleHorizontal = 1.0f;

    [Tooltip("Distance between avatars to warn 3Mistu. If 0 no messages are displayed")]
	public float mitsudesu = 1.5f;
    [Tooltip("Seconds bofore disappear Mistudesu message")]
	public float mitsudesuTimer = 1.0f;
    [Tooltip("Object when mitsudesu happened")]
	[SerializeField] public GameObject mitsudesuObj;

	public GameObject[] Characters;
	private Camera cam;
	private const int maxchar = 30;
	private bool[,] mitsuflag = new bool[maxchar, maxchar];
	private GameObject[,] mitsuobj = new GameObject[maxchar, maxchar];

	// Use this for initialization
	void Start () {
		if(Characters.Length > maxchar){
			Debug.LogError("Number of Characters is too large");
			Application.ForceCrash(0);
		}

	    for(int i=0; i<Characters.Length; i++){
			for(int j=0; j<Characters.Length; j++){
				mitsuflag[i,j] = false;
				mitsuobj[i,j]  = null;
			}
		}

		if (secondDispPortrait == true && Display.displays.Length > 1) Display.displays[1].Activate(1080,1920,60);
		cam = GetComponent<Camera>();
		cam.projectionMatrix *= Matrix4x4.Scale(new Vector3(1.0f / scaleHorizontal, 1, 1));	
		for(int i=0; i<Characters.Length; i++){
			Vector3 lscale = Characters[i].transform.localScale;
			Characters[i].transform.localScale = new Vector3(lscale.x * scaleHorizontal, lscale.y, lscale.z);
		}
	}
	
	// Update is called once per frame
	void Update () {
		// Check 3Mistu
		if(mitsudesu>0.0f){
		    for(int i=0; i<Characters.Length; i++){
				Vector3 posi = Characters[i].transform.position;
				for(int j=0; j<Characters.Length; j++){
					mitsuflag[i,j] = false;
					Vector3 posj = Characters[j].transform.position;
				    if(i!=j && posi.z > -5.0f && posj.z > -5.0f && mitsuobj[i,j]==null){
						posj -= posi;
						if(posj.magnitude < mitsudesu){
							mitsuflag[i,j] = true;
							mitsuobj[i,j] = GameObject.Instantiate(mitsudesuObj);
							mitsuobj[i,j].transform.position 
								= new Vector3((Characters[i].transform.position.x + Characters[j].transform.position.x) * 0.5f,
											1.2f,
											(Characters[i].transform.position.z + Characters[j].transform.position.z) * 0.5f + 1.0f);
							Destroy(mitsuobj[i,j], mitsudesuTimer);
							Debug.Log("Avater "+i+" and "+j+" are mitsudesu: "+posj.magnitude);
						}
					} else if(mitsuobj[i,j]!=null){
//						Debug.Log("object seems active, i=" + i + " j=" + j);
					}
				}
			}
		}

		// Flip right-left for avaters
		if (Input.GetKey(KeyCode.R))
		{
			for(int i=0; i<Characters.Length; i++)
            {
				if(Characters[i].GetComponent<AvatarController>().flipLeftRight == false)
					Characters[i].GetComponent<AvatarController>().flipLeftRight = true;
				else
					Characters[i].GetComponent<AvatarController>().flipLeftRight = false;
			}
		}
		// Zoom avaters
		if (Input.GetKey(KeyCode.B)){
			for(int i=0; i<Characters.Length; i++) Characters[i].transform.localScale *= 1.03f;
		}
		if (Input.GetKey(KeyCode.N)){
			for(int i=0; i<Characters.Length; i++) Characters[i].transform.localScale = new Vector3(1.0f,1.0f,1.0f);
		}
		if (Input.GetKey(KeyCode.M)){
			for(int i=0; i<Characters.Length; i++) Characters[i].transform.localScale /= 1.03f;
		}

		// Camera movement
		if (Input.GetKey(KeyCode.W)){
			transform.position += new Vector3(0,0,-1* Time.deltaTime);
		}
		if(Input.GetKey(KeyCode.S)){
			transform.position += new Vector3(0,0, 1* Time.deltaTime);
		}
		if(Input.GetKey(KeyCode.A)){
			transform.position += new Vector3( 0.25f* Time.deltaTime, 0, 0);
		}
		if(Input.GetKey(KeyCode.D)){
			transform.position += new Vector3(-0.25f* Time.deltaTime, 0, 0);
		}
		if(Input.GetKey(KeyCode.Z)){
			transform.position += new Vector3(0, 0.25f* Time.deltaTime, 0);
		}
		if(Input.GetKey(KeyCode.X)){
			transform.position += new Vector3(0, -0.25f* Time.deltaTime, 0);
		}
		if(Input.GetKey(KeyCode.T)){
			transform.Rotate(-5.0f * Time.deltaTime, 0, 0);
		}
		if(Input.GetKey(KeyCode.G)){
			transform.Rotate( 5.0f * Time.deltaTime, 0, 0);
		}
		if(Input.GetKey(KeyCode.F)){
			float view = cam.fieldOfView + 0.25f;
			cam.fieldOfView = Mathf.Clamp(value: view, min:0.1f, max: 45f);
		}
		if(Input.GetKey(KeyCode.H)){
			float view = cam.fieldOfView - 0.25f;
			cam.fieldOfView = Mathf.Clamp(value: view, min:5f, max: 85f);
		}
	}
}
