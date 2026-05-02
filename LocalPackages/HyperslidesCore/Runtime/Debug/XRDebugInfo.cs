using UnityEngine;
using TMPro;

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

        private void Awake()
        {
            canvas = GetComponentInChildren<Canvas>().gameObject;
            debugInfoText = GetComponentInChildren<TextMeshProUGUI>();
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
                debugString += $"World Pos: {debugObject.transform.position}\nWorld Rot: {debugObject.transform.rotation}\nWorld Scale: {debugObject.transform.localScale}\n\n";
                debugString += $"Local Pos: {debugObject.transform.localPosition}\nLocal Rot: {debugObject.transform.localRotation}\nLocal Scale: {debugObject.transform.localScale}";
            }

            if (debugNetworkSync && debugObject.TryGetComponent(out Network.NetworkSynced syncedComp))
            {
                debugString += $"\n\nNetwork Synced Pos: {syncedComp.syncedTransform.localPosition}";
            }

            debugInfoText.text = debugString;
        }
    }
}