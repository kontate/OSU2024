﻿using UnityEngine;
using System.Collections;
using com.rfilkov.kinect;
using System;

namespace com.rfilkov.components
{
    /// <summary>
    /// BackgroundColorCamUserImage is component that displays the color camera aligned user-body image on RawImage texture, usually the scene background.
    /// </summary>
    public class BackgroundColorCamUserImage : MonoBehaviour
    {
        [Tooltip("Depth sensor index - 0 is the 1st one, 1 - the 2nd one, etc.")]
        public int sensorIndex = 0;

        [Tooltip("RawImage used to display the color camera feed.")]
        public UnityEngine.UI.RawImage backgroundImage;

        [Tooltip("Camera used to display the background image. Set it, if you'd like to allow background image to resize, to match the color image's aspect ratio.")]
        public Camera backgroundCamera;


        // last camera rect width & height
        private float lastCamRectW = 0;
        private float lastCamRectH = 0;

        // reference to the kinectManager
        private KinectManager kinectManager = null;
        private KinectInterop.SensorData sensorData = null;

        // color-camera aligned frames
        private ulong lastColorCamDepthFrameTime = 0;
        private ulong lastColorCamBodyIndexFrameTime = 0;

        // color-camera aligned texture and buffers
        private RenderTexture bodyImageTexture = null;
        private Material bodyImageMaterial = null;

        private ComputeBuffer bodyIndexBuffer = null;
        private ComputeBuffer depthImageBuffer = null;
        private ComputeBuffer bodyHistBuffer = null;

        // body image hist data
        protected int[] depthBodyBufferData = null;
        protected int[] equalBodyBufferData = null;
        protected int bodyHistTotalPoints = 0;


        void Start()
        {
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<UnityEngine.UI.RawImage>();
            }

            kinectManager = KinectManager.Instance;
            sensorData = kinectManager != null ? kinectManager.GetSensorData(sensorIndex) : null;

            if(sensorData != null)
            {
                // enable the color camera aligned depth & body-index frames 
                sensorData.sensorInterface.EnableColorCameraDepthFrame(sensorData, true);
                sensorData.sensorInterface.EnableColorCameraBodyIndexFrame(sensorData, true);

                // create the user texture and needed buffers
                bodyImageTexture = KinectInterop.CreateRenderTexture(bodyImageTexture, sensorData.colorImageWidth, sensorData.colorImageHeight);
                bodyImageMaterial = new Material(Shader.Find("Kinect/UserHistImageShader"));

                int bodyIndexBufferLength = sensorData.colorImageWidth * sensorData.colorImageHeight >> 2;
                bodyIndexBuffer = KinectInterop.CreateComputeBuffer(bodyIndexBuffer, bodyIndexBufferLength, sizeof(uint));

                int depthBufferLength = sensorData.colorImageWidth * sensorData.colorImageHeight >> 1;
                depthImageBuffer = KinectInterop.CreateComputeBuffer(depthImageBuffer, depthBufferLength, sizeof(uint));

                bodyHistBuffer = KinectInterop.CreateComputeBuffer(bodyHistBuffer, DepthSensorBase.MAX_DEPTH_DISTANCE_MM + 1, sizeof(int));

                depthBodyBufferData = new int[DepthSensorBase.MAX_DEPTH_DISTANCE_MM + 1];
                equalBodyBufferData = new int[DepthSensorBase.MAX_DEPTH_DISTANCE_MM + 1];
            }
        }


        void OnDestroy()
        {
            if (bodyImageTexture)
            {
                bodyImageTexture.Release();
                bodyImageTexture = null;
            }

            if (bodyIndexBuffer != null)
            {
                bodyIndexBuffer.Dispose();
                bodyIndexBuffer = null;
            }

            if (depthImageBuffer != null)
            {
                depthImageBuffer.Dispose();
                depthImageBuffer = null;
            }

            if (bodyHistBuffer != null)
            {
                bodyHistBuffer.Dispose();
                bodyHistBuffer = null;
            }

            if (sensorData != null)
            {
                // disable the color camera aligned depth & body-index frames 
                sensorData.sensorInterface.EnableColorCameraDepthFrame(sensorData, false);
                sensorData.sensorInterface.EnableColorCameraBodyIndexFrame(sensorData, false);
            }
        }


        void Update()
        {
            if (kinectManager && kinectManager.IsInitialized() && sensorData != null)
            {
                float cameraWidth = backgroundCamera ? backgroundCamera.pixelRect.width : 0f;
                float cameraHeight = backgroundCamera ? backgroundCamera.pixelRect.height : 0f;

                // check for new color camera aligned frames
                UpdateTextureWithNewFrame();

                if (backgroundImage && (backgroundImage.texture == null || lastCamRectW != cameraWidth || lastCamRectH != cameraHeight))
                {
                    lastCamRectW = cameraWidth;
                    lastCamRectH = cameraHeight;

                    backgroundImage.texture = bodyImageTexture;
                    backgroundImage.rectTransform.localScale = kinectManager.GetColorImageScale(sensorIndex);
                    backgroundImage.color = Color.white;

                    if (backgroundCamera != null)
                    {
                        // adjust image's size and position to match the stream aspect ratio
                        int colorImageWidth = kinectManager.GetColorImageWidth(sensorIndex);
                        int colorImageHeight = kinectManager.GetColorImageHeight(sensorIndex);

                        RectTransform rectImage = backgroundImage.rectTransform;
                        float rectWidth = (rectImage.anchorMin.x != rectImage.anchorMax.x) ? cameraWidth * (rectImage.anchorMax.x - rectImage.anchorMin.x) : rectImage.sizeDelta.x;
                        float rectHeight = (rectImage.anchorMin.y != rectImage.anchorMax.y) ? cameraHeight * (rectImage.anchorMax.y - rectImage.anchorMin.y) : rectImage.sizeDelta.y;

                        if (colorImageWidth > colorImageHeight)
                            rectWidth = rectHeight * colorImageWidth / colorImageHeight;
                        else
                            rectHeight = rectWidth * colorImageHeight / colorImageWidth;

                        Vector2 pivotOffset = (rectImage.pivot - new Vector2(0.5f, 0.5f)) * 2f;
                        Vector2 imageScale = (Vector2)kinectManager.GetColorImageScale(sensorIndex);
                        Vector2 anchorPos = rectImage.anchoredPosition + pivotOffset * imageScale * new Vector2(rectWidth, rectHeight);

                        if (rectImage.anchorMin.x != rectImage.anchorMax.x)
                        {
                            rectWidth = -(cameraWidth - rectWidth);
                        }

                        if (rectImage.anchorMin.y != rectImage.anchorMax.y)
                        {
                            rectHeight = -(cameraHeight - rectHeight);
                        }

                        rectImage.sizeDelta = new Vector2(rectWidth, rectHeight);
                        rectImage.anchoredPosition = anchorPos;
                    }
                }
            }
        }


        // checks for new color-camera aligned frames, and composes an updated body-index texture, if needed
        private void UpdateTextureWithNewFrame()
        {
            if (sensorData == null || sensorData.sensorInterface == null)
                return;

            // get body index frame
            if (lastColorCamDepthFrameTime != sensorData.lastColorCamDepthFrameTime || lastColorCamBodyIndexFrameTime != sensorData.lastColorCamBodyIndexFrameTime)
            {
                lastColorCamDepthFrameTime = sensorData.lastColorCamDepthFrameTime;
                lastColorCamBodyIndexFrameTime = sensorData.lastColorCamBodyIndexFrameTime;

                Array.Clear(depthBodyBufferData, 0, depthBodyBufferData.Length);
                Array.Clear(equalBodyBufferData, 0, equalBodyBufferData.Length);
                bodyHistTotalPoints = 0;

                // get configured min & max distances 
                float minDistance = ((DepthSensorBase)sensorData.sensorInterface).minDistance;
                float maxDistance = ((DepthSensorBase)sensorData.sensorInterface).maxDistance;

                int depthMinDistance = (int)(minDistance * 1000f);
                int depthMaxDistance = (int)(maxDistance * 1000f);

                int frameLen = sensorData.colorCamDepthImage.Length;
                for (int i = 0; i < frameLen; i++)
                {
                    int depth = sensorData.colorCamDepthImage[i];
                    int limDepth = (depth >= depthMinDistance && depth <= depthMaxDistance) ? depth : 0;

                    if (/**rawBodyIndexImage[i] != 255 &&*/ limDepth > 0)
                    {
                        depthBodyBufferData[limDepth]++;
                        bodyHistTotalPoints++;
                    }
                }

                if (bodyHistTotalPoints > 0)
                {
                    equalBodyBufferData[0] = depthBodyBufferData[0];
                    for (int i = 1; i < depthBodyBufferData.Length; i++)
                    {
                        equalBodyBufferData[i] = equalBodyBufferData[i - 1] + depthBodyBufferData[i];
                    }
                }

                if (bodyIndexBuffer != null && sensorData.colorCamBodyIndexImage != null)
                {
                    int bodyIndexBufferLength = sensorData.colorCamBodyIndexImage.Length >> 2;
                    KinectInterop.SetComputeBufferData(bodyIndexBuffer, sensorData.colorCamBodyIndexImage, bodyIndexBufferLength, sizeof(uint));
                }

                if (depthImageBuffer != null && sensorData.colorCamDepthImage != null)
                {
                    int depthBufferLength = sensorData.colorCamDepthImage.Length >> 1;
                    KinectInterop.SetComputeBufferData(depthImageBuffer, sensorData.colorCamDepthImage, depthBufferLength, sizeof(uint));
                }

                if (bodyHistBuffer != null)
                {
                    KinectInterop.SetComputeBufferData(bodyHistBuffer, equalBodyBufferData, equalBodyBufferData.Length, sizeof(int));
                }

                float minDist = kinectManager.minUserDistance != 0f ? kinectManager.minUserDistance : minDistance;
                float maxDist = kinectManager.maxUserDistance != 0f ? kinectManager.maxUserDistance : maxDistance;

                bodyImageMaterial.SetInt("_TexResX", sensorData.colorImageWidth);
                bodyImageMaterial.SetInt("_TexResY", sensorData.colorImageHeight);
                bodyImageMaterial.SetInt("_MinDepth", (int)(minDist * 1000f));
                bodyImageMaterial.SetInt("_MaxDepth", (int)(maxDist * 1000f));

                bodyImageMaterial.SetBuffer("_BodyIndexMap", bodyIndexBuffer);
                bodyImageMaterial.SetBuffer("_DepthMap", depthImageBuffer);
                bodyImageMaterial.SetBuffer("_HistMap", bodyHistBuffer);
                bodyImageMaterial.SetInt("_TotalPoints", bodyHistTotalPoints);

                Color[] bodyIndexColors = kinectManager.GetBodyIndexColors();
                bodyImageMaterial.SetColorArray("_BodyIndexColors", bodyIndexColors);

                Graphics.Blit(null, bodyImageTexture, bodyImageMaterial);
            }


        }

    }
}

