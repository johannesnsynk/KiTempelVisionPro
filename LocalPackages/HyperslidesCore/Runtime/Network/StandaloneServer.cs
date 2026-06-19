using UnityEngine;
using System.Collections.Generic;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.UI;
using NSYNK.HyperSlides.Runtime;
using Nakama;
using System.Linq;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using System;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

#if UNITY_VISIONOS
using Unity.PolySpatial;
#endif


namespace NSYNK.HyperSlides.Network
{
    [System.Serializable]
    public class SyncedTransformWrapper
    {
        public List<XRNetworkObjects.NetworkSyncedTransform> transforms = new();
    }

    [System.Serializable]
    public class SyncedValueWrapper
    {
        public List<XRNetworkObjects.NetworkSyncedValue> values = new();
    }

    /// <summary>
    /// A standalone server implementation for testing purposes, simulating a networked environment without actual network communication.
    /// This allows for testing multiplayer features in a single instance without needing multiple devices or network setup.
    /// </summary>
    public class StandaloneServer : Singleton<StandaloneServer>
    {
        public XRPresentation CurrentPresentation;
        public float updateCheckInterval = 6f;

        private float lastUpdateCheckTime = 0f;

        public XRPlayer StandalonePlayer()
        {
            Debug.Log("Creating standalone player", this);

            DeviceInfo.Instance.Role = XRPlayer.Role.Moderator;

            XRNetworkObjects.XRPlayer presence = new XRNetworkObjects.XRPlayer
            {
                UserId = System.Guid.NewGuid().ToString(),
                Username = "Standalone Player",
                role = DeviceInfo.Instance.Role.ToString()
            };

            XRPlayer LocalPlayer = Instantiate(HyperSlidesStateManager.Instance.Settings.LocalPlayerPrefab).GetComponent<XRPlayer>();

            LocalPlayer.UpdateModerator(presence);
            LocalPlayer.UpdateRole(DeviceInfo.Instance.Role);
            LocalPlayer.transform.SetParent(XRContentRoot.Instance.transform, false);

            return LocalPlayer;
        }

#if UNITY_VISIONOS
        private void OnEnable() => XRCameraManager.Instance.OnWindowUpdate += HandleWindowUpdate;
        private void OnDisable() => XRCameraManager.Instance.OnWindowUpdate -= HandleWindowUpdate;

        private void HandleWindowUpdate(VolumeCamera.WindowState state)
        {
            if ((state.WindowEvent == VolumeCamera.WindowEvent.Opened || state.WindowEvent == VolumeCamera.WindowEvent.Focused) && Time.time - lastUpdateCheckTime > updateCheckInterval)
            {
                Debug.Log("Application gained focus, refreshing presentation data from server after time interval.", this);
                StartPresentationAfterCheck();
            }
            else
                lastUpdateCheckTime = Time.time;
        }
#else
        private void OnApplicationFocus(bool focus)
        {
            if (focus && Time.time - lastUpdateCheckTime > updateCheckInterval)
            {
                Debug.Log("Application gained focus, refreshing presentation data from server after time interval.", this);
                StartPresentationAfterCheck();
            }
            else
                lastUpdateCheckTime = Time.time;
        }
#endif

        /// <summary>
        /// Sets up the standalone server environment, including loading a presentation, checking for updates, and initializing the local player and UI.
        /// </summary>
        public void SetupManagers()
        {
            if (CheckForSavedPresentation())
                Debug.Log("Loaded presentation from local storage: " + CurrentPresentation.id, Instance);
            else if (string.IsNullOrEmpty(DeviceInfo.Instance.StandalonePresentationId))
                LoadDefaultPresentation();
            else
                Debug.LogWarning("No saved presentation found for this device, and no default presentation specified.", Instance);

            StartPresentationAfterCheck();
        }

        /// <summary>
        /// Starts the presentation after checking for updates from the server.
        /// </summary>
        private void StartPresentationAfterCheck()
        {
            StartCoroutine(CheckForPresentationUpdate(() =>
            {
                XRNetworkManager.Instance.RuntimePlayers.Clear();
                XRNetworkManager.Instance.RuntimePlayers.Add(XRNetworkManager.Instance.LocalPlayer);
                XRNetworkManager.Instance.UpdateSessionTransformOverrides(CurrentPresentation.transformOverrides);

                XRSlideManager.Instance.SetPresentation(CurrentPresentation, PlayerPrefs.GetInt("CurrentPresentationIndex", 1));
                XRUIManager.Instance.LoadUserUI(true);

#if UNITY_VISIONOS
                XRUIManager.Instance.ShowModeratorNotesUI();
#endif
            }));
        }


        /// <summary>
        /// Checks for a saved presentation in local storage for this device, allowing for offline access and persistence of presentation data across sessions.
        /// </summary>
        /// <returns></returns>
        private bool CheckForSavedPresentation()
        {
            string presentationId = DeviceInfo.Instance.StandalonePresentationId;

            if (string.IsNullOrEmpty(presentationId))
                return false;

            if (File.Exists(Application.persistentDataPath + "/" + presentationId + ".json"))
            {
                string json = File.ReadAllText(Application.persistentDataPath + "/" + presentationId + ".json");
                List<XRPresentation> presentations = XRDataManager.Instance.HandleJsonResponse<XRPresentation>(json);

                if (presentations != null && presentations.Count > 0)
                {
                    XRPresentation savedPresentation = presentations.FirstOrDefault(p => p.id == presentationId);
                    CurrentPresentation = savedPresentation ?? presentations[0];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Saves the current presentation to local storage, allowing it to be loaded in future sessions or when an update from the server is not available.
        /// </summary>
        /// <param name="presentationJson">The JSON representation of the presentation to save.</param>
        private void SavePresentationLocally(string presentationJson)
        {
            string presentationId = DeviceInfo.Instance.StandalonePresentationId;
            File.WriteAllText(Application.persistentDataPath + "/" + presentationId + ".json", presentationJson);

            Debug.Log("Presentation saved locally with ID: " + presentationId, Instance);
        }

        /// <summary>
        /// Loads a default presentation from the Resources folder if no saved presentation is found for this device and no presentation update is available from the server. 
        /// </summary>
        private void LoadDefaultPresentation()
        {
            try
            {
                TextAsset jsonPresentation = Resources.Load<TextAsset>("StandalonePresentation");
                List<XRPresentation> presentations = XRDataManager.Instance.HandleJsonResponse<XRPresentation>(jsonPresentation.text);
                CurrentPresentation = presentations.FirstOrDefault();

                if (CurrentPresentation != null)
                    Debug.Log("Default presentation loaded with title: " + CurrentPresentation.title, Instance);
                else
                    Debug.LogWarning("Default presentation JSON found but failed to parse into a valid presentation.", Instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to load default presentation: " + e.Message, Instance);
            }

            if (CurrentPresentation != null)
                SavePresentationLocally(JsonUtility.ToJson(new XRJson<XRPresentation> { data = CurrentPresentation }));
        }

        /// <summary>
        /// Checks for updates to the current presentation from the server and updates the local presentation if necessary.
        /// </summary>
        /// <param name="onComplete">Callback to invoke when the update check is complete.</param>
        /// <returns></returns>
        private IEnumerator CheckForPresentationUpdate(Action onComplete)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(HyperSlidesStateManager.Instance.CurrentServerConnection.BackendIP + "/api/presentations/" + DeviceInfo.Instance.StandalonePresentationId))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string cleanedUpJson = CleanUpPresentations(request.downloadHandler.text);

                    List<XRPresentation> presentations = XRDataManager.Instance.HandleJsonResponse<XRPresentation>(cleanedUpJson);
                    XRPresentation foundPresentation = presentations.FirstOrDefault(p => p.id == DeviceInfo.Instance.StandalonePresentationId);

                    if (foundPresentation != null && (CurrentPresentation == null || foundPresentation.updatedAt.ToUniversalTime() > CurrentPresentation.updatedAt.ToUniversalTime()))
                    {
                        Debug.Log("Presentation updated or freshly loaded from server. foundPresentation.updatedAt: " + foundPresentation.updatedAt.ToUniversalTime() + ", CurrentPresentation.updatedAt: " + (CurrentPresentation != null ? CurrentPresentation.updatedAt.ToUniversalTime() : "null"), Instance);
                        CurrentPresentation = foundPresentation;

                        SavePresentationLocally(cleanedUpJson);
                    }
                    else
                    {
                        Debug.Log("No newer presentation update found on server. CurrentPresentation.updatedAt: " + (CurrentPresentation != null ? CurrentPresentation.updatedAt.ToUniversalTime() : "null"), Instance);

                        if (CurrentPresentation == null)
                            LoadDefaultPresentation();
                    }
                }
                else
                {
                    Debug.LogWarning("Failed to fetch presentation update: " + request.error, Instance);
                    Debug.LogWarning("URL: " + request.url, Instance);

                    LoadDefaultPresentation();
                }

                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Cleans up the presentation JSON by removing inactive slides, content tags, and triggers
        /// </summary>
        /// <param name="jsonResponse">The JSON string representing the presentation to clean up.</param>
        /// <returns>The cleaned-up JSON string.</returns>
        private string CleanUpPresentations(string jsonResponse)
        {
            //Remove all inactive slides, contenttags and triggers
            JObject parsedJson = JObject.Parse(jsonResponse);
            JToken dataToken = parsedJson["data"];
            if (dataToken != null)
            {
                if (dataToken.Type == JTokenType.Array)
                {
                    foreach (JObject presentation in dataToken.Children<JObject>())
                        CleanUpPresentationToken(presentation);
                }
                else if (dataToken.Type == JTokenType.Object)
                {
                    CleanUpPresentationToken((JObject)dataToken);
                }
            }

            return parsedJson.ToString(Formatting.None);
        }

        /// <summary>
        /// Removes inactive slides, content tags, and triggers
        /// </summary>
        /// <param name="presentation">The JObject representing the presentation to clean up.</param>
        private void CleanUpPresentationToken(JObject presentation)
        {
            if (presentation == null)
                return;

            JArray slides = presentation["slides"] as JArray;
            if (slides == null)
                return;

            Debug.Log("Cleaning up presentation: " + (string)presentation["title"] + " with current slides: " + slides.Count, this);

            List<JObject> inactiveSlides = slides
                .Children<JObject>()
                .Where(slide => !(slide["enabled"]?.Value<bool>() ?? false))
                .ToList();

            foreach (JObject inactiveSlide in inactiveSlides)
                inactiveSlide.Remove();

            foreach (JObject slide in slides.Children<JObject>())
            {
                JArray contentTags = slide["contentTags"] as JArray;
                if (contentTags != null)
                {
                    List<JObject> inactiveTags = contentTags
                        .Children<JObject>()
                        .Where(tag => !(tag["active"]?.Value<bool>() ?? false))
                        .ToList();

                    foreach (JObject inactiveTag in inactiveTags)
                        inactiveTag.Remove();
                }

                JArray triggers = slide["triggers"] as JArray;
                if (triggers != null)
                {
                    List<JObject> inactiveTriggers = triggers
                        .Children<JObject>()
                        .Where(trigger => !(trigger["active"]?.Value<bool>() ?? false))
                        .ToList();

                    foreach (JObject inactiveTrigger in inactiveTriggers)
                        inactiveTrigger.Remove();
                }
            }

            Debug.Log("Finished cleaning up presentation: " + (string)presentation["title"] + " with remaining slides: " + slides.Count, this);
        }
    }

    /// <summary>
    /// A fake match implementation for the standalone server, simulating a network match environment without actual network communication. This allows for testing multiplayer features in a single instance without needing multiple devices or network setup.
    /// </summary>
    public class FakeMatch : IMatch
    {
        public bool Authoritative => true;
        public string Id => "StandaloneMatch";
        public string Label => "Standalone Match";

        public IEnumerable<IUserPresence> Presences => new List<IUserPresence>();
        public int Size => 1;
        public IUserPresence Self => null;

        public void UpdatePresences(IMatchPresenceEvent presenceEvent) { }
    }
}