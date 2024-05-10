using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.kinect;


namespace com.rfilkov.components
{
    /// <summary>
    /// ForegroundBlendRenderer provides volumetric rendering and lighting of the real environment, filtered by the background-removal manager.
    /// </summary>
    public class ForegroundBlendRenderer : MonoBehaviour
    {
        [Tooltip("Reference to background removal manager. If left to None, it looks up the first available BR-manager in the scene.")]
        public BackgroundRemovalManager backgroundRemovalManager = null;

        [Tooltip("Depth value in meters, used for invalid depth points.")]
        public float invalidDepthValue = 0f;

        [Tooltip("Whether to maximize the rendered object on the screen, or not.")]
        public bool maximizeOnScreen = true;

        [Tooltip("Whether to apply per-pixel lighting on the foreground, or not.")]
        public bool applyLighting = false;


        // references to KM and data
        private KinectManager kinectManager = null;
        private KinectInterop.SensorData sensorData = null;
        private DepthSensorBase sensorInt = null;
        private Material matRenderer = null;

        // depth image buffer (in depth camera resolution)
        private ComputeBuffer depthImageBuffer = null;

        // textures
        private Texture alphaTex = null;
        private Texture colorTex = null;

        // lighting
        private FragmentLighting lighting = new FragmentLighting();


        void Start()
        {
            kinectManager = KinectManager.Instance;

            if (backgroundRemovalManager == null)
            {
                backgroundRemovalManager = FindObjectOfType<BackgroundRemovalManager>();
            }

            Renderer meshRenderer = GetComponent<Renderer>();
            if (meshRenderer && meshRenderer.material /**&& meshRenderer.material.mainTexture == null*/)
            {
                matRenderer = meshRenderer.material;
            }

            if (kinectManager && kinectManager.IsInitialized() && backgroundRemovalManager && backgroundRemovalManager.enabled)
            {
                // get sensor data
                sensorData = kinectManager.GetSensorData(backgroundRemovalManager.sensorIndex);
                sensorInt = sensorData != null ? (DepthSensorBase)sensorData.sensorInterface : null;
            }

            // find scene lights
            Light[] sceneLights = GameObject.FindObjectsOfType<Light>();
            lighting.SetLightsAndBounds(sceneLights, transform.position, new Vector3(20f, 20f, 20f));

            //Debug.Log("sceneLights: " + sceneLights.Length);
            //for(int i = 0; i < sceneLights.Length; i++)
            //{
            //    Debug.Log(i.ToString() + " - " + sceneLights[i].name + " - " + sceneLights[i].type);
            //}
        }


        void OnDestroy()
        {
            if (sensorData != null && sensorData.colorDepthBuffer != null)
            {
                sensorData.colorDepthBuffer.Release();
                sensorData.colorDepthBuffer = null;
            }

            if (depthImageBuffer != null)
            {
                //depthImageCopy = null;

                depthImageBuffer.Release();
                depthImageBuffer = null;
            }

            // release lighting resources
            lighting.ReleaseResources();
        }


        void Update()
        {
            if (matRenderer == null || sensorInt == null)
                return;

            if(alphaTex == null)
            {
                // alpha texture
                alphaTex = backgroundRemovalManager.GetAlphaTex();

                if(alphaTex != null)
                {
                    matRenderer.SetTexture("_AlphaTex", alphaTex);
                }
            }

            if(colorTex == null)
            {
                // color texture
                colorTex = !backgroundRemovalManager.computeAlphaMaskOnly ? backgroundRemovalManager.GetForegroundTex() : alphaTex;  // sensorInt.pointCloudColorTexture

                if (colorTex != null)
                {
                    matRenderer.SetInt("_TexResX", colorTex.width);
                    matRenderer.SetInt("_TexResY", colorTex.height);

                    matRenderer.SetTexture("_ColorTex", colorTex);
                }
            }

            if (colorTex == null || alphaTex == null /**|| foregroundCamera == null*/)
                return;

            if (sensorInt.pointCloudResolution == DepthSensorBase.PointCloudResolution.DepthCameraResolution)
            {
                if (depthImageBuffer == null)
                {
                    //int depthImageLength = sensorData.depthImageWidth * sensorData.depthImageHeight;
                    //depthImageCopy = new ushort[depthImageLength];

                    int depthBufferLength = sensorData.depthImageWidth * sensorData.depthImageHeight / 2;
                    depthImageBuffer = KinectInterop.CreateComputeBuffer(depthImageBuffer, depthBufferLength, sizeof(uint));
                    matRenderer.SetBuffer("_DepthMap", depthImageBuffer);

                    ScaleRendererTransform(colorTex);
                }

                if (depthImageBuffer != null && sensorData.depthImage != null)
                {
                    //KinectInterop.CopyBytes(sensorData.depthImage, sizeof(ushort), depthImageCopy, sizeof(ushort));

                    int depthBufferLength = sensorData.depthImageWidth * sensorData.depthImageHeight / 2;
                    KinectInterop.SetComputeBufferData(depthImageBuffer, sensorData.depthImage, depthBufferLength, sizeof(uint));
                }

                //Debug.Log("ForegroundBlendRenderer DepthFrameTime: " + lastDepthFrameTime);
            }
            else
            {
                if (sensorData.colorDepthBuffer == null)
                {
                    int bufferLength = sensorData.colorImageWidth * sensorData.colorImageHeight / 2;
                    sensorData.colorDepthBuffer = new ComputeBuffer(bufferLength, sizeof(uint));
                    matRenderer.SetBuffer("_DepthMap", sensorData.colorDepthBuffer);

                    ScaleRendererTransform(colorTex);
                }

                //Debug.Log("ForegroundBlendRenderer ColorDepthBufferTime: " + sensorData.lastColorDepthBufferTime);
            }

            matRenderer.SetFloat("_DepthDistance", 0f);
            matRenderer.SetFloat("_InvDepthVal", invalidDepthValue);

            // update lighting parameters
            lighting.UpdateLighting(matRenderer, applyLighting);
        }

        // scales the renderer's transform properly
        private void ScaleRendererTransform(Texture colorTex)
        {
            Vector3 localScale = transform.localScale;

            if (maximizeOnScreen)
            {
                Camera camera = Camera.main;
                float objectZ = transform.position.z;

                float screenW = Screen.width;
                float screenH = Screen.height;

                Vector3 vLeft = camera.ScreenToWorldPoint(new Vector3(0f, screenH / 2f, objectZ));
                Vector3 vRight = camera.ScreenToWorldPoint(new Vector3(screenW, screenH / 2f, objectZ));
                float distLeftRight = (vRight - vLeft).magnitude;

                Vector3 vBottom = camera.ScreenToWorldPoint(new Vector3(screenW / 2f, 0f, objectZ));
                Vector3 vTop = camera.ScreenToWorldPoint(new Vector3(screenW / 2f, screenH, objectZ));
                float distBottomTop = (vTop - vBottom).magnitude;

                localScale.x = distLeftRight / localScale.x;
                localScale.y = distBottomTop / localScale.y;
            }

            // scale according to color-tex resolution
            //localScale.y = localScale.x * colorTex.height / colorTex.width;

            // apply color image scale
            Vector3 colorImageScale = kinectManager.GetColorImageScale(backgroundRemovalManager.sensorIndex);
            if (colorImageScale.x < 0f)
                localScale.x = -localScale.x;
            if (colorImageScale.y < 0f)
                localScale.y = -localScale.y;

            transform.localScale = localScale;
        }

    }
}
