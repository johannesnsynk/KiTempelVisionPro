using System;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    public class SessionTransformOverride : MonoBehaviour
    {
        public string guid;

        public virtual void OnEnable()
        {
            XRNetworkManager.onMatchJoined += UpdateTransformFromNetwork;
            XRNetworkManager.onNetworkSlideUpdate += UpdateTransformFromNetwork;
            XRNetworkManager.onSessionTransformOverride += UpdateTransformFromNetwork;

            UpdateTransformFromNetwork();
        }

        public virtual void OnDisable()
        {
            XRNetworkManager.onMatchJoined -= UpdateTransformFromNetwork;
            XRNetworkManager.onNetworkSlideUpdate -= UpdateTransformFromNetwork;
            XRNetworkManager.onSessionTransformOverride -= UpdateTransformFromNetwork;
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

        private void UpdateTransformFromNetwork(XRNetworkObjects.XRSessionState state) => UpdateTransformFromNetwork();
        private void UpdateTransformFromNetwork()
        {
            XRNetworkObjects.SessionTransformOverride foundRoot = XRNetworkManager.sessionTransformOverrides.Find(r => r.guid == guid);

            if (foundRoot != null)
            {
                transform.SetLocalPositionAndRotation(foundRoot.localPosition, Quaternion.Euler(foundRoot.localRotation));
                transform.localScale = foundRoot.localScale;
            }
            else
                Debug.LogWarning("No network root on match label for: " + guid, this);
        }
    }
}
