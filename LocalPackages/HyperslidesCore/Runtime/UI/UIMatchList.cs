using Nakama;
using NSYNK.HyperSlides.Network;
using System;
using System.Collections.Generic;

using UnityEngine;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.UI
{
    public class UIMatchList : MonoBehaviour
    {
        public RectTransform sessionHolder;
        public GameObject sessionButtonPrefab;

        private List<IApiMatch> apiMatches = new();

        private void OnEnable()
        {
            XRNetworkManager.onMatchListUpdate += UpdateDropdownOptions;

            if(XRNetworkManager.Instance)
                UpdateDropdownOptions(XRNetworkManager.FilteredMatchList);
        }

        private void OnDisable()
        {
            XRNetworkManager.onMatchListUpdate -= UpdateDropdownOptions;
        }

        /// <summary>
        /// Update the current dropdown options based on the matches from network
        /// </summary>
        /// <param name="matches">The match list coming from the network update</param>
        private void UpdateDropdownOptions(List<IApiMatch> matches)
        {
            if (matches == null)
                return;

            Dispatcher.Enqueue(() =>
            {
                apiMatches = new List<IApiMatch>(matches);

                foreach (Transform t in sessionHolder.transform)
                {
                    if (t.TryGetComponent(out UISessionButton sessionButton))
                        sessionButton.StopAllCoroutines();

                    Destroy(t.gameObject);
                }

                foreach (IApiMatch match in apiMatches)
                {
                    UISessionButton newSessionButton = Instantiate(sessionButtonPrefab, sessionHolder, false).GetComponent<UISessionButton>();
                    newSessionButton.Init(match);
                }
            });
        }
    }
}