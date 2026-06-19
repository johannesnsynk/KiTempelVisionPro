using System;
using System.Collections.Generic;
using Nakama;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using TMPro;
using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// Manages the user overview panel, allowing to track/untrack users and filter them based on roles and device types
    /// </summary>
    public class UIUserOverview : UITogglePanel
    {
        public static List<string> trackedUserIds = new List<string>();
        public static List<XRPlayer> filteredPlayers = new();

        public UIButton toggleAllUsersButtonPrefab;
        public UIUserButton uiButtonPrefab;
        public Transform scrollViewContent;

        private bool allUsersTracked = false;

        private void OnEnable()
        {
            XRNetworkManager.Instance.OnMatchLeft += UpdateUserOverview;
            XRNetworkManager.Instance.OnUsersPresencesUpdated += UpdateUserOverview;

            if (XRNetworkManager.Instance)
                UpdateUserOverview();
        }

        private void OnDisable()
        {
            if (XRNetworkManager.Instance == null)
                return;

            XRNetworkManager.Instance.OnMatchLeft -= UpdateUserOverview;
            XRNetworkManager.Instance.OnUsersPresencesUpdated -= UpdateUserOverview;
        }

        /// <summary>
        /// Filters the tracked players to exclude certain roles and device types as well as the local player
        /// </summary>
        private void GetFilteredTrackedPlayers()
        {
            filteredPlayers.Clear();

            foreach (XRPlayer player in XRNetworkManager.Instance.RuntimePlayers)
            {
                try
                {
                    // Exclude non-participants and non-moderators
                    if (player.role != XRPlayer.Role.Participant && player.role != XRPlayer.Role.Moderator)
                        continue;

                    // Exclude iPads
                    // Removed this for now, as we do not use the userpresences from nakama anymore
                    // if (!string.IsNullOrEmpty(player.deviceType) && player.deviceType.ToLower().Contains("ipad"))
                    //     continue;

                    // Exclude local player
                    if (XRNetworkManager.Instance.LocalPlayer != null && player.UserId == XRNetworkManager.Instance.LocalPlayer.UserId)
                        continue;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Error filtering player " + player.Username + ": " + e.Message);
                    continue;
                }

                filteredPlayers.Add(player);
            }
        }

        /// <summary>
        /// Updates the user overview list with current users in the match
        /// </summary>
        private void UpdateUserOverview()
        {
            foreach (Transform t in scrollViewContent.transform)
                Destroy(t.gameObject);

            if (XRNetworkManager.Instance.Match == null)
                return;

            CreateToggleAllUsersButton();
            GetFilteredTrackedPlayers();

            foreach (XRPlayer userPresence in filteredPlayers)
            {
                UIUserButton newUserButton = Instantiate(uiButtonPrefab, scrollViewContent, false);
                TextMeshProUGUI buttonLabel = newUserButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();

                newUserButton.SetUserInformation(userPresence, trackedUserIds.Contains(userPresence.UserId));

                if (buttonLabel)
                    buttonLabel.text = userPresence.Username;

                newUserButton.onClick.AddListener(() =>
                {
                    if (trackedUserIds.Contains(userPresence.UserId))
                        trackedUserIds.Remove(userPresence.UserId);
                    else
                        trackedUserIds.Add(userPresence.UserId);

                    allUsersTracked = trackedUserIds.Count == filteredPlayers.Count;

                    UpdateUserOverview();
                });
            }
        }

        /// <summary>
        /// Creates a button to toggle tracking all users
        /// </summary>
        private void CreateToggleAllUsersButton()
        {
            UIButton toggleAllUsersButton = Instantiate(toggleAllUsersButtonPrefab, scrollViewContent, false);
            TextMeshProUGUI buttonLabel = toggleAllUsersButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            if (buttonLabel)
            {
                buttonLabel.text = allUsersTracked ? "Untrack All" : "Track All";
                buttonLabel.text += $" ({trackedUserIds.Count} / {filteredPlayers.Count})";
            }

            toggleAllUsersButton.onClick.AddListener(() =>
            {
                allUsersTracked = !allUsersTracked;

                trackedUserIds.Clear();

                if (allUsersTracked)
                    foreach (XRPlayer userPresence in filteredPlayers)
                        trackedUserIds.Add(userPresence.UserId);

                UpdateUserOverview();
            });
        }
    }
}