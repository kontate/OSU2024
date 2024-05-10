using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using com.rfilkov.components;

// For task
using System.Threading;
using System.Threading.Tasks;

namespace com.rfilkov.kinect
{
    /// <summary>
    /// Background removal manager is the component that filters and renders user body silhouettes.
    /// </summary>
    public class myBackgroundRemovalManager : MonoBehaviour
    {
        [Tooltip("Depth sensor index - 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0;

        [Tooltip("Index of the player, tracked by this component. -1 means all players, 0 - the 1st player only, 1 - the 2nd player only, etc.")]
        public int playerIndex = -1;

        // Added by T.Narumi
        [Tooltip("Check if you want to skip background removel")]
        public bool SkipBackgroundRemoval = false;

        [Tooltip("RawImage used for displaying the foreground image.")]
        public UnityEngine.UI.RawImage foregroundImage;

        [Tooltip("Camera used for alignment of bodies to color camera image.")]
        public Camera foregroundCamera;

        [Tooltip("Resolution of the generated foreground textures.")]
        private DepthSensorBase.PointCloudResolution foregroundImageResolution = DepthSensorBase.PointCloudResolution.ColorCameraResolution;

        [Tooltip("Whether only the alpha texture is needed.")]
        public bool computeAlphaMaskOnly = false;

        [Tooltip("Whether the alpha texture will be inverted or not..")]
        public bool invertAlphaMask = false;

        [Tooltip("(Advanced) Whether to apply the median filter before the other filters.")]
        public bool applyMedianFilter = false;

        [Tooltip("(Advanced) Number of iterations used by the alpha texture's erode filter 0.")]
        [Range(0, 9)]
        public int erodeIterations0 = 0;  // 1

        [Tooltip("(Advanced) Number of iterations used by the alpha texture's dilate filter 1.")]
        [Range(0, 9)]
        public int dilateIterations = 0;  // 3;

        [Tooltip("(Advanced) Whether to apply the gradient filter.")]
        private bool applyGradientFilter = true;

        [Tooltip("(Advanced) Number of iterations used by the alpha texture's erode filter 2.")]
        [Range(0, 9)]
        public int erodeIterations = 0;  // 4;

        [Tooltip("(Advanced) Whether to apply the blur filter after at the end.")]
        public bool applyBlurFilter = true;

        [Tooltip("(Advanced) Color applied to the body contour after the filters.")]
        public Color bodyContourColor = Color.black;

        [Tooltip("UI-Text to display the BR-Manager debug messages.")]
        public UnityEngine.UI.Text debugText;

        // Added by T.Narumi
//        public UnityEngine.UI.RawImage blackboardImage;
        public float update_period = 2.0f;
        public int bboffset_x = 320;
        public int bboffset_y = 400;
        public int bbsubdiv_y = 4;
        private int bboffset_factor = 1;
        private Texture2D blackboardTex;
        private Texture2D bbalphaTex;
        private Color[] bboutColors = null;
        private float currentTime = 0.0f, lastTime = 0.0f;
        private byte[] bbjpeg = null;
        private int jtagcount = 0;
        private K4A_mic_websocket K4A_instance = null;
        private Task task = null;
        private int frame = 0;

        // max number of bodies to track
        private const int MAX_BODY_COUNT = 10;

        // primary sensor data structure
        private KinectInterop.SensorData sensorData = null;
        private KinectManager kinectManager = null;

        // sensor interface
        private DepthSensorBase sensorInt = null;

        // render texture resolution
        private Vector2Int textureRes;

        // Bool to keep track whether Kinect and BR library have been initialized
        private bool bBackgroundRemovalInited = false;

        // The single instance of BackgroundRemovalManager
        //private static BackgroundRemovalManager instance;

        // last point cloud frame time
        private ulong lastDepth2SpaceFrameTime = 0;
        private ulong lastColorBodyIndexBufferTime = 0;

        // render textures used by the shaders
        private RenderTexture colorTexture = null;
        private RenderTexture vertexTexture = null;
        private RenderTexture alphaTexture = null;
        private RenderTexture foregroundTexture = null;

        // Materials used to apply the shaders
        private Material medianFilterMat = null;
        private Material erodeFilterMat = null;
        private Material dilateFilterMat = null;
        private Material gradientFilterMat = null;
        private Material blurFilterMat = null;
        private Material invertAlphaMat = null;
        private Material foregroundMat = null;

        // reference to filter-by-distance component
        private BackgroundRemovalByBodyBounds filterByBody = null;
        private BackgroundRemovalByDist filterByDist = null;
        private BackgroundRemovalByBodyIndex filterByBI = null;

        // whether the textures are created or not
        private bool bColorTextureCreated = false;
        private bool bVertexTextureCreated = false;


        ///// <summary>
        ///// Gets the single BackgroundRemovalManager instance.
        ///// </summary>
        ///// <value>The BackgroundRemovalManager instance.</value>
        //public static BackgroundRemovalManager Instance
        //{
        //    get
        //    {
        //        return instance;
        //    }
        //}

        /// <summary>
        /// Determines whether the BackgroundRemovalManager was successfully initialized.
        /// </summary>
        /// <returns><c>true</c> if the BackgroundRemovalManager was successfully initialized; otherwise, <c>false</c>.</returns>
        public bool IsBackgroundRemovalInited()
        {
            return bBackgroundRemovalInited;
        }

        /// <summary>
        /// Gets the foreground image texture.
        /// </summary>
        /// <returns>The foreground image texture.</returns>
        public Texture GetForegroundTex()
        {
            return foregroundTexture;
        }

        /// <summary>
        /// Gets the alpha texture.
        /// </summary>
        /// <returns>The alpha texture.</returns>
        public Texture GetAlphaTex()
        {
            return alphaTexture;
        }

        /// <summary>
        /// Gets the color texture.
        /// </summary>
        /// <returns>The color texture.</returns>
        public Texture GetColorTex()
        {
            return colorTexture;
        }

        /// <summary>
        /// Gets the last background removal frame time.
        /// </summary>
        /// <returns>The last background removal time.</returns>
        public ulong GetLastBackgroundRemovalTime()
        {
            return lastDepth2SpaceFrameTime;
        }


        //----------------------------------- end of public functions --------------------------------------//


        //void Awake()
        //{
        //    instance = this;
        //}

        public void Start()
        {
            try
            {
                // get sensor data
                kinectManager = KinectManager.Instance;
                if (kinectManager && kinectManager.IsInitialized())
                {
                    sensorData = kinectManager.GetSensorData(sensorIndex);
                }

                if (sensorData == null || sensorData.sensorInterface == null)
                {
                    throw new Exception("Background removal cannot be started, because KinectManager is missing or not initialized.");
                }

                if(foregroundImage == null)
                {
                    // look for a foreground image
                    foregroundImage = GetComponent<UnityEngine.UI.RawImage>();
                }

                if (!foregroundCamera)
                {
                    // by default - the main camera
                    foregroundCamera = Camera.main;
                }

                // try to get reference to other filter components
                filterByBody = GetComponent<BackgroundRemovalByBodyBounds>();
                if(filterByBody == null)
                    filterByDist = GetComponent<BackgroundRemovalByDist>();
                if (filterByBody == null && filterByDist == null)
                    filterByBI = GetComponent<BackgroundRemovalByBodyIndex>();

                if (filterByBody == null && filterByDist == null && filterByBI == null)
                    filterByBI = gameObject.AddComponent<BackgroundRemovalByBodyIndex>();  // fallback

                // Initialize the background removal
                // Modified by T.Narumi
                bool bSuccess = false;
                // if(!SkipBackgroundRemoval)
                    bSuccess = InitBackgroundRemoval(sensorData);
                
                if (bSuccess)
                {
                    if (debugText != null)
                        debugText.text = string.Empty;
                }
                else
                {
                    throw new Exception("Background removal could not be initialized.");
                }

                // Added by T.Narumi
                Material material = new Material(Shader.Find("Diffuse"));
                this.GetComponent<Renderer>().material = material;
                K4A_instance = K4A_mic_websocket.Instance;

                bBackgroundRemovalInited = bSuccess;
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError(ex.ToString());
                if (debugText != null)
                    debugText.text = "Please check the SDK installations.";
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (debugText != null)
                    debugText.text = ex.Message;
            }
        }

        public void OnDestroy()
        {
            if (bBackgroundRemovalInited)
            {
                // finish background removal
                FinishBackgroundRemoval(sensorData);
            }

            bBackgroundRemovalInited = false;
            //instance = null;
        }

        void MakeBlackBoardImage(){
//                    Debug.Log("in task");
                    if (bBackgroundRemovalInited)
                    {
                        // update the background removal
                        // Modified by T.Narumi
                        //if(!SkipBackgroundRemoval) 
                            UpdateBackgroundRemoval(sensorData);

                        // check for valid foreground image texture
                        if(foregroundImage != null && foregroundImage.texture == null)
                        {
                            Debug.Log("forgroundImage.texture is copyed");
                            foregroundImage.texture = foregroundTexture;
                            foregroundImage.rectTransform.localScale = kinectManager.GetColorImageScale(sensorIndex);
                            foregroundImage.color = Color.white;
                        }
                    }
//                    Debug.Log("in task p");
                    // Setup resolution of the texture
                    if(blackboardTex == null){
                        int resx=100, resy=100;
                        if(foregroundTexture.width == 1280 && foregroundTexture.height == 720){
                            bboffset_factor = 1;
                            if(bboffset_x==240){ // for Fuchu house camera, bboffset_y=340 is reasonable
                                resx = 800; resy = 300; 
                                // bboffset_x=0; bboffset_y=0;
                            } else {
                                resx = 1280; resy = 720; // for full camera
                            }
                        } else if(foregroundTexture.width == 1920 && foregroundTexture.height == 1080){
                            bboffset_factor = 1;
                            if(bboffset_x==360){ // for Fuchu house camera, bboffset_y=510 will be reasonable
                                resx = 1280; resy = 480;
                            } else if(bboffset_x==0 && bboffset_y==0){
                                resx = 1920; resy = 1080; // for full camera
                            } else if(bboffset_x==320 && bboffset_y==400){
                                resx = 1280; resy = 320; // for Room W9-135 black board
                            }
                        } else if(foregroundTexture.width == 3840 && foregroundTexture.height == 2160){
                            resx = 2560; resy = 640; // 160x16, 160x4
                            bboffset_factor = 2;
                        } else {
                            Debug.Log("Texture size is not supported");
                        }
                        Debug.Log("resx=" + resx + " resy=" + resy);
                        blackboardTex = new Texture2D(resx, resy, TextureFormat.RGB24, false);
                        if(!SkipBackgroundRemoval){
                            bbalphaTex = new Texture2D(resx, resy, TextureFormat.RGB24, false);
                            bboutColors = new Color[resx * resy];
                        }
                    }
                    var currentRT = RenderTexture.active; // Change the active render texture
//                    Debug.Log("in task a");
                    // Copy color image from Kinect
//                    Debug.Log("in task b");
                    RenderTexture.active = foregroundTexture;
                    // Graphics.Blit(blackboardTex, foregroundTexture);
                    blackboardTex.ReadPixels(new Rect(bboffset_x * bboffset_factor, bboffset_y * bboffset_factor,
                                                      blackboardTex.width,blackboardTex.height), 0, 0, false);
                    blackboardTex.Apply();

                    if(!SkipBackgroundRemoval){
                        Color[] blackboardColors = blackboardTex.GetPixels();
                    // Copy alpha image from Kinect
//                    Debug.Log("in task c");
                        RenderTexture.active = alphaTexture;
                    // Graphics.Blit(blackboardTex, alphaTexture);
                        bbalphaTex.ReadPixels(new Rect(bboffset_x * bboffset_factor, bboffset_y * bboffset_factor,
                                                       bbalphaTex.width,bbalphaTex.height), 0, 0, false);
                        bbalphaTex.Apply();
                        RenderTexture.active = currentRT; // Recover the previous renter texture

//                    Debug.Log("in task, blackboardColors.Length=" + blackboardColors.Length);
                        Color[] bbalphaColors = bbalphaTex.GetPixels();
                        Color alphaone = new Color(0.0f, 0.0f, 0.0f, 1.0f);
                        for(int iy=0; iy<bbsubdiv_y; iy++){
                            for(int ix=0; ix<bbsubdiv_y * 4; ix++){
                                int gflag = 0, x, y, i;
                                int xxmax = blackboardTex.width/(bbsubdiv_y*4);
                                int yymax = blackboardTex.height/bbsubdiv_y;
                                for(int yy=0; yy<yymax; yy++){
                                    for(int xx=0; xx<xxmax; xx++){
                                        x = ix * xxmax + xx;
                                        y = iy * yymax + yy;
                                        i = y * blackboardTex.width + x;
                                        if(bbalphaColors[i] != alphaone) gflag=1;
                                    }
                                }
                                if(gflag==0){
                                    for(int yy=0; yy<yymax; yy++){
                                        for(int xx=0; xx<xxmax; xx++){
                                            x = ix * xxmax + xx;
                                            y = iy * yymax + yy;
                                            i = y * blackboardTex.width + x;
                                            bboutColors[i] = blackboardColors[i];
                                        }
                                    }
                                }
                            }
                        }
                        blackboardTex.SetPixels(bboutColors);
                    /*for(int i=0; i<blackboardColors.Length; i++){
                        if(bbalphaColors[i] != alphaone) blackboardColors[i] = Color.green;
                    }
                    blackboardTex.SetPixels(blackboardColors);
                    */
                    // blackboardTex.SetPixels(bbalphaColors);
                        blackboardTex.Apply();

                    // convert to jpeg and convert back to texture
//                        Debug.Log("bbjpeg is updated in MakeBlackBoardImage ");
                        bbjpeg = blackboardTex.EncodeToJPG(80);
                        K4A_instance.SendBbjpeg(bbjpeg);
                    } else {
//                        bbjpeg = blackboardTex.EncodeToJPG(80);
//                        K4A_instance.SendBbjpeg(bbjpeg);
//                        RenderTexture.active = currentRT; // Recover the previous renter texture
                    }
        }

        void Update()
        {
            if (bBackgroundRemovalInited)
            {
                // update the background removal
                // Modified by T.Narumi
                //if(!SkipBackgroundRemoval) 
                    UpdateBackgroundRemoval(sensorData);

                // Added by T.Narumi
                if(K4A_instance.is_mic_client){
                    currentTime += Time.deltaTime;
                    if(currentTime > lastTime + update_period){
                        lastTime = currentTime;
                        MakeBlackBoardImage();
                    }
                } else {
//                    Debug.Log("GetBbjpeg is called in myBackgroundRemovalManager at Update");
                    bbjpeg = K4A_instance.GetBbjpeg();
                    K4A_instance.RemoveBbjpeg();
                }
                if(!SkipBackgroundRemoval){
                    if(bbjpeg!=null){
                        Texture2D tmpTex = new Texture2D(1,1);
                        tmpTex.LoadImage(bbjpeg);
//              blackboardImage.texture = tmpTex;
//              blackboardImage.color = Color.white;
                        this.GetComponent<Renderer>().material.mainTexture = tmpTex;
                        if(jtagcount % 30==0){
                            Debug.Log("Jpegsize=" + bbjpeg.Length);
                        }
                        jtagcount++;
                        bbjpeg=null;
                    }
                } else { // skip background removal
                    this.GetComponent<Renderer>().material.mainTexture = blackboardTex;
                }

                // check for valid foreground image texture
                if(foregroundImage != null && foregroundImage.texture == null)
                {
                    foregroundImage.texture = foregroundTexture;
                    foregroundImage.rectTransform.localScale = kinectManager.GetColorImageScale(sensorIndex);
                    foregroundImage.color = Color.white;
                }
            }

            // Added by T.Narumi
            if(frame % 40==5) System.GC.Collect();

            frame++;
        }


        // initializes background removal with shaders
        private bool InitBackgroundRemoval(KinectInterop.SensorData sensorData)
        {
            if (sensorData != null && sensorData.sensorInterface != null && KinectInterop.IsDirectX11Available())
            {
                if(filterByBody != null)
                {
                    if (!filterByBody.InitBackgroundRemoval(sensorData, MAX_BODY_COUNT))
                    {
                        Debug.LogError("Could not init the background removal by body bounds!");
                        return false;
                    }
                }
                else if(filterByBI != null)
                {
                    if(!filterByBI.InitBackgroundRemoval(sensorData))
                    {
                        Debug.LogError("Could not init the background removal by body index!");
                        return false;
                    }
                }

                sensorInt = (DepthSensorBase)sensorData.sensorInterface;

                // set the texture resolution
                if (sensorInt.pointCloudColorTexture == null && sensorInt.pointCloudVertexTexture == null)
                {
                    sensorInt.pointCloudResolution = foregroundImageResolution;
                }

                textureRes = sensorInt.GetPointCloudTexResolution(sensorData);

                if(sensorInt.pointCloudColorTexture == null)
                {
                    colorTexture = KinectInterop.CreateRenderTexture(colorTexture, textureRes.x, textureRes.y, RenderTextureFormat.ARGB32);
                    sensorInt.pointCloudColorTexture = colorTexture;
                    bColorTextureCreated = true;
                }
                else
                {
                    colorTexture = sensorInt.pointCloudColorTexture;
                    bColorTextureCreated = false;
                }

                if (filterByBI == null)
                {
                    if(sensorInt.pointCloudVertexTexture == null)
                    {
                        vertexTexture = KinectInterop.CreateRenderTexture(vertexTexture, textureRes.x, textureRes.y, RenderTextureFormat.ARGBHalf);
                        sensorInt.pointCloudVertexTexture = vertexTexture;
                        bVertexTextureCreated = true;
                    }
                    else
                    {
                        vertexTexture = sensorInt.pointCloudVertexTexture;
                        bVertexTextureCreated = false;
                    }
                }

                alphaTexture = KinectInterop.CreateRenderTexture(alphaTexture, textureRes.x, textureRes.y, RenderTextureFormat.ARGB32);
                foregroundTexture = KinectInterop.CreateRenderTexture(foregroundTexture, textureRes.x, textureRes.y, RenderTextureFormat.ARGB32);

                Shader erodeShader = Shader.Find("Kinect/ErodeShader");
                erodeFilterMat = new Material(erodeShader);
                erodeFilterMat.SetFloat("_TexResX", (float)textureRes.x);
                erodeFilterMat.SetFloat("_TexResY", (float)textureRes.y);
                //sensorData.erodeBodyMaterial.SetTexture("_MainTex", sensorData.bodyIndexTexture);

                Shader dilateShader = Shader.Find("Kinect/DilateShader");
                dilateFilterMat = new Material(dilateShader);
                dilateFilterMat.SetFloat("_TexResX", (float)textureRes.x);
                dilateFilterMat.SetFloat("_TexResY", (float)textureRes.y);
                //sensorData.dilateBodyMaterial.SetTexture("_MainTex", sensorData.bodyIndexTexture);

                Shader gradientShader = Shader.Find("Kinect/GradientShader");
                gradientFilterMat = new Material(gradientShader);

                Shader medianShader = Shader.Find("Kinect/MedianShader");
                medianFilterMat = new Material(medianShader);
                //sensorData.medianBodyMaterial.SetFloat("_Amount", 1.0f);

                Shader blurShader = Shader.Find("Kinect/BlurShader");
                blurFilterMat = new Material(blurShader);

                Shader invertShader = Shader.Find("Kinect/InvertShader");
                invertAlphaMat = new Material(invertShader);

                Shader foregroundShader = Shader.Find("Kinect/ForegroundShader");
                foregroundMat = new Material(foregroundShader);

                return true;
            }

            return false;
        }


        // releases background removal shader resources
        private void FinishBackgroundRemoval(KinectInterop.SensorData sensorData)
        {
            if(filterByBody != null)
            {
                filterByBody.FinishBackgroundRemoval(sensorData);
            }
            else if(filterByBI != null)
            {
                filterByBI.FinishBackgroundRemoval(sensorData);
            }

            if (sensorInt)
            {
                sensorInt.pointCloudColorTexture = null;
                sensorInt.pointCloudVertexTexture = null;
            }

            if (bColorTextureCreated && colorTexture)
            {
                colorTexture.Release();
                colorTexture = null;
            }

            if (bVertexTextureCreated && vertexTexture)
            {
                vertexTexture.Release();
                vertexTexture = null;
            }

            if (alphaTexture)
            {
                alphaTexture.Release();
                alphaTexture = null;
            }

            if(foregroundTexture)
            {
                foregroundTexture.Release();
                foregroundTexture = null;
            }

            erodeFilterMat = null;
            dilateFilterMat = null;
            medianFilterMat = null;
            blurFilterMat = null;
            invertAlphaMat = null;
            foregroundMat = null;
        }


        // computes current background removal texture
        private bool UpdateBackgroundRemoval(KinectInterop.SensorData sensorData)
        {
            if (bBackgroundRemovalInited && (lastDepth2SpaceFrameTime != sensorData.lastDepth2SpaceFrameTime ||
                lastColorBodyIndexBufferTime != sensorData.lastColorBodyIndexBufferTime))
            {
                lastDepth2SpaceFrameTime = sensorData.lastDepth2SpaceFrameTime;
                lastColorBodyIndexBufferTime = sensorData.lastColorBodyIndexBufferTime;
                //Debug.Log("BR Depth2SpaceFrameTime: " + lastDepth2SpaceFrameTime + " ColorBodyIndexBufferTime: " + lastColorBodyIndexBufferTime);

                RenderTexture[] tempTextures = new RenderTexture[2];
                tempTextures[0] = RenderTexture.GetTemporary(textureRes.x, textureRes.y, 0);
                tempTextures[1] = RenderTexture.GetTemporary(textureRes.x, textureRes.y, 0);

                RenderTexture[] tempGradTextures = null;
                if (applyGradientFilter)
                {
                    tempGradTextures = new RenderTexture[2];
                    tempGradTextures[0] = RenderTexture.GetTemporary(textureRes.x, textureRes.y, 0);
                    tempGradTextures[1] = RenderTexture.GetTemporary(textureRes.x, textureRes.y, 0);
                }

                // filter
                if(filterByBody != null && sensorInt != null)
                {
                    filterByBody.ApplyForegroundFilterByBody(vertexTexture, alphaTexture, playerIndex, sensorIndex, MAX_BODY_COUNT, 
                        sensorInt.GetSensorToWorldMatrix(), kinectManager, foregroundCamera);
                }
                else if(filterByDist != null && sensorInt != null)
                {
                    // filter by distance
                    filterByDist.ApplyVertexFilter(vertexTexture, alphaTexture, sensorInt.GetSensorToWorldMatrix());
                }
                else if(filterByBI != null)
                {
                    // filter by body index
                    filterByBI.ApplyForegroundFilterByBodyIndex(alphaTexture, sensorData, kinectManager, playerIndex, MAX_BODY_COUNT);
                }

                //if(filterByBI == null)
                //{
                //    Graphics.Blit(vertexTexture, alphaTexture);
                //}

                // median
                if (applyMedianFilter)
                {
                    ApplySimpleFilter(alphaTexture, alphaTexture, medianFilterMat, tempTextures);
                }
                //else
                //{
                //    Graphics.Blit(vertexTexture, alphaTexture);
                //}

                // erode0
                ApplyIterableFilter(alphaTexture, alphaTexture, erodeFilterMat, erodeIterations0, tempTextures);
                if(applyGradientFilter)
                {
                    Graphics.CopyTexture(alphaTexture, tempGradTextures[0]);
                }

                // dilate
                ApplyIterableFilter(alphaTexture, alphaTexture, dilateFilterMat, dilateIterations, tempTextures);
                if (applyGradientFilter)
                {
                    //Graphics.Blit(alphaTexture, tempGradTextures[1]);
                    gradientFilterMat.SetTexture("_ErodeTex", tempGradTextures[0]);
                    ApplySimpleFilter(alphaTexture, tempGradTextures[1], gradientFilterMat, tempTextures);
                }

                // erode
                ApplyIterableFilter(alphaTexture, alphaTexture, erodeFilterMat, erodeIterations, tempTextures);
                if (tempGradTextures != null)
                {
                    Graphics.Blit(alphaTexture, tempGradTextures[0]);
                }

                // blur
                if(applyBlurFilter)
                {
                    ApplySimpleFilter(alphaTexture, alphaTexture, blurFilterMat, tempTextures);
                }

                if(invertAlphaMask)
                {
                    ApplySimpleFilter(alphaTexture, alphaTexture, invertAlphaMat, tempTextures);
                }

                if(!computeAlphaMaskOnly)
                {
                    foregroundMat.SetTexture("_ColorTex", colorTexture);
                    foregroundMat.SetTexture("_GradientTex", tempGradTextures[1]);

                    Color gradientColor = (erodeIterations0 != 0 || dilateIterations != 0 || erodeIterations != 0) ? bodyContourColor : Color.clear;
                    foregroundMat.SetColor("_GradientColor", gradientColor);

                    ApplySimpleFilter(alphaTexture, foregroundTexture, foregroundMat, tempTextures);
                }
                else
                {
                    Graphics.CopyTexture(alphaTexture, foregroundTexture);
                }

                // cleanup
                if (tempGradTextures != null)
                {
                    RenderTexture.ReleaseTemporary(tempGradTextures[0]);
                    RenderTexture.ReleaseTemporary(tempGradTextures[1]);
                }

                RenderTexture.ReleaseTemporary(tempTextures[0]);
                RenderTexture.ReleaseTemporary(tempTextures[1]);

                //sensorData.usedColorBodyIndexBufferTime = sensorData.lastColorBodyIndexBufferTime;
            }

            return true;
        }

        // applies iterable filter to the source texture
        private void ApplyIterableFilter(RenderTexture source, RenderTexture destination, Material filterMat, int numIterations, RenderTexture[] tempTextures)
        {
            if (!source || !destination || !filterMat || numIterations == 0)
                return;

            Graphics.Blit(source, tempTextures[0]);

            for (int i = 0; i < numIterations; i++)
            {
                Graphics.Blit(tempTextures[i % 2], tempTextures[(i + 1) % 2], filterMat);
            }

            if ((numIterations % 2) == 0)
            {
                Graphics.Blit(tempTextures[0], destination);
            }
            else
            {
                Graphics.Blit(tempTextures[1], destination);
            }
        }

        // applies simple filter to the source texture
        private void ApplySimpleFilter(RenderTexture source, RenderTexture destination, Material filterMat, RenderTexture[] tempTextures)
        {
            if (!source || !destination || !filterMat)
                return;

            Graphics.Blit(source, tempTextures[0], filterMat);
            Graphics.Blit(tempTextures[0], destination);
        }

    }
}

/*
        // Added by T.Narumi
//        public UnityEngine.UI.RawImage blackboardImage;
        public float update_period = 2.0f;
        public int bboffset_x = 320;
        public int bboffset_y = 400;
        public int bbsubdiv_y = 4;
        private int bboffset_factor = 1;
        private Texture2D blackboardTex;
        private Texture2D bbalphaTex;
        private Color[] bboutColors = null;
        private float currentTime = 0.0f, lastTime = 0.0f;
        private byte[] bbjpeg = null;
        private int jtagcount = 0;
        private K4A_mic_websocket K4A_instance = null;
        private Task task = null;
*/
/*
            // Added by T.Narumi
            Material material = new Material(Shader.Find("Diffuse"));
            this.GetComponent<Renderer>().material = material;
            K4A_instance = K4A_mic_websocket.Instance;
        }

        void OnDestroy()
        */
        /*
        void Update()
        {
            // Added by T.Narumi
            if(K4A_instance.is_mic_client){
                currentTime += Time.deltaTime;
                if(currentTime > lastTime + update_period){
                    lastTime = currentTime;
                    MakeBlackBoardImage();
                }
            } else {
                bbjpeg = K4A_instance.GetBbjpeg();
                K4A_instance.RemoveBbjpeg();
            }
            if(bbjpeg!=null){
                Texture2D tmpTex = new Texture2D(1,1);
                tmpTex.LoadImage(bbjpeg);
//              blackboardImage.texture = tmpTex;
//              blackboardImage.color = Color.white;
                this.GetComponent<Renderer>().material.mainTexture = tmpTex;
                if(jtagcount % 30==0){
                    Debug.Log("Jpegsize=" + bbjpeg.Length);
                }
                jtagcount++;
                bbjpeg=null;
            }
*/