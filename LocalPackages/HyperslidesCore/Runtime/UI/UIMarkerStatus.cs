using NSYNK.HyperSlides.XR;
using UnityEngine;
using TMPro;

namespace NSYNK.HyperSlides.UI
{
    public class UIMarkerStatus : MonoBehaviour
    {
        public XRWorldAnchor.WorldAnchorType type;

        private TextMeshProUGUI markerStatusText;

        private void Awake()
        {
            markerStatusText = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (!XRAnchorManager.Instance || !XRAnchorManager.arSupported)
            {
                markerStatusText.text = "";
                return;
            }

            markerStatusText.text = type == XRWorldAnchor.WorldAnchorType.Position ?
                XRAnchorManager.Instance.positionAnchor.trackedAnchor ? "Position: " + XRAnchorManager.Instance.positionAnchor.trackedAnchor.trackingState.ToString() : "Position anchor missing" :
                XRAnchorManager.Instance.rotationAnchor.trackedAnchor ? "Rotation: " + XRAnchorManager.Instance.rotationAnchor.trackedAnchor.trackingState.ToString() : "Rotation anchor missing" ;        }
    }
}