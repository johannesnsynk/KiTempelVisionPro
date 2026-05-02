using System;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using TMPro;
using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    public class UISlideOverview : MonoBehaviour
    {
        public UISlideButton uiButtonPrefab;
        public Transform scrollViewContent;

        private RectTransform rectTransform;
        private bool isOpen = false;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(rectTransform.sizeDelta.x, rectTransform.anchoredPosition.y);
        }

        private void OnEnable()
        {
            XRNetworkManager.onMatchJoined += UpdateSlideOverview;

            if (XRNetworkManager.Instance)
                UpdateSlideOverview();
        }

        private void OnDisable()
        {
            XRNetworkManager.onMatchJoined -= UpdateSlideOverview;
        }

        public void OpenClose()
        {
            isOpen = !isOpen;

            Vector2 anchoredPos = rectTransform.anchoredPosition;

            this.AnimateFloat(this.transform, Easing.Ease.EaseInOutQuad, 0, 1, 0.5f, 0, update =>
            {
                anchoredPos.x = Mathf.Lerp(anchoredPos.x, isOpen ? 50 : rectTransform.sizeDelta.x, update);
                rectTransform.anchoredPosition = anchoredPos;
            });
        }

        private void UpdateSlideOverview()
        {
            foreach (Transform t in scrollViewContent.transform)
                Destroy(t.gameObject);

            if (!XRSlideManager.CurrentPresentation)
                return;

            for (int i = 0; i < XRSlideManager.CurrentPresentation.contents.Count; i++)
            {
                int currentIndex = i + 1;

                if(XRSlideManager.CurrentPresentation.contents[i] is XRSlide.Trigger)
                    continue;

                XRSlide slide = XRSlideManager.CurrentPresentation.contents[i] as XRSlide;
                UISlideButton newSlideButton = Instantiate(uiButtonPrefab, scrollViewContent, false);
                TextMeshProUGUI buttonLabel = newSlideButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();

                newSlideButton.slide = slide;
                newSlideButton.LoadThumbnail();

                if (buttonLabel)
                    buttonLabel.text = slide.cueNumber + " - " + slide.displayName;

                newSlideButton.onClick.AddListener(() =>
                {
                    XRNetworkManager.Instance.SendSlideUpdate(XRSlideManager.CurrentPresentation, currentIndex);
                });
            }
        }
    }
}