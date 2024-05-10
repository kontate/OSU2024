using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using com.rfilkov.kinect;


namespace com.rfilkov.components
{
    /// <summary>
    /// Simple gesture listener that only displays the status and progress of the given gestures.
    /// </summary>
    public class mySimpleGestureListener : MonoBehaviour, GestureListenerInterface
    {
        [Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
        public int playerIndex = 0;

        [Tooltip("List of the gestures to detect.")]
        public List<GestureType> detectGestures = new List<GestureType>();

        [Tooltip("UI-Text to display the gesture-listener output.")]
        public UnityEngine.UI.Text gestureInfo;

        [Tooltip("Seconds to continue the face after run-gesture")]
        public float runface_time = 2.5f;

        // private bool to track if progress message has been displayed
        private bool progressDisplayed;
        private float progressGestureTime;

        // Added by T.Narumi
        private int playeroffset = 0;
        private float current_time = 0;
        private int runflag = 0, runflag_bak = 0;
        private float runtime = 0;

        // The singleton instance
        protected static mySimpleGestureListener instance = null;
        public static mySimpleGestureListener Instance
        {
            get
            {
                return instance;
            }
        }
        void Awake()
        {
            // initializes the singleton instance of KinectManager
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(this);
            }
            else if (instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }
        }
        void OnDestroy()
        {
            if (instance == null || instance != this)
                return;
            instance = null;
        }


        // invoked when a new user is detected
        public void UserDetected(ulong userId, int userIndex)
        {
            if (userIndex == playerIndex)
            {
                // as an example - detect these user specific gestures
                KinectGestureManager gestureManager = KinectManager.Instance.gestureManager;

                foreach(GestureType gesture in detectGestures)
                {
                    gestureManager.DetectGesture(userId, gesture);
                }
            }

            if (gestureInfo != null)
            {
                //gestureInfo.text = "Please do the gestures and look for the gesture detection state.";
            }
        }


        // invoked when the user is lost
        public void UserLost(ulong userId, int userIndex)
        {
            if (userIndex != playerIndex)
                return;

            if (gestureInfo != null)
            {
                gestureInfo.text = string.Empty;
            }
        }


        // invoked to report gesture progress. important for the continuous gestures, because they never complete.
        public void GestureInProgress(ulong userId, int userIndex, GestureType gesture,
                                      float progress, KinectInterop.JointType joint, Vector3 screenPos)
        {
            if (userIndex != playerIndex)
                return;

            // check for continuous gestures
            switch(gesture)
            {
                case GestureType.ZoomOut:
                case GestureType.ZoomIn:
                    if (progress > 0.5f && gestureInfo != null)
                    {
                        string sGestureText = string.Format("{0} - {1:F0}%", gesture, screenPos.z * 100f);
                        gestureInfo.text = sGestureText;

                        progressDisplayed = true;
                        progressGestureTime = Time.realtimeSinceStartup;
                    }
                    break;

                case GestureType.Wheel:
                case GestureType.LeanLeft:
                case GestureType.LeanRight:
                case GestureType.LeanForward:
                case GestureType.LeanBack:
                    if (progress > 0.5f && gestureInfo != null)
                    {
                        string sGestureText = string.Format("{0} - {1:F0} degrees", gesture, screenPos.z);
                        gestureInfo.text = sGestureText;

                        progressDisplayed = true;
                        progressGestureTime = Time.realtimeSinceStartup;
                    }
                    break;

                case GestureType.Run:
                    if (progress > 0.5f && gestureInfo != null)
                    {
                        string sGestureText = string.Format("{0} - progress: {1:F0}%", gesture, progress * 100);
                        gestureInfo.text = sGestureText;

                        progressDisplayed = true;
                        progressGestureTime = Time.realtimeSinceStartup;
                        // Added by T.Narumi
//                        runflag = 1;
                    }
                    break;
            }
        }


        // invoked when a (discrete) gesture is complete.
        public bool GestureCompleted(ulong userId, int userIndex, GestureType gesture,
                                      KinectInterop.JointType joint, Vector3 screenPos)
        {
            if (userIndex != playerIndex)
                return false;

            if (progressDisplayed)
                return true;

            string sGestureText = gesture + " detected";
            if (gestureInfo != null)
            {
                gestureInfo.text = sGestureText;

                // added by T.Narumi
                KinectManager manager = KinectManager.Instance;
//                if(gesture == KinectGestures.Gestures.SwipeRight){
                if(runflag==0){
                    if(gesture == GestureType.SwipeRight){
                        playeroffset++;
/*                    int newplayerindex = (manager.playerindex_offset + 1) % manager.Characters.Length;
                    int current = manager.Characters[manager.playerindex_offset].GetComponent<AvatarController>().playerIndex;
                    int next = manager.Characters[newplayerindex].GetComponent<AvatarController>().playerIndex;
                    manager.Characters[manager.playerindex_offset].GetComponent<AvatarController>().playerIndex = next;
                    manager.Characters[newplayerindex].GetComponent<AvatarController>().playerIndex = current;
                    manager.playerindex_offset = newplayerindex;
                    manager.RefreshAvatarUserIds();*/
                    } 
                    if(gesture == GestureType.SwipeLeft){
                        playeroffset--;
                    }
                    if(gesture == GestureType.Jump){
                        runflag = 1;
                    }
                }
            }

            return true;
        }

        // Added by T.Narumi
        public int GetPlayeroffset()
        {
            return playeroffset;
        }
        public int GetRunflag()
        {
            return runflag;
        }

        // invoked when a gesture gets cancelled by the user
        public bool GestureCancelled(ulong userId, int userIndex, GestureType gesture,
                                      KinectInterop.JointType joint)
        {
            if (userIndex != playerIndex)
                return false;

            if (progressDisplayed)
            {
                progressDisplayed = false;

                if (gestureInfo != null)
                {
                    gestureInfo.text = String.Empty;
                }
            }

            return true;
        }


        public void Update()
        {
            // checks for timed out progress message
            if (progressDisplayed && ((Time.realtimeSinceStartup - progressGestureTime) > 2f))
            {
                progressDisplayed = false;

                if (gestureInfo != null)
                {
                    gestureInfo.text = String.Empty;
                }

                Debug.Log("Forced progress to end.");
            }

            // Added by T.Narumi
            current_time = Time.time;
            if(runflag_bak==0 && runflag==1){
                runtime = current_time;
            } else if(runflag==1 && (current_time - runtime > runface_time)){
                runflag = 2;
            } else if(runflag==2 && (current_time - runtime > runface_time * 2.0f)){
                runflag = 0;
            }
            runflag_bak = runflag;
        }

    }
}
