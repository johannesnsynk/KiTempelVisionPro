using System.Collections;
using System.Collections.Generic;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.XR;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// A root gameobject, that every content should be attached to, so we can anchor this to AR worldanchors
    /// </summary>
    public class XRContentRoot : Singleton<XRContentRoot>
    {
        [Header("iOS Filtering Settings")]
        [SerializeField] private float positionJumpThreshold = 0.15f;
        [SerializeField] private float positionIgnoreThreshold = 0.02f;
        [SerializeField] private float rotationJumpThreshold = 45f;
        [SerializeField] private float rotationIgnoreThreshold = 0.5f;

        private NSYNK.KalmanFilter positionKalman;
        private NSYNK.QuaternionKalmanFilter rotationKalman;
        private bool filtersInitialized = false;
        private Vector3 lastFilteredPosition;
        private Quaternion lastFilteredRotation;

        void Start()
        {
#if UNITY_IOS
            InitializeFilters();
#endif
        }

        private void InitializeFilters()
        {
            Vector3 initialPosition = transform.position;
            Quaternion initialRotation = transform.rotation;

            positionKalman = new KalmanFilter(0.01f, 0.1f, initialPosition, Vector3.one);
            rotationKalman = new QuaternionKalmanFilter(0.01f, 0.1f, initialRotation, 1f);

            lastFilteredPosition = initialPosition;
            lastFilteredRotation = initialRotation;
            filtersInitialized = true;
        }

        public void Update()
        {
            if (DeviceInfo.Role == XRPlayer.Role.Simulation)
            {
                transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return;
            }
#if UNITY_IOS
            if (!filtersInitialized)
                InitializeFilters();


            transform.position = FilterPosition(XRAnchorManager.Instance.positionAnchor.transform.position);

            transform.rotation = FilterRotation(GetTargetRotation());
#else
            transform.position = XRAnchorManager.Instance.positionAnchor.transform.position;

            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Anchors && XRAnchorManager.Instance.rotationAnchor.IsTracking())
                transform.LookAt(XRAnchorManager.Instance.rotationAnchor.transform.position);
            else if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Image)
                transform.rotation = XRAnchorManager.Instance.positionAnchor.transform.rotation;
            else
                transform.rotation = Quaternion.identity;
#endif
        }

        public Vector3 FilterPosition(Vector3 targetPosition)
        {
            float distance = Vector3.Distance(lastFilteredPosition, targetPosition);

            // Jump detection
            if (distance > positionJumpThreshold)
            {
                // reinitialize kalman
                positionKalman = new KalmanFilter(0.01f, 0.1f, targetPosition, Vector3.one);

                lastFilteredPosition = targetPosition;
                return targetPosition;
            }
            // Ignore small movements
            else if (distance < positionIgnoreThreshold)
            {
                return lastFilteredPosition;
            }
            // apply kalman filtering for medium movements
            else
            {
                Vector3 filteredPosition = positionKalman.Update(targetPosition);
                lastFilteredPosition = filteredPosition;
                return filteredPosition;
            }
        }

        public Quaternion FilterRotation(Quaternion targetRotation)
        {
            float angle = Quaternion.Angle(lastFilteredRotation, targetRotation);

            //Jump detection
            if (angle > rotationJumpThreshold)
            {
                rotationKalman = new QuaternionKalmanFilter(0.01f, 0.1f, targetRotation, 1f);
                lastFilteredRotation = targetRotation;
                return targetRotation;
            }
            // ignore small rotation
            else if (angle < rotationIgnoreThreshold)
            {
                return lastFilteredRotation;
            }
            // filter medium rotation
            else
            {
                Quaternion filteredRotation = rotationKalman.Update(targetRotation);
                lastFilteredRotation = filteredRotation;
                return filteredRotation;
            }
        }

        private Quaternion GetTargetRotation()
        {
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Anchors && XRAnchorManager.Instance.rotationAnchor.IsTracking())
            {
                Vector3 lookDirection = XRAnchorManager.Instance.rotationAnchor.transform.position - transform.position;
                if (lookDirection.magnitude > 0.001f)
                    return Quaternion.LookRotation(lookDirection);
                else
                {
                    return Quaternion.LookRotation(1000f * lookDirection);
                }
            }
            else if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Image)
                return XRAnchorManager.Instance.positionAnchor.transform.rotation;
            else
                return Quaternion.identity;
        }
    }
}