using System.Collections;
using NSYNK.HyperSlides.XR;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.UI
{
    [RequireComponent(typeof(RawImage))]
    public class UIMarkerImage : MonoBehaviour
    {
        public XRWorldAnchor.AnchorType type;

        private RawImage markerUIImage;

        private void Awake()
        {
            markerUIImage = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            markerUIImage.texture = type == XRWorldAnchor.AnchorType.Position ?
                XRAnchorManager.Instance.arTrackedImageManager.referenceLibrary[0].texture :
                XRAnchorManager.Instance.arTrackedImageManager.referenceLibrary[1].texture;
        }
    }
}