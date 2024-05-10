using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.kinect;


namespace com.rfilkov.components
{
    /// <summary>
    /// SceneBlendRenderer provides volumetric rendering and lighting of the real environment, as seen by the sensor's color camera.
    /// </summary>
    public class SceneBlendRenderer : MonoBehaviour
    {
        [Tooltip("Added depth distance between the real environment and the virtual environment, in meters.")]
        [Range(-0.5f, 0.5f)]
        public float depthDistance = 0.1f;

        [Tooltip("Index of the depth sensor that generates the color camera background. 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0;

        [Tooltip("Depth value in meters, used for invalid depth points.")]
        public float invalidDepthValue = 5f;

        [Tooltip("Whether to maximize the rendered object on the screen, or not.")]
        private bool maximizeOnScreen = true;

        [Tooltip("Whether to apply per-pixel lighting on the foreground, or not.")]
        public bool applyLighting = false;

        [Tooltip("Reference to a background removal manager, if one is available in the scene.")]
        public BackgroundRemovalManager backgroundRemovalManager = null;


        // references to KM and data
        private KinectManager kinectManager = null;
        private KinectInterop.SensorData sensorData = null;
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

            Renderer meshRenderer = GetComponent<Renderer>();
            if (meshRenderer && meshRenderer.material /**&& meshRenderer.material.mainTexture == null*/)
            {
                matRenderer = meshRenderer.material;
            }

            if (kinectManager && kinectManager.IsInitialized())
            {
                // get sensor data
                sensorData = kinectManager.GetSensorData(sensorIndex);
            }

            // find scene lights
            Light[] sceneLights = GameObject.FindObjectsOfType<Light>();
            lighting.SetLightsAndBounds(sceneLights, transform.position, new Vector3(20f, 20f, 20f));
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
            if (matRenderer == null || sensorData == null)
                return;

            if(backgroundRemovalManager != null && alphaTex == null)
            {
                // alpha texture
                alphaTex = backgroundRemovalManager.GetAlphaTex();

                if (alphaTex != null)
                {
                    matRenderer.SetTexture("_AlphaTex", alphaTex);
                }
            }

            if(colorTex == null)
            {
                // color texture
                colorTex = sensorData.colorImageTexture;
                if(backgroundRemovalManager != null)
                {
                    colorTex = !backgroundRemovalManager.computeAlphaMaskOnly ? backgroundRemovalManager.GetForegroundTex() : alphaTex;
                }

                if (colorTex != null)
                {
                    matRenderer.SetInt("_TexResX", colorTex.width);
                    matRenderer.SetInt("_TexResY", colorTex.height);

                    matRenderer.SetTexture("_ColorTex", colorTex);
                }
            }

            if (colorTex == null)
                return;

            if (sensorData.colorDepthBuffer == null)
            {
                int bufferLength = sensorData.colorImageWidth * sensorData.colorImageHeight / 2;
                sensorData.colorDepthBuffer = new ComputeBuffer(bufferLength, sizeof(uint));
                matRenderer.SetBuffer("_DepthMap", sensorData.colorDepthBuffer);

                ScaleRendererTransform(colorTex);
            }

            matRenderer.SetFloat("_DepthDistance", depthDistance);
            matRenderer.SetFloat("_InvDepthVal", invalidDepthValue);

            // update lighting parameters
            lighting.UpdateLighting(matRenderer, applyLighting);
        }

        // scales the renderer's transform properly
        private void ScaleRendererTransform(Texture colorTex)
        {
            Vector3 localScale = Vector3.one;  // transform.localScale;

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
            Vector3 colorImageScale = kinectManager.GetColorImageScale(sensorIndex);
            if (colorImageScale.x < 0f)
                localScale.x = -localScale.x;
            if (colorImageScale.y < 0f)
                localScale.y = -localScale.y;

            transform.localScale = localScale;
        }

    }
}
