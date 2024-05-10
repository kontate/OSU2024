using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;


namespace com.rfilkov.kinect
{
    // predefined smoothing types
    public enum SmoothingType : int { None, Default, Light, Medium, Aggressive }


    /// <summary>
    /// Parameters used for smoothing of the body-joint positions between frames.
    /// </summary>
    public class SmoothParameters
    {
        public float smoothing;
        public float correction;
        public float prediction;
        public float jitterRadius;
        public float maxDeviationRadius;
    }


    /// <summary>
    /// Implementation of a Holt Double Exponential Smoothing filter. The double exponential
    /// smooths the curve and predicts.  There is also noise jitter removal. And maximum
    /// prediction bounds.  The parameters are commented in the Init function.
    /// </summary>
    public class JointPositionsFilter
    {
        // The history data.
        private FilterDoubleExponentialData[,] history;

        // The smoothing parameters for this filter.
        private SmoothParameters smoothParameters;
        private SmoothingType smoothingType = SmoothingType.Default;

        // True when the filter parameters are initialized.
        private bool init;


        /// Initializes a new instance of the class.
        public JointPositionsFilter()
        {
            init = false;
        }

        // Initialize the filter with a default set of TransformSmoothParameters.
        public void Init()
        {
            // Specify some defaults
            Init(0.5f, 0.5f, 0.5f, 0.05f, 0.04f);
        }

        /// <summary>
        /// Initialize the filter with a set of manually specified TransformSmoothParameters.
        /// </summary>
        /// <param name="smoothingValue">Smoothing = [0..1], lower values is closer to the raw data and more noisy.</param>
        /// <param name="correctionValue">Correction = [0..1], higher values correct faster and feel more responsive.</param>
        /// <param name="predictionValue">Prediction = [0..n], how many frames into the future we want to predict.</param>
        /// <param name="jitterRadiusValue">JitterRadius = The deviation distance in m that defines jitter.</param>
        /// <param name="maxDeviationRadiusValue">MaxDeviation = The maximum distance in m that filtered positions are allowed to deviate from raw data.</param>
        public void Init(float smoothingValue, float correctionValue, float predictionValue, float jitterRadiusValue, float maxDeviationRadiusValue)
        {
            this.smoothingType = SmoothingType.Default; 
            smoothParameters = new SmoothParameters();

            smoothParameters.smoothing = smoothingValue;                   // How much soothing will occur.  Will lag when too high
            smoothParameters.correction = correctionValue;                 // How much to correct back from prediction.  Can make things springy
            smoothParameters.prediction = predictionValue;                 // Amount of prediction into the future to use. Can over shoot when too high
            smoothParameters.jitterRadius = jitterRadiusValue;             // Size of the radius where jitter is removed. Can do too much smoothing when too high
            smoothParameters.maxDeviationRadius = maxDeviationRadiusValue; // Size of the max prediction radius Can snap back to noisy data when too high

            // Check for divide by zero. Use an epsilon of a 10th of a millimeter
            smoothParameters.jitterRadius = Math.Max(0.0001f, this.smoothParameters.jitterRadius);

            Reset();
            init = true;
        }

        // Initialize the filter with a set of SmoothParameters.
        public void Init(SmoothParameters smoothParameters)
        {
            this.smoothingType = SmoothingType.Default;
            this.smoothParameters = smoothParameters;

            Reset();
            init = true;
        }

        // Initialize the filter with a set of SmoothParameters.
        public void Init(SmoothingType smoothingType)
        {
            this.smoothingType = smoothingType;
            smoothParameters = new SmoothParameters();

            switch (smoothingType)
            {
                case SmoothingType.Light:
                    smoothParameters.smoothing = 0.3f;
                    smoothParameters.correction = 0.35f;
                    smoothParameters.prediction = 0.35f;
                    smoothParameters.jitterRadius = 0.15f;
                    smoothParameters.maxDeviationRadius = 0.15f;
                    break;
                case SmoothingType.Medium:
                    smoothParameters.smoothing = 0.5f;
                    smoothParameters.correction = 0.1f;
                    smoothParameters.prediction = 0.5f;
                    smoothParameters.jitterRadius = 0.1f;
                    smoothParameters.maxDeviationRadius = 0.1f;
                    break;
                case SmoothingType.Aggressive:
                    smoothParameters.smoothing = 0.7f;
                    smoothParameters.correction = 0.3f;
                    smoothParameters.prediction = 1.0f;
                    smoothParameters.jitterRadius = 1.0f;
                    smoothParameters.maxDeviationRadius = 1.0f;
                    break;

                //case SmoothingType.Default:
                default:
                    smoothParameters.smoothing = 0.5f;
                    smoothParameters.correction = 0.5f;
                    smoothParameters.prediction = 0.5f;
                    smoothParameters.jitterRadius = 0.05f;
                    smoothParameters.maxDeviationRadius = 0.04f;
                    break;
            }

            Reset();
            init = true;
        }

        // Resets the filter to default values.
        public void Reset()
        {
            KinectManager kinectManager = KinectManager.Instance;
            history = new FilterDoubleExponentialData[kinectManager.GetMaxBodyCount(), kinectManager.GetJointCount()];
            //Debug.Log("BodyCount: " + kinectManager.GetMaxBodyCount() + ", JointCount: " + kinectManager.GetJointCount());
        }

        //// Update the filter with a new frame of data and smooth.
        //public void UpdateFilter(ref KinectInterop.BodyData[] alTrackedBodies)
        //{
        //    if (!init)
        //    {
        //        // initialize with by-default parameters
        //        Init();              
        //    }

        //    if (smoothingType == SmoothingType.None)
        //        return;

        //    SmoothParameters tempSmoothingParams = new SmoothParameters();
        //    tempSmoothingParams.smoothing = this.smoothParameters.smoothing;
        //    tempSmoothingParams.correction = this.smoothParameters.correction;
        //    tempSmoothingParams.prediction = this.smoothParameters.prediction;

        //    int bodyCount = alTrackedBodies != null ? alTrackedBodies.Length : 0;
        //    for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++)
        //    {
        //        if (alTrackedBodies[bodyIndex].bIsTracked)
        //        {
        //            FilterBodyJoints(ref alTrackedBodies[bodyIndex], /**bodyIndex*/ alTrackedBodies[bodyIndex].iBodyIndex, tempSmoothingParams);
        //        }
        //    }
        //}

        // Update the filter with a new frame of data and smooth.
        public void UpdateFilter(ref KinectInterop.BodyData bodyData)
        {
            if (!init)
            {
                // initialize with by-default parameters
                Init();
            }

            if (smoothingType == SmoothingType.None)
                return;

            SmoothParameters tempSmoothingParams = new SmoothParameters();
            tempSmoothingParams.smoothing = this.smoothParameters.smoothing;
            tempSmoothingParams.correction = this.smoothParameters.correction;
            tempSmoothingParams.prediction = this.smoothParameters.prediction;

            if (bodyData.bIsTracked)
            {
                FilterBodyJoints(ref bodyData, bodyData.iBodyIndex, tempSmoothingParams);
            }
        }

        // Update the filter for all body joints
        private void FilterBodyJoints(ref KinectInterop.BodyData bodyData, int bodyIndex, SmoothParameters tempSmoothingParams)
        {
            KinectManager manager = KinectManager.Instance;
            int jointsCount = manager.GetJointCount();

            for (int jointIndex = 0; jointIndex < jointsCount; jointIndex++)
            {
                // If not tracked, we smooth a bit more by using a bigger jitter radius
                // Always filter end joints highly as they are more noisy
                if (bodyData.joint[jointIndex].trackingState != KinectInterop.TrackingState.Tracked ||
                    jointIndex == (int)KinectInterop.JointType.FootLeft || jointIndex == (int)KinectInterop.JointType.FootRight ||
                    jointIndex == (int)KinectInterop.JointType.HandtipLeft || jointIndex == (int)KinectInterop.JointType.HandtipRight ||
                    jointIndex == (int)KinectInterop.JointType.ThumbLeft || jointIndex == (int)KinectInterop.JointType.ThumbRight ||
                    jointIndex == (int)KinectInterop.JointType.Head)
                {
                    tempSmoothingParams.jitterRadius = this.smoothParameters.jitterRadius * 2.0f;
                    tempSmoothingParams.maxDeviationRadius = smoothParameters.maxDeviationRadius * 2.0f;
                }
                else
                {
                    tempSmoothingParams.jitterRadius = smoothParameters.jitterRadius;
                    tempSmoothingParams.maxDeviationRadius = smoothParameters.maxDeviationRadius;
                }

                bodyData.joint[jointIndex].position = FilterJoint(bodyData.joint[jointIndex].position, bodyIndex, jointIndex, tempSmoothingParams);
            }

            for (int jointIndex = 0; jointIndex < jointsCount; jointIndex++)
            {
                if (jointIndex == 0)
                {
                    bodyData.position = bodyData.joint[jointIndex].position;
                    bodyData.orientation = bodyData.joint[jointIndex].orientation;

                    bodyData.joint[jointIndex].direction = Vector3.zero;
                }
                else
                {
                    int jParent = (int)manager.GetParentJoint((KinectInterop.JointType)jointIndex);

                    if (bodyData.joint[jointIndex].trackingState != KinectInterop.TrackingState.NotTracked &&
                       bodyData.joint[jParent].trackingState != KinectInterop.TrackingState.NotTracked)
                    {
                        bodyData.joint[jointIndex].direction =
                            bodyData.joint[jointIndex].position - bodyData.joint[jParent].position;
                    }
                }
            }
        }

        // Update the filter for one joint
        private Vector3 FilterJoint(Vector3 rawPosition, int bodyIndex, int jointIndex, SmoothParameters smoothingParameters)
        {
            Vector3 filteredPosition;
            Vector3 diffVec;
            Vector3 trend;
            float diffVal;

            Vector3 prevFilteredPosition = history[bodyIndex, jointIndex].filteredPosition;
            Vector3 prevTrend = this.history[bodyIndex, jointIndex].trend;
            Vector3 prevRawPosition = this.history[bodyIndex, jointIndex].rawPosition;
            bool jointIsValid = (rawPosition != Vector3.zero);

            // If joint is invalid, reset the filter
            if (!jointIsValid)
            {
                history[bodyIndex, jointIndex].frameCount = 0;
            }

            // Initial start values
            if (this.history[bodyIndex, jointIndex].frameCount == 0)
            {
                filteredPosition = rawPosition;
                trend = Vector3.zero;
            }
            else if (this.history[bodyIndex, jointIndex].frameCount == 1)
            {
                filteredPosition = (rawPosition + prevRawPosition) * 0.5f;
                diffVec = filteredPosition - prevFilteredPosition;
                trend = (diffVec * smoothingParameters.correction) + (prevTrend * (1.0f - smoothingParameters.correction));
            }
            else
            {
                // First apply jitter filter
                diffVec = rawPosition - prevFilteredPosition;
                diffVal = Math.Abs(diffVec.magnitude);

                if (diffVal <= smoothingParameters.jitterRadius)
                {
                    filteredPosition = (rawPosition * (diffVal / smoothingParameters.jitterRadius)) + (prevFilteredPosition * (1.0f - (diffVal / smoothingParameters.jitterRadius)));
                }
                else
                {
                    filteredPosition = rawPosition;
                }

                // Now the double exponential smoothing filter
                filteredPosition = (filteredPosition * (1.0f - smoothingParameters.smoothing)) + ((prevFilteredPosition + prevTrend) * smoothingParameters.smoothing);

                diffVec = filteredPosition - prevFilteredPosition;
                trend = (diffVec * smoothingParameters.correction) + (prevTrend * (1.0f - smoothingParameters.correction));
            }

            // Predict into the future to reduce latency
            Vector3 predictedPosition = filteredPosition + (trend * smoothingParameters.prediction);

            // Check that we are not too far away from raw data
            diffVec = predictedPosition - rawPosition;
            diffVal = Mathf.Abs(diffVec.magnitude);

            if (diffVal > smoothingParameters.maxDeviationRadius)
            {
                predictedPosition = (predictedPosition * (smoothingParameters.maxDeviationRadius / diffVal)) + (rawPosition * (1.0f - (smoothingParameters.maxDeviationRadius / diffVal)));
            }

            // Save the data from this frame
            history[bodyIndex, jointIndex].rawPosition = rawPosition;
            history[bodyIndex, jointIndex].filteredPosition = filteredPosition;
            history[bodyIndex, jointIndex].trend = trend;
            history[bodyIndex, jointIndex].frameCount++;

            return predictedPosition;
        }


        // Historical Filter Data.  
        private struct FilterDoubleExponentialData
        {
            // Gets or sets Historical Position.  
            public Vector3 rawPosition;

            // Gets or sets Historical Filtered Position.  
            public Vector3 filteredPosition;

            // Gets or sets Historical Trend.  
            public Vector3 trend;

            // Gets or sets Historical FrameCount.  
            public uint frameCount;
        }
    }
}

