using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nakama;
using NSYNK.HyperSlides.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.Runtime
{
    public class XRRecordManager : Singleton<XRRecordManager>
    {
        /// <summary> The desired presentation ID to load from the Nakama server </summary>
        [Tooltip("The desired presentation ID to load from the Nakama server")]
        public string DesiredPresentationID;
        /// <summary> The Nakama server connection settings </summary>
        [Tooltip("The Nakama server connection settings")]
        public ServerConnection DefaultServerConnection;
        /// <summary> Enable slide navigation input in editor mode </summary>
        [Tooltip("Enable slide navigation input in editor mode")]
        public bool EnableArrowNavigation = false;

        /// <summary> Event invoked when the presentation has been loaded </summary>
        [Tooltip("Event invoked when the presentation has been loaded")]
        [Space(10)]
        public UnityEvent OnPresentationLoaded;

        /// <summary> The current slide index </summary>
        [Tooltip("The current slide index")]
        [ReadOnly, SerializeField]
        private int currentSlideIndex = 1;

        private XRPresentation loadedPresentation;
        private List<XRPresentation> allPresentations = new();
        private Client client;
        private ISession session;
        private string DeviceID = "";

        void Start()
        {
            ConnectToNakama();
        }

        /// <summary>
        /// Setup input actions for slide navigation in editor mode
        /// </summary>
        void OnEnable()
        {
            if (!EnableArrowNavigation)
                return;
                
            ///Create input action for left and right slide navigation with arrow keys
            InputAction inputAction = new InputAction(type: InputActionType.Button, binding: "");
            inputAction.Enable();

            inputAction.AddBinding("<Keyboard>/rightArrow");
            inputAction.AddBinding("<Keyboard>/leftArrow");
            inputAction.performed += ctx =>
            {
                if (ctx.control.name == "rightArrow")
                {
                    NextSlide();
                }
                else if (ctx.control.name == "leftArrow")
                {
                    PreviousSlide();
                }
            };
        }

        /// <summary>
        /// Go to the next or previous slide
        /// </summary>
        public void NextSlide() => SetSlideIndex(currentSlideIndex + 1);
        public void PreviousSlide() => SetSlideIndex(currentSlideIndex - 1);

        /// <summary>
        /// Set the current slide index
        /// </summary>
        /// <param name="index"></param>
        public void SetSlideIndex(int index)
        {
            if (loadedPresentation == null || loadedPresentation.slides.Count == 0)
            {
                Debug.LogWarning("No current presentation or slides to set slide index.", Instance);
                return;
            }

            if (XRSlideManager.Instance.CurrentPresentation == null)
                XRSlideManager.Instance.SetPresentation(loadedPresentation);

            //Loop index to start or end of presentation when exceeding bounds
            index = index < 1 ?
                loadedPresentation.slides.Count :
                index > loadedPresentation.slides.Count ?
                    1 : index;

            if (loadedPresentation != null)
            {
                currentSlideIndex = index;
                Debug.Log("Selected slide: " + currentSlideIndex, Instance);

                XRSlideManager.Instance.SetSlide(currentSlideIndex);
            }
        }

        /// <summary>
        /// Connect to Nakama server
        /// </summary>
        public void ConnectToNakama()
        {
            DeviceID = SystemInfo.deviceUniqueIdentifier + "_RecordManager";

            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            Dispatcher.Enqueue(async () =>
            {
                CreateClient();

                await CreateSession(token);
                await ReadPresentationStorageObject(token);
            });
        }

        /// <summary>
        /// Create a new nakama client
        /// </summary>
        /// <returns></returns>
        private void CreateClient()
        {
            Debug.Log("Creating client with: " + JsonUtility.ToJson(DefaultServerConnection), Instance);

            if (client == null)
                client = new Client(
                DefaultServerConnection.Protocol,
                DefaultServerConnection.NakamaIP,
                DefaultServerConnection.Port,
                DefaultServerConnection.ServerKey
                );

            var retryConfiguration = new RetryConfiguration(500, 5, delegate { Debug.Log("about to retry."); }, (history, baseDelay, random) =>
            {
                const int delayCap = 20000;
                var lastAttempt = history.Last();
                var jitter = Mathf.Min(delayCap, random.Next(baseDelay, lastAttempt.JitterBackoff * 3));
                return jitter;
            });

            client.GlobalRetryConfiguration = retryConfiguration;
        }

        /// <summary>
        /// Create a new nakama session
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task CreateSession(CancellationToken token)
        {
            try
            {
                if (session == null)
                    session = await client.AuthenticateDeviceAsync(DeviceID, canceller: token);
                else if (session.IsExpired || session.HasExpired(DateTime.UtcNow.AddDays(1)))
                {
                    try
                    {
                        session = await client.SessionRefreshAsync(session, canceller: token);
                        PlayerPrefs.SetString("nakama.authToken", session.AuthToken);
                    }
                    catch (ApiResponseException)
                    {
                        // Couldn't refresh the session so reauthenticate.
                        session = await client.AuthenticateDeviceAsync(DeviceID, canceller: token);
                        PlayerPrefs.SetString("nakama.refreshToken", session.RefreshToken);
                    }
                }
            }
            catch (OperationCanceledException e)
            {
                Debug.LogWarning("CreateSession() was canceled: " + e.Message, Instance);
            }
            catch (ApiResponseException e)
            {
                Debug.LogError("Error creating session: " + e.StatusCode + ": " + e.Message, Instance);
            }
            catch (Exception e)
            {
                Debug.Log("Error creating session:" + e.Message);
            }
        }

        /// <summary>
        /// Read Nakama storage object to avoid SSL issues for now
        /// </summary>
        public async Task ReadPresentationStorageObject(CancellationToken token)
        {
            Debug.Log("Start reading presentation storage object", Instance);

            try
            {
                var readObjectId = new StorageObjectId
                {
                    Collection = "Presentations",
                    Key = "Presentations",
                    UserId = ""
                };

                var result = await client.ReadStorageObjectsAsync(session, new[] { readObjectId }, canceller: token);

                if (result.Objects.Any())
                {
                    var storageObject = result.Objects.First();
                    XRDataManager.Instance.HandleJsonResponse<XRPresentation>(storageObject.Value);

                    Debug.Log("Read presentation from nakama storage", Instance);
                }
                else
                {
                    Debug.LogWarning("No presentation found in storage, loading default resource file", Instance);
                    XRDataManager.Instance.LoadResourceTextfile();
                }

                allPresentations = XRDataManager.Instance.AllPresentations;

                if (DesiredPresentationID != null && DesiredPresentationID != "")
                {
                    int desiredIndex = allPresentations.FindIndex(p => p.id == DesiredPresentationID);
                    if (desiredIndex != -1)
                    {
                        loadedPresentation = allPresentations[desiredIndex];

                        SetSlideIndex(1);

                        OnPresentationLoaded?.Invoke();

                        Debug.Log("Set current presentation to desired ID: " + DesiredPresentationID, Instance);
                    }
                    else
                    {
                        Debug.LogWarning("Desired presentation ID not found: " + DesiredPresentationID, Instance);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Error in reading storage ", Instance);
                XRDataManager.Instance.LoadResourceTextfile();
            }
            catch (ApiResponseException e)
            {
                Debug.LogWarning("Error in reading presentation from storage: " + e, Instance);
                XRDataManager.Instance.LoadResourceTextfile();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error in reading presentation from storage: " + e, Instance);
                XRDataManager.Instance.LoadResourceTextfile();
            }
        }

    }
}