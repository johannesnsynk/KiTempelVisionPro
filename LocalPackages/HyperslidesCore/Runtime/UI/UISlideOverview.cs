using System;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using TMPro;
using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    public class UISlideOverview : UITogglePanel
    {
        public UISlideButton uiButtonPrefab;
        public Transform scrollViewContent;

        private void OnEnable()
        {
            XRNetworkManager.Instance.OnMatchJoined += UpdateSlideOverview;

            if (XRNetworkManager.Instance)
                UpdateSlideOverview();
        }

        private void OnDisable()
        {
            if (XRNetworkManager.Instance == null)
                return;

            XRNetworkManager.Instance.OnMatchJoined -= UpdateSlideOverview;
        }

        /// <summary>
        /// Updates the slide overview list with current slides in the presentation
        /// </summary>
        private void UpdateSlideOverview()
        {
            foreach (Transform t in scrollViewContent.transform)
                Destroy(t.gameObject);

            if (!XRSlideManager.Instance.CurrentPresentation)
                return;

            for (int i = 0; i < XRSlideManager.Instance.CurrentPresentation.contents.Count; i++)
            {
                int currentIndex = i + 1;

                if (XRSlideManager.Instance.CurrentPresentation.contents[i] is XRSlide.Trigger)
                    continue;

                XRSlide slide = XRSlideManager.Instance.CurrentPresentation.contents[i] as XRSlide;
                UISlideButton newSlideButton = Instantiate(uiButtonPrefab, scrollViewContent, false);
                TextMeshProUGUI buttonLabel = newSlideButton.GetComponentInChildren<TextMeshProUGUI>();

                newSlideButton.slide = slide;
                newSlideButton.LoadThumbnail();

                if (buttonLabel)
                    buttonLabel.text = (DeviceInfo.Instance.UseStandaloneSetup ? i + 1 : slide.cueNumber) + " - " + slide.displayName;

                newSlideButton.onClick.AddListener(() =>
                {
                    XRNetworkManager.Instance.SendSlideUpdate(XRSlideManager.Instance.CurrentPresentation, currentIndex);
                });
            }
        }
    }
}