using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Net;

// For Compressor
using System.IO;
using System.Text;

// For KinectManager
using com.rfilkov.kinect;

[RequireComponent(typeof(AudioSource))]
public class mic_websocket : MonoBehaviour
{
  // variables for websocket 
  public string serverAddr = "172.21.55.226";
  public int port = 3334;
  [Tooltip("Set true if microphone client, otherwise set false")]
  public bool is_mic_client = false;
  [Tooltip("Compressed binary data is used via WebSocket or not")]
  public bool is_compressed = false;
  [Tooltip("Test sound of 440Hz when no microphone client")]
  public bool test_sound = false;
  
  WebSocket ws;
//  private int c=0;
  private float mytime = 0.0f;

  // variables for generating sound
  private double frequency = 440;
	private double gain = 0.05;
	private double increment;
	private double phase;
	private double sampling_frequency = 24000;// You need to modify Audio sampling rate to 24kHz
	private bool playing = false;
  

  // variables for microphone
  private AudioSource ads;
//  private float[] adsdata = new float[2048];
  private byte[] adsbyte;
//  private List<byte[]> adslist; 
//  private List<byte> adslist; 
  private const int audio_bufsize = 2048;
  private const int max_buffers = 50;
//  private byte[,] adslist = new byte[max_buffers, audio_bufsize * 2]; 
  private byte[][] adslist = new byte[max_buffers][]; 
  private int ads_first = 0;// first index of adslist
  private int ads_last = 0; // next of the last index of adslist
                            // if ads_first=ads_last, adslist is empty
  private int adslist_waitflag = 0;// 0:wait until 25, 1:normal
  private float lasttime = 0.0f;
  private float bpssum = 0.0f;

  private KinectManager kinectManager = null;
  private float fStartTime = 0f;
  private float fCurrentTime = 0f;

  
//  [Serializable]
  public class Item
  {
    public string type;
    public string payload;
    public byte[] binary;
  }

  static Item item = new Item();
  static Item itemBody = new Item();
  int add_adspointer(int old, int a)
  {
    int ret = old + a;
    if(ret>=max_buffers) ret -= max_buffers;
    return ret;
  }

  int numdata_adspointer(int first, int last)
  {
    int ret = last - first;
    if(ret<0) ret += max_buffers;
    return ret;
  }
  void Start()
  {
    if (!kinectManager){
      kinectManager = KinectManager.Instance;
    }

    // allocate buffer
    for(int i=0; i<max_buffers; i++){
      adslist[i] = new byte[audio_bufsize*2];
    }

    //        ws = new WebSocket("ws://192.168.0.208:8888/ws");
    //    ws = new WebSocket("ws://localhost:8888/ws");
    Debug.Log("Server=" + serverAddr + " port=" + port);
    ws = new WebSocket("ws://" + serverAddr + ":" + port + "/ws");
    
    ws.OnOpen += (sender, e) =>
      {
       Debug.Log("WebSocket Open");
      };
    
    ws.OnMessage += (sender, e) =>
      {
//       Debug.Log("WebSocket Message Type: " + e.Type + ", Data: " + e.Data);
       Item it = JsonUtility.FromJson<Item>(e.Data);
       if(it.type=="echobinary" || it.type=="broadcastbinary"){
         if(is_compressed) adsbyte = Compressor.Decompress(it.binary);
         else              adsbyte = it.binary;
         
         if(adsbyte.Length != audio_bufsize * 2){
           Debug.Log("** error : adsbyte.Length and audio_bufsize is not consistent **");
           Application.Quit();
         }
//         Debug.Log("Going to add adsbyte");
         for(int i=0; i<adsbyte.Length; i++) adslist[ads_last][i] = adsbyte[i];
         ads_last = add_adspointer(ads_last, 1);
//         Debug.Log("ads_first = " + ads_first + " ads_last = " + ads_last + " numdata = " + numdata_adspointer(ads_first, ads_last));
         if(numdata_adspointer(ads_first, ads_last)>=38){
           ads_first = add_adspointer(ads_first, 13); // reduce the number of data to 25 instead of 38
           Debug.Log("Reduced adslist to " + numdata_adspointer(ads_first, ads_last));
         }
         bpssum += it.binary.Length * 8;
//         Debug.Log(" Json: type="+it.type+" payload="+it.payload+" binary["+it.binary.Length+"] adsbyte["+adsbyte.Length+"]");         
       } else if(it.type=="echo" || it.type=="broadcast"){
//         Debug.Log("  Json: type="+it.type+" payload="+it.payload);
       } else if(it.type=="broadcastResult"){
//         Debug.Log("  Json: type="+it.type+" payload="+it.payload);
       } else {
         Debug.Log("  Json: type="+it.type+" payload="+it.payload);
       }
      };
    
    ws.OnError += (sender, e) =>
      {
       Debug.Log("WebSocket Error Message: " + e.Message);
      };
    
    ws.OnClose += (sender, e) =>
      {
       Debug.Log("WebSocket Close");
      };
    
    ws.Connect();
    Debug.Log(ws);

#if !UNITY_WEBGL
    if(is_mic_client){
      // Initialize Microphone
      ads = GetComponent<AudioSource>();
      ads.clip = Microphone.Start(null, true, 1, 48000);
      ads.loop = true;
      while (Microphone.GetPosition(null) <= 0) {}
      ads.Play();
    }
#endif
  }
  
  void Update()
  {
    mytime = Time.time;
    if(is_mic_client){
//      liRelTime = kinectManager.GetBodyFrameTimestamp();
      string sBodyFrame = kinectManager.GetBodyFrameData(ref fCurrentTime, ';');
      System.Globalization.CultureInfo invCulture = System.Globalization.CultureInfo.InvariantCulture;
      if (sBodyFrame.Length > 0){
        string sRelTime = string.Format(invCulture, "{0:F3}", (fCurrentTime - fStartTime));
        string oneBodyData = sRelTime + "|" + sBodyFrame;
        itemBody.type = "broadcast";
        itemBody.payload = oneBodyData;
        ws.Send(JsonUtility.ToJson(itemBody));
//        Debug.Log(oneBodyData);
      }
    } else {
      if(mytime - lasttime >= 10.0f){
        Debug.Log("Average Mbps = " + bpssum/1000000.0f/10.0f);
        lasttime = mytime;
        bpssum = 0.0f;
      }
    }

    /*
    string itemJson = "{ \"type\": \"broadcast\", \"payload\": \"broadcast string=" + c +"\"}";
    if(c % 100==0){
      float[] fa = {1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f};
      byte[] b = new byte[4];
      int i, j;     
      item.type = "broadcastbinary";
      Array.Resize(ref item.binary, fa.Length * 4);
      for(i=0; i<fa.Length; i++){
        b = BitConverter.GetBytes(fa[i]);
        for(j=0; j<4; j++){
          item.binary[i*4+j] = b[j];
        }
      }
      ws.Send(JsonUtility.ToJson(item));
    }
    c++;
    */
  }
  
  void OnDestroy()
  {
    ws.Close();
    ws = null;
  }

  void OnAudioFilterRead (float[] data, int channels)
	{
    int i,j;

//        Debug.Log("size="+data.Length+" channel="+channels);

    // get mic data and send it to other clients
    if(is_mic_client && playing){
//      ads.GetOutputData(adsdata, 2);
      item.type = "broadcastbinary";
      item.payload = mytime.ToString();
      byte[] b = new byte[2];
      byte[] btmp = new byte[data.Length * 2];
      for(i=0; i<data.Length; i++){
        b = BitConverter.GetBytes((short)(data[i]*32767));
        for(j=0; j<2; j++) btmp[i*2+j] = b[j];
      }
      /*
      byte[] b = new byte[4];
      byte[] btmp = new byte[data.Length * 4];
//      Array.Resize(ref item.binary, data.Length * 4);
      for(i=0; i<data.Length; i++){
        b = BitConverter.GetBytes(data[i]);
        for(j=0; j<4; j++){
//          item.binary[i*4+j] = b[j];
          btmp[i*4+j] = b[j];
        }
      }
      */
      if(is_compressed) item.binary = Compressor.Compress(btmp);
      else              item.binary = btmp;
      ws.Send(JsonUtility.ToJson(item));
    }

    if(!is_mic_client){
      // Check the size of arrays
      if(adsbyte == null || adsbyte.Length == 0){
        if(test_sound){
		      // update increment in case frequency has changed
		      increment = frequency * 2 * Math.PI / sampling_frequency;
		      for (i = 0; i < data.Length; i = i + channels) {
			      phase = phase + increment;
    			  data [i] = (float)(gain * Math.Sin (phase));
			      // if we have stereo, we copy the mono data to each channel
			      if (channels == 2) data [i + 1] = data [i];
			      if (phase > 2 * Math.PI) phase = 0;
		      }
        } else {
          for(i=0; i<data.Length; i++) data[i] = 0.0f;
        }
        return;
      }
//      if(data.Length*4 != adsbyte.Length){
      if(data.Length*2 != adsbyte.Length){
        Debug.Log("** error : data size is not consistent **");
        Application.Quit();
      }

      // buffering
      if(adslist_waitflag == 1){// normal satte
        if(numdata_adspointer(ads_first, ads_last)>=13){
          for(i=0; i<data.Length; i++){
            data[i] = (float)(BitConverter.ToInt16(adslist[ads_first], i*2) / 32767.0f);
          }
          ads_first = add_adspointer(ads_first, 1);
        } else {
          adslist_waitflag = 0;
        }
      }
      if(adslist_waitflag == 0){// buffering state
//        Debug.Log("number of buffers is " + numdata_adspointer(ads_first, ads_last));
        for(i=0; i<data.Length; i++) data[i]=0.0f;
        if(numdata_adspointer(ads_first, ads_last)>=25) adslist_waitflag = 1;
      }

/*
      // copy mic data to filtered array
      for(i=0; i<data.Length; i++){
 //       data[i] = BitConverter.ToSingle(adsbyte, i*4);
        data[i] = (float)(BitConverter.ToInt16(adsbyte, i*2) / 32767.0f);
      }
*/
    }
	}

	void OnGUI ()
	{
    if(is_mic_client){
		  int y = 10;
		  if (GUI.Button (new Rect (10, y, 100, 30), "Play")) {
			  playing = true;
		  }
		  y += 40;
		  if (GUI.Button (new Rect (10, y, 100, 30), "Stop")) {
			  playing = false;
		  }
    }
	}
}
