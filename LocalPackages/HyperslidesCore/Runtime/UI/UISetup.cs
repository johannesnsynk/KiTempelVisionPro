using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;

using System.Collections.Generic;

using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    public class UISetup : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI deviceName;
        public List<GameObject> moderatorSpecificUI;

        private void OnEnable()
        {
            XRNetworkManager.onUserConnected += UpdateUIItems;
            UpdateUIItems();
        }

        private void OnDisable()
        {
            XRNetworkManager.onUserConnected -= UpdateUIItems;
        }

        private void UpdateUIItems()
        {
            moderatorSpecificUI.ForEach(ui => ui.SetActive(DeviceInfo.Role >= XRPlayer.Role.Moderator));
            deviceName.text = $"({PlayerPrefs.GetString("sessionCode", "")}) {PlayerPrefs.GetString("DeviceName", "")}";
        }
    }
}