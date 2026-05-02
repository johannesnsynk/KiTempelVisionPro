using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.XR;

using UnityEngine;
using UnityEngine.UI;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides
{
    public class XRAdminManager : Singleton<XRAdminManager>
    {
        public Button roleButton, trackingButton;

        private void OnEnable()
        {
            XRNetworkManager.onUserConnected += UpdateTextfields;
            XRAnchorManager.onTrackingUpdate += UpdateTextfields;

            if (trackingButton)
                trackingButton.onClick.AddListener(ChooseNextTrackingType);
        }

        private void OnDisable()
        {
            XRNetworkManager.onUserConnected -= UpdateTextfields;
            XRAnchorManager.onTrackingUpdate -= UpdateTextfields;

            if (roleButton)
                roleButton.onClick.RemoveAllListeners();

            if (trackingButton)
                trackingButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            UpdateTextfields();
        }

        private void UpdateTextfields()
        {
            //Debug.Log("ROLE OR TRACKING UPDATE: " + DeviceInfo.Role.ToString() + " / " + RuntimeHandler.Settings.trackingType.ToString());
            if (roleButton)
            {
                roleButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = DeviceInfo.Role.ToString();
                LayoutRebuilder.ForceRebuildLayoutImmediate(roleButton.GetComponent<RectTransform>());
            }

            if (trackingButton)
            {
                trackingButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = RuntimeHandler.Settings.trackingType.ToString();
                LayoutRebuilder.ForceRebuildLayoutImmediate(trackingButton.GetComponent<RectTransform>());
            }
        }

        public static void ChooseTrackingType(Settings.TrackingType trackingType)
        {
            RuntimeHandler.Settings.trackingType = trackingType;

            XRAnchorManager.Instance.StartOverAnchorSetup();
        }

        public void ChooseNextTrackingType()
        {
            Settings.TrackingType nextTrackingType = RuntimeHandler.Settings.trackingType < Settings.TrackingType.Image ? RuntimeHandler.Settings.trackingType + 1 : 0;

            ChooseTrackingType(nextTrackingType);
        }
    }
}