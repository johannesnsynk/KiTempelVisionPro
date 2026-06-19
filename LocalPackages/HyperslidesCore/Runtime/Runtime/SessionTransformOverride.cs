using System;
using NSYNK.HyperSlides.Network;
using UnityEngine;
using static NSYNK.HyperSlides.Network.XRNetworkObjects;

namespace NSYNK.HyperSlides.Runtime
{
    public class SessionTransformOverride : MonoBehaviour
    {
        public string guid;

        /// <summary>
        /// The starting transform to reset to if no network data is found
        /// </summary>
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 startScale;
        
        public Action OnSessionTransformOverrideUpdated;

        private void Awake()
        {
            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
            startScale = transform.localScale;
        }

        public virtual void OnEnable()
        {
            XRNetworkManager.Instance.OnMatchJoined += UpdateTransformFromNetwork;
            XRNetworkManager.Instance.OnNetworkSlideUpdate += UpdateTransformFromNetwork;
            XRNetworkManager.Instance.OnSessionTransformOverride += UpdateTransformFromNetwork;

            UpdateTransformFromNetwork();
        }

        public virtual void OnDisable()
        {
            if (XRNetworkManager.Instance == null)
                return;
                
            XRNetworkManager.Instance.OnMatchJoined -= UpdateTransformFromNetwork;
            XRNetworkManager.Instance.OnNetworkSlideUpdate -= UpdateTransformFromNetwork;
            XRNetworkManager.Instance.OnSessionTransformOverride -= UpdateTransformFromNetwork;
        }

        private void OnValidate() => CheckForGUID();
        private void Reset() => CheckForGUID();

        /// <summary>
        /// Autogenerate a GUID to have it static over the course of creation
        /// </summary>
        private void CheckForGUID()
        {
            if (string.IsNullOrEmpty(guid))
                guid = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Simplified overload to match the XRSessionState callback
        /// </summary>
        /// <param name="state"></param>
        private void UpdateTransformFromNetwork(XRSessionState state) => UpdateTransformFromNetwork();

        /// <summary>
        /// Update the transform from the networked session overrides or reset to start if none found
        /// </summary>
        private void UpdateTransformFromNetwork()
        {
            XRNetworkObjects.SessionTransformOverride foundRoot = XRNetworkManager.Instance.SessionTransformOverrides.Find(r => r.guid == guid);

            if (foundRoot != null)
            {
                // Debug.Log($"Found session transform override for {guid}", this);
                transform.SetLocalPositionAndRotation(foundRoot.localPosition, Quaternion.Euler(foundRoot.localRotation));
                transform.localScale = foundRoot.localScale;
            }
            else
            {
                // Debug.Log($"No session transform override found for {guid}, resetting to start", this);
                transform.SetLocalPositionAndRotation(startPosition, startRotation);
                transform.localScale = startScale;
            }

            OnSessionTransformOverrideUpdated?.Invoke();

        }
    }
}
