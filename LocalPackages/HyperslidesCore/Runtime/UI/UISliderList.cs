using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// A list of sliders that can be used to control various aspects of the presentation, such as volume, brightness, etc.
    /// </summary>
    public class UISliderList : MonoBehaviour
    {
        public NetworkSynced uiSliderPrefab;

        private void OnEnable()
        {
            XRSlideManager.Instance.OnXRSlideChanged += CheckForNetworkSyncs;

            CheckForNetworkSyncs(null);
        }

        private void OnDisable()
        {
            if (XRSlideManager.Instance)
                XRSlideManager.Instance.OnXRSlideChanged -= CheckForNetworkSyncs;
        }

        /// <summary>
        /// Checks for any network synced sliders on the current slide and creates UI sliders for them in the slider list.
        /// </summary>
        /// <param name="slide">The current slide. Not relevant in this context.</param>
        private void CheckForNetworkSyncs(XRSlide slide)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            XRUISlider[] syncObjects = FindObjectsByType<XRUISlider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (XRUISlider sync in syncObjects)
            {
                NetworkSynced syncedComponent = sync.GetComponent<NetworkSynced>();

                if (syncedComponent == null)
                    continue;

                NetworkSynced newSlider = Instantiate(uiSliderPrefab, transform);
                newSlider.guid = syncedComponent.guid;
            }

        }
    }
}