using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using DLLTest;
#if UNITY_ANDROID 
#elif UNITY_IOS
#else
using form2;
#endif
public class TestDLLTest : MonoBehaviour
{
    public GameObject[] Characters;

#if UNITY_ANDROID 
#elif UNITY_IOS
#else
    private int count = 0;
    // Start is called before the first frame update
//    private Class1 c1;
    private Test t1;

    void Start()
    {
//        c1 = new Class1();
//        c1.RegisterHandler();
//        t1 = new Test();
//        Test.Main_internal(); // work
        t1 = new Test();
        t1.Launch();
    }

    // Update is called once per frame
    void Update()
    {
        int vup = t1.isVolumeUp();
        int vdown = t1.isVolumeDown();
        int esc = t1.isEsc();
        int i, cnum = 0;
        if(esc == 1) Application.Quit();
        transform.position += new Vector3(0,vup*0.003f,0);
        transform.position += new Vector3(0,-vdown*0.003f,0);
        transform.Rotate(0,0,Time.deltaTime * 10.0f);

        if(vup == 0 && vdown == 0)      cnum = 0;
        else if(vup == 0 && vdown == 1) cnum = 1;
        else if(vup == 1 && vdown == 0) cnum = 2;
        else if(vup == 1 && vdown == 1) cnum = 3;
//        cnum = 4;
//        Debug.Log("cnum=" + cnum);
        for(i=0; i<Characters.Length; i++){
            if(i == cnum) Characters[i].SetActive(true);
            else          Characters[i].SetActive(false);
        }

        if (count % 100 == 0)
        {
            Debug.Log("isVolumeDown is " + vup);
            Debug.Log("isVolumeUp is " + vdown);
        }
        count++;

/*
        if (Input.GetKeyDown(KeyCode.Keypad8))
        {
            Debug.Log("Pad8 (VolumeUp) is down");
        } else if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            Debug.Log("Pad2 (VolumeDown) is down");
        } else if (Input.GetKeyDown(KeyCode.A)){
            Debug.Log("A is down");
        }
        if (Input.GetKeyUp(KeyCode.Keypad8))
        {
            Debug.Log("Pad8 (VolumeUp) is up");
        }
        else if (Input.GetKeyUp(KeyCode.Keypad2))
        {
            Debug.Log("Pad2 (VolumeDown) is up");
        }
        */
    }

    void OnApplicationQuit()
    {
//        c1.UnregisterHandler();
    }
#endif
}
