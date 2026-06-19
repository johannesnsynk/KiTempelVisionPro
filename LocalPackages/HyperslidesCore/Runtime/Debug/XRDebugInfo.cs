using UnityEngine;
using TMPro;
using System;

namespace NSYNK.HyperSlides.Core.Utilities
{
    /// <summary>
    /// This class is used to display debug information about the XR object.
    /// It can show the world and local position, rotation, and scale of the object,
    /// as well as the network synced position if the object is network synced.
    /// </summary>
    public class XRDebugInfo : MonoBehaviour
    {
        /// <summary>
        /// This delegate is used to toggle the XR debug information on and off.
        /// </summary>
        /// <param name="enable"></param>
        public delegate void SwitchXRDebug(bool enable);
        /// <summary>
        /// This event is used to toggle the XR debug information on and off.
        /// </summary>
        public static SwitchXRDebug enableXRDebug;
        /// <summary>
        /// This is the object that you want to debug.
        /// </summary>
        public GameObject debugObject;
        /// <summary>
        /// This is a flag to enable or disable the debug information for the transform.
        /// </summary>
        public bool debugTransform;
        /// <summary>
        /// This is a flag to enable or disable the debug information for the network synced position.
        /// </summary>
        public bool debugNetworkSync;

        private GameObject canvas;
        private bool showDebug = true;
        private TextMeshProUGUI debugInfoText;
        private Network.NetworkSynced debugSyncedComp;
        private DateTime lastUpdateTime;

        private void Awake()
        {
            canvas = GetComponentInChildren<Canvas>().gameObject;
            debugInfoText = GetComponentInChildren<TextMeshProUGUI>();
            debugSyncedComp = debugObject.GetComponent<Network.NetworkSynced>();
        }

        private void OnEnable() => enableXRDebug += OnSwitchXRDebug;
        private void OnDisable() => enableXRDebug -= OnSwitchXRDebug;

        /// <summary>
        /// This method is called when the XR debug switch is toggled.
        /// </summary>
        /// <param name="enable"></param>
        private void OnSwitchXRDebug(bool enable)
        {
            showDebug = enable;

            canvas.SetActive(enable);
        }

        private void FixedUpdate()
        {
            if (!showDebug || debugObject == null)
                return;

            string debugString = "";

            if (debugTransform)
            {
                debugString += $"Local Pos: {debugObject.transform.localPosition}";
            }

            if (debugNetworkSync && debugSyncedComp)
            {
                lastUpdateTime = debugSyncedComp.syncedTransform.timeStamp;
                float latencyMilliseconds = (float)(DateTime.UtcNow - lastUpdateTime).TotalMilliseconds;

                if (debugSyncedComp.syncedTransform.state == Network.XRNetworkObjects.NetworkSyncedTransform.NetworkState.AVAILABLE)
                    latencyMilliseconds = 0f;

                debugString += $"\n\nNETWORK SYNC: {debugSyncedComp.syncedTransform.guid}";
                debugString += $"\nPos: {debugSyncedComp.syncedTransform.localPosition}";
                debugString += $"\nOwner: {debugSyncedComp.syncedTransform.owner}";
                debugString += $"\nState: {debugSyncedComp.syncedTransform.state}";
                debugString += $"\nUpdate Latency: {latencyMilliseconds} ms";
            }

            debugInfoText.text = debugString;
        }
    }
}