using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.UI;
using NSYNK.HyperSlides.XR;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides
{
    public class HyperSlidesStateManager : Singleton<HyperSlidesStateManager>
    {
        public static CancellationTokenSource tokenSource;
        public static StateUpdate stateUpdate;

        public delegate void StateUpdate(AppState state);

        [Header("UI Status Objects")]
        public TMPro.TextMeshProUGUI statusText;
        public TMPro.TMP_InputField inputField;
        public GameObject loadingIconHolder, loadingIndicator, loadingDoneIndicator, statusRoot;
        public UnityEngine.UI.Image loadingIcon;
        public List<AppStateIcon> appStateIcons;

        public enum AppState
        {
            STARTING = 0, // App is starting
            PLATFORMCHECK = 100, // App is checking for platform and sets up correct scenes and prefabs
            DEVICE_SETUP = 200, // Setting up the device XR/AR settings and checking for tracking capabilities
            CONNECTION_SETUP = 300, // Setting up the connection to the server/backend to fetch or register user data
            MATCHMAKING = 400, // Handle the view of session matchmaking or the autoconnect to the first session available
            JOIN_SESSION = 500, // Device will join a session
            ACTIVE_SESSION = 600, // Device has joined a session
            ERROR = 1000, // If any known error occurs, stop the system and idle in error state
        }

        public enum LoadingState
        {
            NONE = 0,
            LOADING = 1,
            DONE = 2
        }

        [Serializable]
        public class AppStateIcon
        {
            public AppState state;
            public Sprite icon;
        }

        public static Exception exception;
        public static AppState appState;
        public AppState inspectorAppState = AppState.STARTING;

        private void Start()
        {
            tokenSource = new CancellationTokenSource();

#if !UNITY_VISIONOS
            statusRoot.transform.SetParent(XRUIManager.Instance.headUIRoot, false);
            statusRoot.transform.localPosition = new Vector3(0, 0, 1);
#endif

            UpdateAppState(AppState.STARTING);
        }

        /// <summary>
        /// Update the app state with a custom task that can be cancelled to avoid errors
        /// </summary>
        /// <param name="appState"></param>
        /// <param name="stateContext"></param>
        public static async void UpdateAppState(AppState appState, string stateContext = "")
        {
            try
            {
                var task = Task.Run(async () =>
                {
                    await UpdateAppStateAsync(appState, stateContext);
                    return "Task Completed";
                });

                if (!tokenSource.IsCancellationRequested)
                    await task.WithCancellation(tokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Task was cancelled.");
            }
        }

        /// <summary>
        /// Stop all background threads to avoid instantiation in editor or runtime issues
        /// </summary>
        private void OnApplicationQuit()
        {
            CancelToken();
            tokenSource.Dispose();
        }

        private static void CancelToken()
        {
            tokenSource.Cancel();
        }

        /// <summary>
        /// Update the current state asynchronously
        /// </summary>
        /// <param name="newState">The new app state to assign and check for</param>
        /// <param name="stateContext">A custom string that can hold information for the current state (like joining a match)</param>
        /// <returns></returns>
        private static async Task UpdateAppStateAsync(AppState newState, string stateContext)
        {
            if (tokenSource.IsCancellationRequested)
                return;

            appState = newState;

            Dispatcher.Enqueue(() =>
            {
                XRUIManager.Instance.HideAll();

                SetStateIcon(appState);
                DisableInputfield();

                Instance.statusRoot.SetActive(appState != AppState.ACTIVE_SESSION && appState != AppState.MATCHMAKING);
            });

            //Reset match and slides
            if (
                XRNetworkManager.match != null &&
                appState != AppState.JOIN_SESSION &&
                appState != AppState.ACTIVE_SESSION
                )
            {
                Dispatcher.Enqueue(() => XRSlideManager.Instance.Reset());

                await Awaitable.MainThreadAsync();
                await XRNetworkManager.Instance.LeaveMatchAsync();
            }

            await Debug.LogQueue($"<b>{appState}</b>\n{stateContext}\n{exception}", Instance);
            await Awaitable.MainThreadAsync();

            Instance.inspectorAppState = appState;
            stateUpdate?.Invoke(appState);

            switch (appState)
            {
                //The app start, nothing special
                case AppState.STARTING:
                    await App_Starting();
                    break;
                //The platform check for different scenes if needed
                case AppState.PLATFORMCHECK:
                    await App_Platformcheck();
                    break;
                //Setting up the device tracking methods
                case AppState.DEVICE_SETUP:
                    await App_DeviceSetup();
                    break;
                //Connection to the web and nakama server
                case AppState.CONNECTION_SETUP:
                    await App_ConnectionSetup();
                    break;
                //Show the matchmaking UI if auto connect is disabled
                case AppState.MATCHMAKING:
                    await App_Matchmaking();
                    break;
                //Join the current session or return to matchmaking
                case AppState.JOIN_SESSION:
                    await App_JoinSession(stateContext);
                    break;
                //User is in an active session
                case AppState.ACTIVE_SESSION:
                    await App_ActiveSession();
                    break;
                //Possible fallback state to still show information
                case AppState.ERROR:
                    await App_Error();
                    break;
            }
        }

        public static void StopWithError(Exception e)
        {
            exception = e;
            UpdateAppState(AppState.ERROR);
        }

        private static void SetStateIcon(AppState appState)
        {
            AppStateIcon appStateIcon = Instance.appStateIcons.Find(i => i.state == appState);

            if (appStateIcon != null)
                Instance.loadingIcon.sprite = appStateIcon.icon;

            Instance.loadingIcon.gameObject.SetActive(appStateIcon != null);
        }

        private static async Task App_Starting()
        {
            await Awaitable.MainThreadAsync();
            await UpdateStateWithDelay("App started");

            UpdateAppState(AppState.PLATFORMCHECK);
        }

        private static async Task App_Platformcheck()
        {
            try
            {
                await UpdateStateWithAwaitable("Loading assets", DeviceInfo.LoadPlatformAssets());

                UpdateAppState(AppState.DEVICE_SETUP);
            }
            catch (Exception e)
            {
                StopWithError(e);
            }
        }

        private static async Task App_DeviceSetup()
        {
            await UpdateStateWithDelay("Setting up tracking", loadingState: LoadingState.LOADING);

            await XRAnchorManager.Instance.Init();

            if (XRAnchorManager.arSupported)
                await UpdateStateWithDelay("Enabled XR systems", loadingState: LoadingState.DONE);

            if (!XRNetworkManager.IsConnected())
                UpdateAppState(AppState.CONNECTION_SETUP);
            else
            {
                if (!string.IsNullOrEmpty(XRNetworkManager.lastSessionStored))
                    UpdateAppState(AppState.JOIN_SESSION, XRNetworkManager.lastSessionStored);
                else
                    await HandleAutoJoinSession();
            }
        }

        private static async Task App_ConnectionSetup()
        {
            await UpdateStateWithTask("Connecting client", XRNetworkManager.Instance.Init());

            //Connect again when session code was changed
            if (XRNetworkManager.newSessionCodeDetected)
            {
                XRNetworkManager.newSessionCodeDetected = false;
                XRNetworkManager.Instance.Disconnect();
                await UpdateStateWithTask("Connecting client to new node", XRNetworkManager.Instance.Init());
            }

            if (!XRNetworkManager.IsConnected())
                UpdateText($"Can not connect to webserver ({XRNetworkManager.IsConnected()}).\nPlease check your network settings!");
            else
            {
                if (!string.IsNullOrEmpty(XRNetworkManager.lastSessionStored))
                    UpdateAppState(AppState.JOIN_SESSION, XRNetworkManager.lastSessionStored);
                else
                    await HandleAutoJoinSession();
            }
        }

        private static async Task App_Matchmaking()
        {
            await Awaitable.MainThreadAsync();

            XRUIManager.Instance.LoadUserUI();
            XRUIManager.Instance.InitMatchMakingUI();
        }

        private static async Task App_JoinSession(string stateContext)
        {
            await UpdateStateWithTask("Joining session", XRNetworkManager.Instance.JoinMatchAsync(stateContext));

            if (XRNetworkManager.match != null)
            {
                await UpdateStateWithDelay("Session joined");
                UpdateAppState(AppState.ACTIVE_SESSION);
            }
            else
            {
                await UpdateStateWithDelay("Can not join session\nstateContext");
                UpdateAppState(AppState.MATCHMAKING);
            }
        }

        private static async Task App_ActiveSession()
        {
            await Awaitable.MainThreadAsync();
            XRUIManager.Instance.LoadUserUI(true);
        }

        private static async Task App_Error()
        {
            XRNetworkManager.Instance.Stop();

            await Awaitable.MainThreadAsync();

            if (exception != null)
            {
                UpdateText("System stopped, check for error:\n" + exception.ToString());
                Debug.Log("System stopped, check for error: " + exception, Instance);
            }
            else
            {
                UpdateText("System error, please restart the app and check the logs");
                Debug.Log("System error, please restart the app and check the logs", Instance);
            }

            CancelToken();
        }

        private static async Task HandleAutoJoinSession()
        {
            string autojoinSession = await XRNetworkManager.Instance.GetAutojoinSession();

            if (string.IsNullOrEmpty(autojoinSession) || XRNetworkManager.stopAutoJoin)
                UpdateAppState(AppState.MATCHMAKING);
            else
                UpdateAppState(AppState.JOIN_SESSION, autojoinSession);
        }

        /// <summary>
        /// Update the state text and do not wait for any callback
        /// </summary>
        /// <param name="text">The text to be shown</param>
        public static void UpdateStateTextOnly(string text)
        {
            UpdateText(text);
            DisableInputfield();

            _ = UpdateLoadingIndicator(LoadingState.NONE);
        }

        /// <summary>
        /// Update the current state and text with an assigned task to wait for completion
        /// </summary>
        /// <param name="text"></param>
        /// <param name="waitedResult"></param>
        /// <returns></returns>
        public static async Task UpdateStateWithAwaitable(string text, Awaitable waitedResult) => await UpdateWaitingText(text, waitedResult);
        public static async Task UpdateStateWithTask(string text, Task waitedResult) => await UpdateWaitingText(text, waitedResult);
        public static async Task UpdateStateWithBool(string text, Func<bool> awaitBool) => await UpdateWaitingText(text, awaitBool);

        private static async Task UpdateWaitingText<T>(string text, T waitedResult)
        {
            UpdateText(text);

            await UpdateLoadingIndicator(LoadingState.LOADING);

            bool isDone = false;

            while (!isDone)
            {
                switch (waitedResult)
                {
                    case Task task:
                        isDone = task.IsCompleted;
                        break;
                    case Awaitable awaitable:
                        isDone = awaitable.IsCompleted;
                        break;
                    case Func<bool> boolean:
                        isDone = boolean();
                        break;
                }

                await Task.Delay(500);
            }

            await UpdateLoadingIndicator(LoadingState.DONE);

            DisableInputfield();
        }

        public static void EnableInputfield(string placeholder, UnityAction<string> callback)
        {
            Instance.inputField.gameObject.SetActive(true);
            Instance.inputField.text = placeholder;
            Instance.inputField.onEndEdit.AddListener(callback);
        }

        public static void DisableInputfield()
        {
            Dispatcher.Enqueue(() =>
            {
                Instance.inputField.gameObject.SetActive(false);
                Instance.inputField.onEndEdit.RemoveAllListeners();
            });
        }

        /// <summary>
        /// Update the current state and text with a delay to wait for
        /// </summary>
        /// <param name="text">The text shown in the center of the screen</param>
        /// <param name="delay">The delay in seconds</param>
        /// <returns></returns>
        public static async Task UpdateStateWithDelay(string text, float delay = .5f, LoadingState loadingState = LoadingState.DONE)
        {
            UpdateText(text);
            await UpdateLoadingIndicator(loadingState);

            await Task.Delay((int)(delay * 1000));

            await UpdateLoadingIndicator(LoadingState.DONE);
            UpdateText();
        }

        /// <summary>
        /// Update user UI text for stati
        /// </summary>
        /// <param name="text">The text shown in the center of the screen</param>
        private static void UpdateText(string text = "")
        {
            Dispatcher.Enqueue(() =>
            {
                if (appState == AppState.ERROR)
                    Instance.statusText.fontSize = 8;

#if UNITY_EDITOR
                text = "<size=8>" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "</size>\n" + text;
#endif

                Instance.statusText.text = text;
            });
        }

        private static async Task UpdateLoadingIndicator(LoadingState loadingState)
        {
            Dispatcher.Enqueue(() =>
            {
                Instance.loadingIndicator.SetActive(loadingState == LoadingState.LOADING);
                Instance.loadingDoneIndicator.SetActive(loadingState == LoadingState.DONE);
                Instance.loadingIconHolder.SetActive(loadingState != LoadingState.NONE);
            });

            await Task.Delay(500);
            await Awaitable.MainThreadAsync();
        }
    }
}