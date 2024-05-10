using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Net;

// For task
using System.Threading;
using System.Threading.Tasks;

// For Compressor
using System.IO;
using System.Text;

// For KinectManager
using com.rfilkov.kinect;

// For Concurrent Queue
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class K4A_mic_websocket : MonoBehaviour
{
  public GameObject[] Characters;
  public GameObject[] OffCharacters;
  public GameObject   MyCharacter;
//  public GameObject   DebugOtherPerson;
  public float lifetime = 0.5f; // lifetime of other person's object
  // variables for websocket 
  public string serverAddr = "172.21.55.226";
  public int port = 3334;
  [Tooltip("Set true if microphone client, otherwise set false")]
  public bool is_mic_client = false;
  [Tooltip("Compressed binary data is used via WebSocket or not")]
  public bool is_compressed = false;
  [Tooltip("Test sound of 440Hz when no microphone client")]
  public bool test_sound = false;
  
  private WebSocket ws;
  private int wsstate = 0; // 0:not connected, 1:connected, 2:connecting
  private Task task;
//  private int c=0;
  private float mytime = 0.0f;

  // variables for generating sound
  private double frequency = 440;
	private double gain = 0.05;
	private double increment;
	private double phase;
	private double sampling_frequency = 48000;// You need to modify Audio sampling rate to 24kHz
	private bool playing = false;
  

  // variables for microphone
  private AudioSource ads;
//  private float[] adsdata = new float[2048];
  private byte[] adsbyte;
//  private List<byte[]> adslist; 
//  private List<byte> adslist; 
  private const int audio_bufsize = 2048;
  private int read_bufsize = 0;
  private int read_channels = 0;
  private const int max_buffers = 150;
//  private byte[,] adslist = new byte[max_buffers, audio_bufsize * 2]; 
  private byte[][] adslist = new byte[max_buffers][]; 
  private int ads_first = 0;// first index of adslist
  private int ads_last = 0; // next of the last index of adslist
                            // if ads_first=ads_last, adslist is empty
  private int adslist_waitflag = 0;// 0:wait until 25, 1:normal
  private float lasttime = 0.0f;
  private float bpssum = 0.0f;
  public int data_reduction_mode = 2; // 0: No data reduction, 1: mic and body data are sampled with a half rate
                                      // 2: 1/4              , 3: 1/8
  private int frame = 0;
  private float waittime = 30.0f;

  private KinectManager kinectManager = null;
  private float fStartTime = 0f;
  private float fCurrentTime = 0f;
  private string bfd = null;
  private string[] carsdata;
  private byte[] bbjpeg = null; // black board jpeg data
  protected static K4A_mic_websocket instance = null;
  private bool[] isactive = new bool[100]; // active or inactive flag 
//  private Vector3 debug_person_pos = new Vector3(0.0f, 0.0f, 0.0f);
//  private Vector3 debug_person_rot = new Vector3(0.0f, 0.0f, 0.0f);
  public int maxcpsdata = 1000; 

  private ConcurrentQueue<Cpsdata> cpsqueue = new ConcurrentQueue<Cpsdata>();
  public class Cpsdata
  {
    public Vector3 pos;
    public Vector3 rot;
  } 

  
//  [Serializable]
  public class Item
  {
    public string type;
    public string payload;
    public byte[] binary;
  }

  static Item item = new Item();
  static Item itemBody = new Item();
  static Item itemBbjpeg = new Item();
  static Item itemCpos = new Item();

  public static K4A_mic_websocket Instance
  {
    get
    {
      return instance;
    }
  }

  public bool GetOtherpersonsPosRot(out Vector3 pos, out Vector3 rot){
    Cpsdata cpsone = new Cpsdata();
    if(cpsqueue.Count>0 && cpsqueue.TryDequeue(out cpsone)==true){
      pos = cpsone.pos;
      rot = cpsone.rot;
      return true;
    }
    pos = new Vector3(0.0f, 0.0f, 0.0f);
    rot = new Vector3(0.0f, 0.0f, 0.0f);
    return false;
  }
  public void SendMyPosition(Vector3 pos, Vector3 rot){
    itemCpos.type = "broadcast";
    itemCpos.payload = "cps " + pos.x + " " + pos.y + " " + pos.z + " " + rot.x + " " + rot.y + " " + rot.z;
    itemCpos.binary = null;
    if(wsstate==1){
      ws.Send(JsonUtility.ToJson(itemCpos));
//      Debug.Log("SendMyPosition is called and ws.Send is called, " + itemCpos.payload);
    }
  }
  public string[] GetCarsData(){
    return carsdata;
  }
  public void InitializeCarsData(){
    carsdata = new string[0];
  }

  public byte[] GetBbjpeg(){
    return bbjpeg;
  }

  public void RemoveBbjpeg(){
    bbjpeg = null;
  }

  public void SendBbjpeg(byte[] bb){
    itemBbjpeg.type = "broadcastbinary";
    itemBbjpeg.payload = "bbj0";
    itemBbjpeg.binary = bb;
    if(wsstate==1){
      ws.Send(JsonUtility.ToJson(itemBbjpeg));
      //Debug.Log("SendBbjpeg is called and ws.Send is called, size=" + bbjpeg.Length);
    }
  }

  public bool IsMicClient(){
    return is_mic_client;
  }
  void Awake()
  {
    // initializes the singleton instance of KinectManager
    if (instance == null)
    {
      instance = this;
      DontDestroyOnLoad(this);
    }
/*    else if (instance != this)
    {
      DestroyImmediate(gameObject);
      return;
    }*/
  }

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

  public int GetNumdata(){
    return numdata_adspointer(ads_first, ads_last);
  }
  WebSocket websocket_open(){
    //        ws = new WebSocket("ws://192.168.0.208:8888/ws");
    //    ws = new WebSocket("ws://localhost:8888/ws");
    Debug.Log("Server=" + serverAddr + " port=" + port);
    return new WebSocket("ws://" + serverAddr + ":" + port + "/ws");
  }
  void websocket_tryconnect(){
    wsstate=2;
    ws.Connect();
    if(wsstate==2){
      wsstate=1;
      Debug.Log("Websocket connection succeeded");
    }
  }
  void websocket_registerroutines(){
    ws.OnOpen += (sender, e) =>
      {
       Debug.Log("WebSocket Open");
      };
    
    ws.OnMessage += (sender, e) =>
      {
        // Debug.Log("WebSocket Message Type: " + e.Type + ", Data: " + e.Data);
        Item it = JsonUtility.FromJson<Item>(e.Data);
        if(it.type=="echobinary" || it.type=="broadcastbinary"){
          bpssum += it.binary.Length * 8;
          if(it.payload.Substring(0,3)=="mic" || it.payload.Substring(0,3)=="MIC"){ // mic data
            data_reduction_mode = Convert.ToInt32(it.payload.Substring(3,1));
            if(it.payload.Substring(0,3)=="mic") adsbyte = Compressor.Decompress(it.binary);
            else                                 adsbyte = it.binary;
         
            if(data_reduction_mode==0){
              if(adsbyte.Length != audio_bufsize * 2){
                Debug.LogError("** error : adsbyte.Length and audio_bufsize is not consistent ** 0 " + adsbyte.Length);
                Application.Quit();
              }
            } else if(data_reduction_mode==1){
              if(adsbyte.Length != audio_bufsize){
                Debug.LogError("** error : adsbyte.Length and audio_bufsize is not consistent ** 1 " + adsbyte.Length);
                Application.Quit();
              }
              Array.Resize(ref adsbyte, audio_bufsize * 2);
              for(int i=adsbyte.Length-8;i>=0;i-=8){
                adsbyte[i+7] = adsbyte[i+3] = adsbyte[i/2+3];// second channel
                adsbyte[i+6] = adsbyte[i+2] = adsbyte[i/2+2];
                adsbyte[i+5] = adsbyte[i+1] = adsbyte[i/2+1];// first channel
                adsbyte[i+4] = adsbyte[i  ] = adsbyte[i/2  ];
              }
            } else if(data_reduction_mode==2){
              if(adsbyte.Length != audio_bufsize/2){
                Debug.LogError("** error : adsbyte.Length and audio_bufsize is not consistent ** 2 " + adsbyte.Length);
                Application.Quit();
              }
              Array.Resize(ref adsbyte, audio_bufsize * 2);
              for(int i=adsbyte.Length-8;i>=0;i-=8){
                adsbyte[i+7] = adsbyte[i+5] = adsbyte[i+3] = adsbyte[i+1] = adsbyte[i/4+1];
                adsbyte[i+6] = adsbyte[i+4] = adsbyte[i+2] = adsbyte[i  ] = adsbyte[i/4  ];
              } 
            } else if(data_reduction_mode==3){
              if(adsbyte.Length != audio_bufsize/4){
                Debug.LogError("** error : adsbyte.Length and audio_bufsize is not consistent **");
                Application.Quit();
              }
              Array.Resize(ref adsbyte, audio_bufsize * 2);
              for(int i=adsbyte.Length-8;i>=0;i-=8){// little endian is assumed
                adsbyte[i+7] = adsbyte[i+5] = adsbyte[i+3] = adsbyte[i+1] = adsbyte[i/8];
                adsbyte[i+6] = adsbyte[i+4] = adsbyte[i+2] = adsbyte[i  ] = 0;
              } 
            } else {
              Debug.LogError("** not supported data_reduction_mode=" + data_reduction_mode);
              Application.Quit();
            }

//          Debug.Log("Going to add adsbyte");
            for(int i=0; i<adsbyte.Length; i++) adslist[ads_last][i] = adsbyte[i];
//            adsbyte = null;
            ads_last = add_adspointer(ads_last, 1);
//         Debug.Log("ads_first = " + ads_first + " ads_last = " + ads_last + " numdata = " + numdata_adspointer(ads_first, ads_last));
            if(numdata_adspointer(ads_first, ads_last)>=138){
              ads_first = add_adspointer(ads_first, 63); // reduce the number of data to 75 instead of 138
              Debug.Log("Reduced adslist to " + numdata_adspointer(ads_first, ads_last));
            }
//         Debug.Log(" Json: type="+it.type+" payload="+it.payload+" binary["+it.binary.Length+"] adsbyte["+adsbyte.Length+"]");

          } else if(it.payload.Substring(0,4)=="bfd ") { // compressed version of body frame data
            bfd = System.Text.Encoding.UTF8.GetString(Compressor.Decompress(it.binary));
//            Debug.Log("bfd=" + bfd);
          } else if(it.payload.Substring(0,3)=="bbj") { // black board jpeg data
            //Debug.Log("Black board jpeg data is received");
            bbjpeg = it.binary;
          } else {
            Debug.Log("Not supported binary data");
          }
        } else if(it.type=="echo" || it.type=="broadcast"){
          //Debug.Log("  Json: type="+it.type+" payload="+it.payload);
          if(it.payload.Substring(0,4)=="cars"){ // car data
            string[] splitMsg = it.payload.Split('|');
            string[] dataArr = new string[splitMsg.Length - 1];
            Array.Copy(splitMsg, 1, dataArr, 0, dataArr.Length);  // キーを取り除いて配列に直す。データのオブジェクトを新しく定義した方が楽か？
            carsdata = dataArr;
            //Debug.Log("cars=" + carsdata[0]);
          } else if(it.payload.Substring(0,4)=="act "){ // active flag
//            Debug.Log("Received active flag=" + it.payload);
            for(int i=4; i<it.payload.Length; i++){
              if(it.payload.Substring(i,1)=="1"){
                isactive[i-4] = true;
              } else {
                isactive[i-4] = false;
              }
            }
          } else if(it.payload.Substring(0,4)=="cps "){ // Other players position
//            Debug.Log("Received other players position, " + it.payload);
            if(cpsqueue.Count<maxcpsdata){
              string[] scans = it.payload.Substring(4).Split(' ');
              Cpsdata cpsone = new Cpsdata();
              cpsone.pos.x = float.Parse(scans[0]);
              cpsone.pos.y = float.Parse(scans[1]);
              cpsone.pos.z = float.Parse(scans[2]);
              cpsone.rot.x = float.Parse(scans[3]);
              cpsone.rot.y = float.Parse(scans[4]);
              cpsone.rot.z = float.Parse(scans[5]);
              cpsqueue.Enqueue(cpsone);
//              Debug.Log(cpsqueue.Count + ": Queuing otherPerson's position=" + float.Parse(scans[0]) + float.Parse(scans[1]) + float.Parse(scans[2]));
            } else {
              Debug.Log("cpsqueue is maximum (" + maxcpsdata + ")");
            }
          } else { // body data
            bfd = it.payload;
          }
          bpssum += it.payload.Length * 8;
        } else if(it.type=="broadcastResult"){
//         Debug.Log("  Json: type="+it.type+" payload="+it.payload);
        } else {
          Debug.Log("Not supported type,  Json: type="+it.type+" payload="+it.payload);
        }
      };
    
    ws.OnError += (sender, e) =>
      {
       Debug.Log("WebSocket Error Message: " + e.Message);
       wsstate = 0;
      };
    
    ws.OnClose += (sender, e) =>
      {
       Debug.Log("WebSocket Close");
       wsstate = 0;
      };
  }
  void Start()
  {
    if (!kinectManager){
      kinectManager = KinectManager.Instance;
      if(!is_mic_client) kinectManager.EnablePlayMode(true);
    }

    // allocate buffer
    for(int i=0; i<max_buffers; i++){
      adslist[i] = new byte[audio_bufsize*2];
    }

    ws = websocket_open();
    websocket_registerroutines();
    task = Task.Run(() => websocket_tryconnect());
    //websocket_tryconnect();
//    Debug.Log("ws=" + ws + " wsstate=" + wsstate);

#if !UNITY_WEBGL
#if !UNITY_ANDROID
    if(is_mic_client){
      // Initialize Microphone
      ads = GetComponent<AudioSource>();
      Debug.Log("Microphone.devices");
      for(int i=0; i<Microphone.devices.Length; i++){
//        Debug.Log(Microphone.devices[i]);
        int minFreq, maxFreq;
        Microphone.GetDeviceCaps(Microphone.devices[i], out minFreq, out maxFreq);
        Debug.Log(Microphone.devices[i] + "min=" + minFreq + " max=" + maxFreq);
      }
      ads.clip = Microphone.Start(null, true, 1, 48000);
//      ads.clip = Microphone.Start(Microphone.devices[0], true, 1, 48000);
//      ads.clip = Microphone.Start(Microphone.devices[1], true, 1, 44100);
      ads.loop = true;
      if(ads.clip != null){
        while (Microphone.GetPosition(null) <= 0) {}
        ads.Play();
      } else {
        Debug.Log("Failed to initialize microphone");
      }
    }
#endif
#endif

// Initialize activeflag
    isactive[0] = true; isactive[1] = false; isactive[2] = true; isactive[3] = false; isactive[4] = true; isactive[5] = false;
    isactive[6] = true; isactive[7] = false; isactive[8] = false; isactive[9] = true; 
  }
  
  void Update()
  {
    mytime = Time.time;
    frame++;
    bool updateflag = false;
    if(mytime - lasttime >= waittime){
      lasttime = mytime;
      updateflag = true;
    }

    // retry of websocket connection
    if(updateflag){
      if(wsstate==0 && (task==null || task.IsCompleted)){
        task = Task.Run(() => websocket_tryconnect());
      }
    }

    if(is_mic_client){
      if((data_reduction_mode==0) ||
         (data_reduction_mode==1 && (frame % 2)==0) ||
         (data_reduction_mode==2 && (frame % 4)==0) ||
         (data_reduction_mode==3 && (frame % 8)==0)){
//      liRelTime = kinectManager.GetBodyFrameTimestamp();
        string sBodyFrame = kinectManager.GetBodyFrameData(ref fCurrentTime, ';');
        System.Globalization.CultureInfo invCulture = System.Globalization.CultureInfo.InvariantCulture;
        if (sBodyFrame.Length > 0){
          string sRelTime = string.Format(invCulture, "{0:F3}", (fCurrentTime - fStartTime));
          string oneBodyData = sRelTime + "|" + sBodyFrame;
          if(is_compressed){
            itemBody.type = "broadcastbinary";
            itemBody.payload = "bfd ";
            itemBody.binary = Compressor.Compress(System.Text.Encoding.UTF8.GetBytes(oneBodyData));
            if(wsstate==1) ws.Send(JsonUtility.ToJson(itemBody));
          } else {
            itemBody.type = "broadcast";
            itemBody.payload = oneBodyData;
            itemBody.binary = null;
            if(wsstate==1) ws.Send(JsonUtility.ToJson(itemBody));
//        Debug.Log(oneBodyData);
          }
        }
      }

      // activeflag
      if (Input.GetKeyDown(KeyCode.Alpha0)){
        isactive[9] = !isactive[9];
      } else if(Input.GetKeyDown(KeyCode.Alpha1)){
        isactive[0] = !isactive[0];
        isactive[1] = !isactive[1];
      } else if(Input.GetKeyDown(KeyCode.Alpha2)){
        isactive[2] = !isactive[2];
        isactive[3] = !isactive[3];
      } else if(Input.GetKeyDown(KeyCode.Alpha3)){
        isactive[4] = !isactive[4];
        isactive[5] = !isactive[5];
      } else if(Input.GetKeyDown(KeyCode.Alpha7)){
        isactive[6] = !isactive[6];
      } else if(Input.GetKeyDown(KeyCode.Alpha8)){
        isactive[7] = !isactive[7];
      } else if(Input.GetKeyDown(KeyCode.Alpha9)){
        isactive[8] = !isactive[8];
      }
/*      for(int i=0; i<Characters.Length; i++){
        Characters[i].SetActive(isactive[i]);
      }*/
      if(frame % 60 == 0){
        string stmp = "1010101001";
        char[] ctmp = new char[10];
        for(int i=0; i<Characters.Length; i++){
          if(isactive[i] == true) ctmp[i] = '1';
          else                    ctmp[i] = '0';
//          Debug.Log("Characters[" + i + "]=" + Characters[i].activeSelf);
        } 
        stmp = new string(ctmp);
        itemBody.type = "broadcast";
        itemBody.payload = "act " + stmp;
        itemBody.binary = null;
        if(wsstate==1) ws.Send(JsonUtility.ToJson(itemBody));
        Debug.Log("active string=" + itemBody.payload); 
      }
    } else { // non mic client
      // manual gabarge collection
      if(frame % 40==20) System.GC.Collect();
      
      if(bfd!=null && bfd.Length>0){
        char[] delimiters = { '|' };
        string[] sLineParts = bfd.Split(delimiters);
        string sPlayLine = string.Empty; 
        if (sLineParts.Length >= 2){
//                float.TryParse(sLineParts[0], numFloat, invCulture, out fPlayTime);
          sPlayLine = sLineParts[1];
        }
        kinectManager.SetBodyFrameData(sPlayLine);
//        Debug.Log("SetBodyFrameData is called, bfd=" + bfd);
      }

      // Send MyCharacter position to others
      if(MyCharacter != null && (frame % 20)==0){
        SendMyPosition(MyCharacter.transform.position, MyCharacter.transform.eulerAngles);
      }

      // Set DebugPerson's position
/*      if(DebugOtherPerson != null && (frame % 20)==0){
        DebugOtherPerson.transform.position = debug_person_pos;
        DebugOtherPerson.transform.rotation = Quaternion.Euler(debug_person_rot);
      }*/

      // Report network trafic
      if(updateflag){
        Debug.Log("Average Mbps = " + bpssum/1000000.0f/waittime);
        bpssum = 0.0f;
      }
    }
    if(wsstate==1){
      int i;
      for(i=0; i<Characters.Length - 1; i++){
        Characters[i].SetActive(isactive[i]);
      }
      Characters[i].SetActive(true);
      if(isactive[i]){
        Characters[i].transform.localPosition = new Vector3(-3.63f, 0.06f, 2.9f);
        Characters[i].transform.localRotation = Quaternion.Euler(new Vector3(90.0f, 0.0f, 0.0f));
        Characters[i].transform.localScale    = new Vector3(0.04f, 0.01f, -0.01f);
      } else {
        Characters[i].transform.localPosition = new Vector3(-5.508f, 0.09f, 2.73f);
        Characters[i].transform.localRotation = Quaternion.Euler(new Vector3(90.0f, 90.0f, 0.0f));
//        Characters[i].transform.localScale    = new Vector3(0.04f, 0.01f, -0.01f);
        Characters[i].transform.localScale    = new Vector3(0.0555f, 0.0138f, -0.013f);
      }
      for(i=0; i<OffCharacters.Length; i++){
        OffCharacters[i].SetActive(false);
      }
    } else {
      for(int i=0; i<Characters.Length; i++){
        Characters[i].SetActive(false);
      }
      for(int i=0; i<OffCharacters.Length; i++){
        OffCharacters[i].SetActive(true);
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
    if(kinectManager) kinectManager.EnablePlayMode(false);
  }

  void OnAudioFilterRead (float[] data, int channels)
	{
    int i,j;

    read_bufsize = data.Length;
    read_channels = channels;
//        Debug.Log("size="+data.Length+" channel="+channels);

    // get mic data and send it to other clients
    if(is_mic_client && playing){
      if(channels!=2){
        Debug.LogError("channel is not 2");
        Application.Quit();
      }
//      ads.GetOutputData(adsdata, 2);
      item.type = "broadcastbinary";
      if(is_compressed) item.payload = "mic" + data_reduction_mode + " " + mytime.ToString();
      else              item.payload = "MIC" + data_reduction_mode + " " + mytime.ToString();
      byte[] b = new byte[2];
      byte[] btmp = null;
      if(data_reduction_mode==0){
        btmp = new byte[data.Length * 2];
        for(i=0; i<data.Length; i++){
          b = BitConverter.GetBytes((short)(data[i]*32767));
          for(j=0; j<2; j++) btmp[i*2+j] = b[j];
        }
      } else if(data_reduction_mode==1){// sampling rate is reduced
        short sdata;
        btmp = new byte[data.Length];
        for(i=0; i<data.Length; i+=4){
          sdata = (short)((data[i]+data[i+2])*0.5f*32767);
          b = BitConverter.GetBytes(sdata);
          for(j=0; j<2; j++) btmp[i+j] = b[j];
          sdata = (short)((data[i+1]+data[i+3])*0.5f*32767);
          b = BitConverter.GetBytes(sdata);
          for(j=0; j<2; j++) btmp[i+2+j] = b[j];
        }
      } else if(data_reduction_mode==2){// two channels are merged
        short sdata;
        btmp = new byte[data.Length/2];
        for(i=0; i<data.Length; i+=4){
          sdata = (short)((data[i]+data[i+1]+data[i+2]+data[i+3])*0.25f*32767);
          b = BitConverter.GetBytes(sdata);
          for(j=0; j<2; j++) btmp[i/2+j] = b[j];
        }
      } else if(data_reduction_mode==3){// 8-bit mode
        short sdata;
        btmp = new byte[data.Length/4];
        for(i=0; i<data.Length; i+=4){
          sdata = (short)((data[i]+data[i+1]+data[i+2]+data[i+3])*0.25f*32767);
          b = BitConverter.GetBytes(sdata);
          btmp[i/4] = b[1]; // little endian is assumed
        }
      } else {
        Debug.LogError("** not supported data_reduction_mode=" + data_reduction_mode);
        Application.Quit();
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
      if(wsstate==1) ws.Send(JsonUtility.ToJson(item));
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
      /* // This check is not needed since adslist is used
//      if(data.Length*4 != adsbyte.Length){
      if(data.Length*2 != adsbyte.Length){
        Debug.LogError("** error : data size is not consistent **");
        Application.Quit();
      }*/

      // buffering
      if(adslist_waitflag == 1){// normal satte
        if(numdata_adspointer(ads_first, ads_last)>=13){
/* #if UNITY_ANDROID 
          for(i=0; i<data.Length/2; i+=2){
            data[i+1] = (float)(BitConverter.ToInt16(adslist[ads_first], (i*2+1)*2) / 32767.0f);
            data[i  ] = (float)(BitConverter.ToInt16(adslist[ads_first], (i*2)*2) / 32767.0f);
          }
          ads_first = add_adspointer(ads_first, 1);
          for(i=0; i<data.Length/2; i+=2){
            data[data.Length/2+i+1] = (float)(BitConverter.ToInt16(adslist[ads_first], (i*2+1)*2) / 32767.0f);
            data[data.Length/2+i  ] = (float)(BitConverter.ToInt16(adslist[ads_first], (i*2)*2) / 32767.0f);
          }
#else
*/
          for(i=0; i<data.Length; i++){
            data[i] = (float)(BitConverter.ToInt16(adslist[ads_first], i*2) / 32767.0f);
          }
//#endif
          ads_first = add_adspointer(ads_first, 1);
        } else {
          adslist_waitflag = 0;
        }
      }
      if(adslist_waitflag == 0){// buffering state
//        Debug.Log("number of buffers is " + numdata_adspointer(ads_first, ads_last));
        for(i=0; i<data.Length; i++) data[i]=0.0f;
        if(numdata_adspointer(ads_first, ads_last)>=75){
          Debug.Log("adslist become 75");
          adslist_waitflag = 1;
        }
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
		  if (GUI.Button (new Rect (10, y, 70+Convert.ToInt32(playing)*30, 30), "Mic on")) {
			  playing = true;
		  }
		  y += 40;
		  if (GUI.Button (new Rect (10, y, 70+Convert.ToInt32(playing==false)*30, 30), "Mic off")) {
			  playing = false;
		  }
    } else {
      int y = 10;
      GUI.Button (new Rect (10, y, numdata_adspointer(ads_first, ads_last)*3, 30), "numdata");
/*      y += 40;
      GUI.Button (new Rect (10, y, read_bufsize/16, 128), "read_bufsize");
      y += 140;
      GUI.Button (new Rect (10, y, read_channels*64, 128), "read_channels");*/
    }
	}
}
