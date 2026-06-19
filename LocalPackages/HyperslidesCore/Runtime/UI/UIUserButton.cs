using System.Collections;
using System.Linq;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.UI
{
    public class UIUserButton : UIButton
    {
        public TextMeshProUGUI usernameText;
        public Material trackingMaterial;

        private XRPlayer userPresence;
        private Toggle trackedToggle;
        private XRPlayer trackedPlayer;
        private GameObject trackingIndicator;

        protected override void Awake()
        {
            base.Awake();

            trackedToggle = GetComponentInChildren<Toggle>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            onClick.AddListener(() =>
            {
                trackedToggle.isOn = !trackedToggle.isOn;

                // if (!trackedToggle.isOn)
                //     // UIUserOverview.AddTrackedPlayer(userPresence);
                // else
                //     // UIUserOverview.RemoveTrackedPlayer(userPresence);
            });
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            onClick.RemoveAllListeners();

            if (trackingIndicator != null)
                Destroy(trackingIndicator);
        }

        private void FixedUpdate()
        {
            if (trackedPlayer == null && XRNetworkManager.Instance)
                trackedPlayer = XRNetworkManager.Instance.RuntimePlayers.Find(p => p.UserId == userPresence.UserId);

            if (trackedToggle.isOn && trackedPlayer != null)
            {
                CreateTrackingIndicator();

                if (trackingIndicator != null)
                {
                    trackingIndicator.transform.position = trackedPlayer.head.transform.position;
                    trackingIndicator.transform.rotation = trackedPlayer.transform.rotation;
                }
            }
            else
            {
                if (trackingIndicator != null)
                {
                    Destroy(trackingIndicator);
                    trackingIndicator = null;
                }
            }
        }

        private void CreateTrackingIndicator()
        {
            if (trackingIndicator != null || trackedPlayer == null)
                return;

            trackingIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            trackingIndicator.name = "Tracking Indicator - " + userPresence.Username;
            trackingIndicator.transform.localScale = Vector3.one * 0.2f;
            trackingIndicator.GetComponent<MeshRenderer>().material = trackingMaterial;

            Destroy(trackingIndicator.GetComponent<Collider>());
        }

        public void SetUserInformation(XRPlayer presence, bool wasTracked)
        {
            userPresence = presence;
            trackedToggle.isOn = wasTracked;

            if (usernameText)
            {
                usernameText.text = userPresence.Username;
                usernameText.text += userPresence.UserId;
            }
        }
    }
}