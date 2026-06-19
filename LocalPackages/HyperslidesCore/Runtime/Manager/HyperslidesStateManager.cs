using Nakama;
using Newtonsoft.Json.Linq;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.UI;
using NSYNK.HyperSlides.XR;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityMainThreadDispatcher;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NSYNK.HyperSlides
{
    public class HyperSlidesStateManager : Singleton<HyperSlidesStateManager>
    {
        public CancellationTokenSource tokenSource;
        public event Action<AppState> OnStateUpdate;

        /// <summary>Current app state</summary>
        [ReadOnly]
        public AppState CurrentAppState;

        /// <summary>
        /// Settings for the HyperSlides application
        /// </summary>
        [SerializeField]
        private Settings _settings;
        public Settings Settings
        {
            get
            {
                if (Instance)
                    return Instance._settings;
                else
                    return null;
            }
            set
            {
                Instance._settings = value;
            }
        }

        public enum ServerConnectionProfile
        {
            local = -2,
            custom = -1,
            eu = 0,
            sin = 1
        }

        [Header("Server Connection Profiles")]
        public ServerConnectionProfile EditorServerConnectionProfile = ServerConnectionProfile.eu;
        public List<ServerConnection> ServerConnections = new();
        [ReadOnly]
        public ServerConnection CurrentServerConnection = null;

        [Header("UI Status Objects")]
        public TMPro.TextMeshProUGUI statusText;
        public TMPro.TMP_InputField inputField;
        public GameObject loadingIconHolder, loadingIndicator, loadingDoneIndicator, statusRoot;
        public Image loadingIcon;
        public List<AppStateIcon> appStateIcons;

        [Header("Interaction Button Prefab")]
        public UIButton interactionButton;

        [ReadOnly]
        public string CurrentVersion = "Not defined";
        [HideInInspector]
        public bool ShowVersionOverlay = false;

        /// <summary>
        /// The different app states for the HyperSlides application
        /// </summary>
        public enum AppState
        {
            STARTING = 0, // App is starting
            DEVICE_SETUP = 200, // Setting up the device XR/AR settings and checking for tracking capabilities
            CONNECTION_SETUP = 300, // Setting up the connection to the server/backend to fetch or register user data
            RUNNING = 400, // Handle the view of session matchmaking or the autoconnect to the first session available
            ERROR = 1000, // If any known error occurs, stop the system and idle in error state
        }

        /// <summary>
        /// The loading state for the current operation
        /// </summary>
        public enum LoadingState
        {
            NONE = 0,
            LOADING = 1,
            DONE = 2
        }

        /// <summary>Exception that caused the error state</summary>
        private Exception exception;
        /// <summary>List of currently shown interaction buttons</summary>
        private List<UIButton> interactionButtons = new List<UIButton>();
        /// <summary>Stored settings for editor runtime changes</summary>
        private Settings storedSettings;

        void OnEnable()
        {
#if UNITY_EDITOR
            Instance.storedSettings = Instantiate(Settings);
            Settings = Instance.storedSettings;
#endif

            XRNetworkManager.Instance.OnSettingOverride += HandleCustomNotification;
        }

        void OnDisable()
        {
            RemoveAllButtonInteractions();

            if (XRNetworkManager.Instance == null)
                return;

            XRNetworkManager.Instance.OnSettingOverride -= HandleCustomNotification;
        }

        void OnGUI()
        {
#if UNITY_EDITOR
            ShowVersionOverlay = true;
#endif

            if (ShowVersionOverlay)
            {
                //Show the version of bottom right screen
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.fontSize = 20;
                style.normal.textColor = Color.white;
                Vector2 size = style.CalcSize(new GUIContent(CurrentVersion));
                GUI.Label(new Rect(Screen.width - size.x - 10, Screen.height - size.y - 10, size.x, size.y), CurrentVersion, style);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            //Get package version from package.json file
            string packagePath = GetPackageJsonPath();
            if (System.IO.File.Exists(packagePath))
            {
                string json = System.IO.File.ReadAllText(packagePath);
                JObject packageObj = JObject.Parse(json);
                string version = packageObj["version"]?.Value<string>();

                if (version != null)
                    CurrentVersion = version;
            }
        }

        private string GetPackageJsonPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);
            if (packageInfo != null)
                return System.IO.Path.Combine(packageInfo.resolvedPath, "package.json");

            MonoScript script = MonoScript.FromMonoBehaviour(this);
            string scriptAssetPath = AssetDatabase.GetAssetPath(script);

            if (string.IsNullOrEmpty(scriptAssetPath))
                return string.Empty;

            var directory = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(scriptAssetPath)!);

            while (directory != null)
            {
                string candidatePath = System.IO.Path.Combine(directory.FullName, "package.json");
                if (System.IO.File.Exists(candidatePath))
                    return candidatePath;

                directory = directory.Parent;
            }

            return string.Empty;
        }
#endif

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

#if UNITY_EDITOR
            SetServerConnection(EditorServerConnectionProfile.ToString());
#endif
        }

        private void Start()
        {
            tokenSource = new CancellationTokenSource();

#if !UNITY_VISIONOS
            statusRoot.transform.SetParent(XRUIManager.Instance.dynamicUIRoot, false);
            statusRoot.transform.localPosition = new Vector3(0, 0, 1);
#endif

            UpdateAppState(AppState.STARTING);
        }

        /// <summary>
        /// Sets the current server connection based on the provided connection name. If the connection is not found, it falls back to the default connection
        /// </summary>
        /// <param name="connectionName">The name of the server connection to switch to</param>
        public void SetServerConnection(string connectionName = "none")
        {
            ServerConnection connection = ServerConnections.Find(c => c.ConnectionName == connectionName);

#if UNITY_EDITOR
            // In editor, allow switching to custom connection profile with enum
            connection = ServerConnections.Find(c => c.ConnectionName == EditorServerConnectionProfile.ToString());
#endif

            if (connection != null)
            {
                CurrentServerConnection = connection;
                Debug.Log("Switched to server connection: " + JsonUtility.ToJson(CurrentServerConnection), Instance);
            }
            else
            {
                CurrentServerConnection = ServerConnections.FirstOrDefault();
                Debug.LogWarning("Server connection not found: " + connectionName + " Switching to default connection: " + (CurrentServerConnection != null ? CurrentServerConnection.ConnectionName : "None"), Instance);
            }
        }

        /// <summary>
        /// Adds a new server connection to the list if it doesn't exist and sets it as the current connection
        /// </summary>
        /// <param name="serverConnection">The server connection to add and set</param>
        public void AddNewConnectionAndSet(ServerConnection serverConnection)
        {
            if (serverConnection == null)
            {
                Debug.LogWarning("Cannot add null server connection.", Instance);
                return;
            }

            if (!ServerConnections.Any(c => c.ConnectionName == serverConnection.ConnectionName))
            {
                Debug.LogWarning("Server connection not found: " + serverConnection.ConnectionName + " Adding to connections list.", Instance);
                ServerConnections.Add(serverConnection);
            }

            SetServerConnection(serverConnection.ConnectionName);
        }

        /// <summary>
        /// Update the app state with a custom task that can be cancelled to avoid errors
        /// </summary>
        /// <param name="appState"></param>
        /// <param name="stateContext"></param>
        public async void UpdateAppState(AppState appState, string stateContext = "")
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
                Debug.Log("Task was cancelled.", Instance);
            }
        }

        void FixedUpdate()
        {
            bool showStatusRoot =
                XRNetworkManager.Instance.CurrentNetworkState != XRNetworkManager.NetworkState.JOINED &&
                XRNetworkManager.Instance.CurrentNetworkState != XRNetworkManager.NetworkState.MATCHMAKING;

            if (Instance.statusRoot != null && Instance.statusRoot.activeSelf != showStatusRoot)
                Instance.statusRoot.SetActive(showStatusRoot);
        }

        /// <summary>
        /// Stop all background threads to avoid instantiation in editor or runtime issues
        /// </summary>
        private void OnApplicationQuit()
        {
            CancelToken();
            tokenSource.Dispose();
        }

        private void CancelToken()
        {
            tokenSource.Cancel();
        }

        /// <summary>
        /// Update the current state asynchronously
        /// </summary>
        /// <param name="newState">The new app state to assign and check for</param>
        /// <param name="stateContext">A custom string that can hold information for the current state (like joining a match)</param>
        /// <returns></returns>
        private async Task UpdateAppStateAsync(AppState newState, string stateContext)
        {
            if (tokenSource.IsCancellationRequested)
                return;

            CurrentAppState = newState;

            Dispatcher.Enqueue(() =>
            {
                SetStateIcon(CurrentAppState);
                DisableInputfield();
                RemoveAllButtonInteractions();
            });

            await Debug.LogQueue($"APP STATE: <b>{CurrentAppState}</b>\n{stateContext}\n{exception}", Instance);
            await Awaitable.MainThreadAsync();

            OnStateUpdate?.Invoke(CurrentAppState);

            if (CurrentAppState != AppState.RUNNING)
                XRSlideManager.Instance.Reset();

            switch (CurrentAppState)
            {
                //The app start, nothing special
                case AppState.STARTING:
                    await App_Starting();
                    break;
                //Setting up the device tracking methods
                case AppState.DEVICE_SETUP:
                    await App_DeviceSetup(stateContext);
                    break;
                //Connection to the web and nakama server
                case AppState.CONNECTION_SETUP:
                    await App_ConnectionSetup();
                    break;
                //Possible fallback state to still show information
                case AppState.ERROR:
                    await App_Error();
                    break;
            }
        }

        public void StopWithError(Exception e)
        {
            exception = e;
            UpdateAppState(AppState.ERROR);
        }

        private void SetStateIcon(AppState appState)
        {
            AppStateIcon appStateIcon = Instance.appStateIcons.Find(i => i.state == appState);

            if (appStateIcon != null)
                Instance.loadingIcon.sprite = appStateIcon.icon;

            Instance.loadingIcon.gameObject.SetActive(appStateIcon != null);
        }

        private async Task App_Starting()
        {
            await Task.Delay(1);

            UpdateAppState(AppState.DEVICE_SETUP);
        }

        private async Task App_DeviceSetup(string stateContext = "")
        {
            await XRNetworkManager.Instance.UpdateNetworkStateAsync(XRNetworkManager.NetworkState.DISCONNECT);

#if !UNITY_EDITOR
            if (!DeviceInfo.Instance.UseStandaloneSetup)
            {
                await UpdateStateWithButton("Setting up tracking", new List<ButtonInteraction> {
                new () {
                        buttonText = "Skip Anchor setup",
                        callback = () => {
                            Debug.Log("Skipping Anchor Setup");
                            HyperSlidesStateManager.Instance.Settings.trackingType = Settings.TrackingType.Free;
                            XRAnchorManager.Instance.StartOverAnchorSetup();
                        }
                    }
                });

                await XRAnchorManager.Instance.Init();

                if (XRAnchorManager.arSupported)
                    await UpdateStateWithDelay("Enabled XR systems", loadingState: LoadingState.DONE);
            }
            else
            {
                HyperSlidesStateManager.Instance.Settings.trackingType = Settings.TrackingType.Free;
                XRAnchorManager.Instance.StartOverAnchorSetup();
            }
#endif

            UpdateAppState(AppState.CONNECTION_SETUP, stateContext);
        }

        private async Task App_ConnectionSetup(string stateContext = "")
        {
            await UpdateStateWithTask("Connecting client", XRNetworkManager.Instance.Init());

            //Connect again when session code was changed
            if (XRNetworkManager.Instance.NewSessionCodeDetected)
                await UpdateStateWithTask("Connecting client to new node", XRNetworkManager.Instance.UpdateNetworkStateAsync(XRNetworkManager.NetworkState.RECONNECTING));

            UpdateAppState(AppState.RUNNING);
        }

        private async Task App_Error()
        {
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

        /// <summary>
        /// Update the state text and do not wait for any callback
        /// </summary>
        /// <param name="text">The text to be shown</param>
        public void UpdateStateTextOnly(string text)
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
        public async Awaitable UpdateStateWithAwaitable(string text, Awaitable waitedResult) => await UpdateWaitingText(text, waitedResult);
        public async Awaitable UpdateStateWithTask(string text, Task waitedResult) => await UpdateWaitingText(text, waitedResult);
        public async Awaitable UpdateStateWithBool(string text, Func<bool> awaitBool) => await UpdateWaitingText(text, awaitBool);
        public async Awaitable UpdateStateWithButton(string text, List<ButtonInteraction> buttonInteractions, bool needsConfirmation = false) => await UpdateWithButtonAction(text, buttonInteractions, needsConfirmation);

        private async Awaitable UpdateWaitingText<T>(string text, T waitedResult)
        {
            UpdateText(text);

            await UpdateLoadingIndicator(LoadingState.LOADING);

            await AwaitResultToContinue(waitedResult);

            await UpdateLoadingIndicator(LoadingState.DONE);

            DisableInputfield();
        }

        /// <summary>
        /// Update the current state and text with button interactions that can be confirmed to continue
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="buttonInteractions">The list of button interactions</param>
        /// <param name="needsConfirmation">Whether confirmation is needed for any input or not</param>
        /// <returns></returns>
        private async Awaitable UpdateWithButtonAction(string text, List<ButtonInteraction> buttonInteractions, bool needsConfirmation = false)
        {
            UpdateText(text);

            bool isConfirmed = false;

            RemoveAllButtonInteractions();

            foreach (var buttonInteraction in buttonInteractions)
            {
                UIButton buttonInstance = Instantiate(Instance.interactionButton, Instance.loadingIconHolder.transform.parent, false);

                buttonInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = buttonInteraction.buttonText;
                buttonInstance.onClick.AddListener(buttonInteraction.callback);

                if (needsConfirmation)
                    buttonInstance.onClick.AddListener(() => isConfirmed = true);

                Instance.interactionButtons.Add(buttonInstance);
            }

            if (needsConfirmation)
                await AwaitResultToContinue<Func<bool>>(() => isConfirmed);

            await UpdateLoadingIndicator(LoadingState.NONE);
        }

        /// <summary>
        /// Remove all button interactions from the status UI
        /// </summary>
        public void RemoveAllButtonInteractions()
        {
            Instance.interactionButtons.ForEach(b => Destroy(b.gameObject));
            Instance.interactionButtons.Clear();
        }

        /// <summary>
        /// Wait for a result to be completed, can be a Task, Awaitable or Func<bool>
        /// </summary>
        /// <typeparam name="T">The type of the waited result</typeparam>
        /// <param name="waitedResult">The result to wait for</param>
        /// <returns>Returns a task with a boolean indicating completion</returns>
        private async Task AwaitResultToContinue<T>(T waitedResult)
        {
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

                await Task.Delay(10);
            }
        }

        public void EnableInputfield(string placeholder, UnityAction<string> callback)
        {
            EnableInputfield(placeholder, 0, callback);
        }

        public void EnableInputfield(string placeholder, int characterLimit, UnityAction<string> callback)
        {
            int defaultCharacterLimit = Instance.inputField.characterLimit;
            Instance.inputField.characterLimit = characterLimit > 0 ? characterLimit : defaultCharacterLimit;

            Instance.inputField.gameObject.SetActive(true);
            Instance.inputField.text = placeholder;
            Instance.inputField.onEndEdit.RemoveAllListeners();
            Instance.inputField.onEndEdit.AddListener(callback);
            Instance.inputField.onEndEdit.AddListener((_) => Instance.inputField.characterLimit = defaultCharacterLimit);
        }

        public void DisableInputfield()
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
        public async Task UpdateStateWithDelay(string text, float delay = .5f, LoadingState loadingState = LoadingState.DONE)
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
        private void UpdateText(string text = "")
        {
            Dispatcher.Enqueue(() =>
            {
                if (CurrentAppState == AppState.ERROR)
                    Instance.statusText.fontSize = 8;

#if UNITY_EDITOR
                text = "<size=8>" + DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss") + "</size>\n" + text;
#endif

                Instance.statusText.text = text;
            });
        }

        private async Task UpdateLoadingIndicator(LoadingState loadingState)
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

        /// <summary>
        /// Handles custom notifications from the Nakama server.
        /// </summary>
        /// <param name="notification"></param>
        private void HandleCustomNotification(IApiNotification notification)
        {
            try
            {
                JObject contentObj = JObject.Parse(notification.Content);
                string dataString = contentObj["data"]?.Value<string>();
                JObject dataObj = JObject.Parse(dataString);

                string settingName = dataObj[0]?.Value<string>();
                string settingValue = dataObj[1]?.Value<string>();

                OverrideSettings(settingName, settingValue);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse custom notification: {e.Message}");
                return;
            }
        }

        /// <summary>
        /// Overrides a specific setting in the Settings class.
        /// </summary>
        /// <param name="settingName"></param>
        /// <param name="settingValue"></param>
        private void OverrideSettings(string settingName, string settingValue)
        {
            foreach (var setting in Settings.GetType().GetFields())
            {
                if (setting.Name == settingName)
                {
                    try
                    {
                        setting.SetValue(Settings, Convert.ChangeType(settingValue, setting.FieldType));
                        Debug.Log($"Setting {setting.Name} overridden with value: {settingValue}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to override setting {setting.Name}: {e.Message}");
                    }
                }
            }
        }
    }
}