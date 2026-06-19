using Nakama;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.Runtime;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.Networking;
using System.Threading;
using Unity.PolySpatial;
using NSYNK.HyperSlides.XR;
using NSYNK.HyperSlides.UI;
using System.Text.RegularExpressions;
using MessagePack;

namespace NSYNK.HyperSlides.Network
{
    public class XRNetworkManager : Singleton<XRNetworkManager>
    {
        public StandaloneServer StandaloneServer;

        public enum NetworkState
        {
            STANDALONE = -1,
            OFFLINE = 0,
            CONNECTING = 1,
            JOINING = 3,
            JOINED = 4,
            LEAVING = 5,
            DISCONNECT = 6,
            RECONNECTING = 7,
            MATCHMAKING = 8,
            ERROR = 9
        }

        /// <summary>The role to override for the local player</summary>
        public XRPlayer.Role OverrideRole;
        /// <summary>Event fired when the match list is updated</summary>
        public event Action<List<IApiMatch>> OnMatchListUpdate;
        /// <summary>Event fired when a user connects to the server</summary>
        public event Action OnUserConnected;
        /// <summary>Event fired when users presences are updated or changed</summary>
        public event Action OnUsersPresencesUpdated;
        /// <summary>Event fired when the match is joined</summary>
        public event Action OnMatchJoined;
        /// <summary>Event fired when the match is left</summary>
        public event Action OnMatchLeft;
        /// <summary>Event fired when the session transform override is updated from network</summary>
        public event Action OnSessionTransformOverride;
        /// <summary>Event fired when the slide is changed from network</summary>
        public event Action<XRNetworkObjects.XRSessionState> OnNetworkSlideUpdate;
        /// <summary>Event fired when a player transform is updated from network</summary>
        public event Action<XRNetworkObjects.XRPlayer> OnNetworkPlayerUpdate;
        /// <summary>Event fired when a moderator transform is updated from network</summary>
        public event Action<XRNetworkObjects.XRPlayer> OnNetworkModeratorUpdate;
        /// <summary>Event fired when a network synced transform is updated from network</summary>
        public event Action<XRNetworkObjects.NetworkSyncedTransform> OnNetworkTransformUpdate;
        /// <summary>Event fired when a network synced value is updated from network</summary>
        public event Action<XRNetworkObjects.NetworkSyncedValue> OnNetworkValueUpdate;
        /// <summary>Event fired when a custom notification is received from network</summary>
        public event Action<IApiNotification> OnCustomNotification;
        /// <summary>Event fired when a setting override notification is received from network</summary>
        public event Action<IApiNotification> OnSettingOverride;

        /// <summary>The filtered match list based on the session code</summary>
        public List<IApiMatch> FilteredMatchList { get; private set; } = new();
        /// <summary>The list of user presences in the session</summary>
        public List<XRNetworkObjects.XRPlayer> UserPresences = new();
        /// <summary>The list of network synced transforms in the session</summary>
        public Dictionary<string, XRNetworkObjects.NetworkSyncedTransform> NetworkSyncedTransforms = new();
        /// <summary>The list of network synced values in the session</summary>
        public Dictionary<string, XRNetworkObjects.NetworkSyncedValue> NetworkSyncedValues = new();
        /// <summary>The list of session transform overrides in the session</summary>
        public List<XRNetworkObjects.SessionTransformOverride> SessionTransformOverrides = new();

        /// <summary>The local player instance</summary>
        public XRPlayer LocalPlayer { get; private set; } = null;
        /// <summary>The current match instance</summary>
        public IMatch Match { get; private set; }
        /// <summary>The nakama socket instance</summary>
        public ISocket Socket { get; private set; }

        /// <summary>The list of runtime players in the session</summary>
        public List<XRPlayer> RuntimePlayers { get; private set; } = new();
        /// <summary>The current slide index in the presentation</summary>
        [ReadOnly]
        public int CurrentSlideIndex { get; private set; } = 0;
        /// <summary>The current network state</summary>
        public NetworkState CurrentNetworkState = NetworkState.OFFLINE;
        /// <summary>The last session code stored in player prefs</summary>
        public string LastSessionStored { get; private set; } = "";
        /// <summary>Flag to indicate if a new session code was detected</summary>
        public bool NewSessionCodeDetected { get; private set; } = false;
        /// <summary>Flag to stop auto joining a session on start</summary>
        public bool StopAutoJoin { get; private set; } = false;
        /// <summary>Flag to enable application focus handling on editor</summary>
        public bool EnableApplicationFocusHandling { get; set; } = false;
        /// <summary>The client latency in milliseconds, calculated as the difference between current UTC time and the received player state timestamp</summary>
        public float ClientLatencyMs = 0;

        /// <summary>The nakama account instance</summary>
        private IApiAccount account;
        /// <summary>The nakama session instance</summary>
        private ISession session;
        /// <summary>The nakama client instance</summary>
        private Client client;
        /// <summary>The UDP connection</summary>
        private UDPConnection udpConnection = null;

        /// <summary>Timestamp of the last received network message</summary>
        private DateTime lastTimestamp;
        /// <summary>Cancellation token source for network operations</summary>
        private CancellationTokenSource networkTokenSource = new();
        /// <summary>Retry count for reconnect attempts</summary>
        private int retryCount = 5;
        /// <summary>The username used for the session</summary>
        private string deviceUserName = "";
        private DateTime lastUDPUpdateTimestamp = DateTime.MinValue;
        private DateTime lastUDPPlayerStateTimestamp = DateTime.MinValue;

        /// <summary>Connect to the nakama server and create client, socket and session</summary>
        public async Task Init() => await UpdateNetworkStateAsync(NetworkState.CONNECTING);
        /// <summary>Check if the socket is connected</summary><returns>True, if the socket is connected</returns>
        public bool SocketIsConnected() => Socket != null && Socket.IsConnected;
        /// <summary>Get the current session code from the player prefs</summary><returns>The session code from playerprefs or 000-000 by default</returns>
        public string GetSessionCode() => PlayerPrefs.GetString("sessionCode", "000-000");
        /// <summary>Get the current session code from the current match</summary><returns>The match ID or an empty string</returns>
        public string GetUserMatchID() => Match != null ? Match.Id : "";
        /// <summary>Check the socket connection and reconnect if needed</summary>
        private void CheckSocketConnection() => CheckSocketConnectionAsync();

        /// <summary>Receive any error message coming from the socket</summary>
        private void SocketErrorMessage(Exception error) => Dispatcher.Enqueue(() => Debug.Log("ERROR Received: " + error, this));

        private void OnEnable()
        {
            if (udpConnection == null)
                udpConnection = gameObject.AddComponent<UDPConnection>();

            RuntimeHandler.Instance.Tick += SendUpdatedPlayer;
            RuntimeHandler.Instance.SlowTick += SendPlayerMetaData;
            RuntimeHandler.Instance.SocketTick += CheckSocketConnection;

#if UNITY_VISIONOS
            XRCameraManager.Instance.OnWindowUpdate += VisionOSWindowUpdate;
#endif
        }

        private void OnDisable()
        {
            if (RuntimeHandler.Instance == null)
                return;

            RuntimeHandler.Instance.Tick -= SendUpdatedPlayer;
            RuntimeHandler.Instance.SlowTick -= SendPlayerMetaData;
            RuntimeHandler.Instance.SocketTick -= CheckSocketConnection;

#if UNITY_VISIONOS
            if (XRCameraManager.Instance == null)
                return;

            XRCameraManager.Instance.OnWindowUpdate -= VisionOSWindowUpdate;
#endif
        }


        /// <summary>
        /// Query through active players list and update their positions visually based on the server data
        /// </summary>
        private void Update()
        {
            if (Match == null || LocalPlayer == null || LocalPlayer.gameObject == null)
                return;
            else
                MovePlayer();

            RuntimePlayers.ForEach(p => p.UpdatePlayerTransform());

            if (udpConnection != null && udpConnection.connected)
            {
                UDPMatchStateUpdate(udpConnection.lastMatchUpdate);
                UDPMatchStateUpdate(udpConnection.lastPlayerUpdate);
            }

            if (DeviceInfo.Instance.UseStandaloneSetup)
            {
                LocalPlayer.syncedTransforms.ForEach(t => OnNetworkTransformUpdate?.Invoke(t));
                LocalPlayer.syncedValues.ForEach(v => OnNetworkValueUpdate?.Invoke(v));
            }
            else
                UpdateNetworkSyncedObjects();
        }

        /// <summary>
        /// Check the socket connection and reconnect if needed
        /// </summary>
        /// <returns></returns>
        private async void CheckSocketConnectionAsync()
        {
            if (CurrentNetworkState == NetworkState.ERROR || DeviceInfo.Instance.UseStandaloneSetup)
            {
                Socket = null;
                return;
            }

            try
            {
                if ((Socket == null || !Socket.IsConnected) && client != null)
                {
                    Socket = client.NewSocket();

                    SetupSocketDebugEvents();
                    SetupSocketMatchEvents();

                    await Debug.LogQueue("Created socket: " + Socket, Instance);
                }

                if (Socket != null && session != null && !Socket.IsConnected && !Socket.IsConnecting)
                    await Socket.ConnectAsync(session, true, 3600);
            }
            catch (ApiResponseException ex)
            {
                Debug.LogError("Error connecting socket: " + ex.Message, Instance);
                UpdateNetworkState(NetworkState.ERROR, ex.Message);
                return;
            }
            catch (SocketException ex)
            {
                Debug.LogError("Socket exception: " + ex.Message, Instance);
                UpdateNetworkState(NetworkState.ERROR, ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in creating socket: " + ex, Instance);
                UpdateNetworkState(NetworkState.ERROR, ex.Message);
                return;
            }
            return;
        }

        /// <summary>
        /// Setup socket debug events for handling closing, erroring and notifications
        /// </summary>
        private void SetupSocketDebugEvents()
        {
            if (Socket == null)
                return;

            Debug.Log("Setup socket debug events", Instance);

            Socket.ReceivedError += SocketErrorMessage;
            Socket.ReceivedNotification += SocketNotification;
        }

        /// <summary>
        /// Handle match joining and leaving event to update players list
        /// </summary>
        private void SetupSocketMatchEvents()
        {
            if (Socket == null)
                return;

            Debug.Log("Setup socket match events", Instance);

            Socket.ReceivedMatchState += SocketMatchState;
            Socket.ReceivedMatchPresence += SocketPresenceUpdate;
        }

        /// <summary>
        /// Cancel the current cancellation token for network operations
        /// </summary>
        private void CancelToken()
        {
            if (networkTokenSource != null)
            {
                networkTokenSource.Cancel();
                networkTokenSource.Dispose();
                networkTokenSource = null;
            }
        }

        /// <summary>
        /// Start a new cancellation token for network operations
        /// </summary>
        private void StartNewToken()
        {
            CancelToken();
            networkTokenSource = new CancellationTokenSource();
        }

#if UNITY_IOS
        /// <summary>
        /// Handle the application focus event for iOS
        /// </summary>
        /// <param name="focus"></param>
        private void OnApplicationFocus(bool focus)
        {
#if UNITY_EDITOR
            if (!EnableApplicationFocusHandling)
                return;
#endif

            HandleApplicationLifecycleState(focus);
        }
#endif

#if UNITY_VISIONOS
        /// <summary>
        /// Handle the window update event for VisionOS
        /// </summary>
        /// <param name="state"></param>
        private void VisionOSWindowUpdate(VolumeCamera.WindowState state)
        {
#if UNITY_EDITOR
            if (!EnableApplicationFocusHandling)
                return;
#endif

            if ((state.WindowEvent == VolumeCamera.WindowEvent.Focused ||
                state.WindowEvent == VolumeCamera.WindowEvent.Opened) && CurrentNetworkState != NetworkState.RECONNECTING)
            {
                HandleApplicationLifecycleState(true);
            }
            else if (state.WindowEvent == VolumeCamera.WindowEvent.Backgrounded ||
                     state.WindowEvent == VolumeCamera.WindowEvent.Closed)
            {
                HandleApplicationLifecycleState(false);
            }
        }
#endif

        /// <summary>
        /// Handle the application lifecycle state changes for all platforms
        /// This method is called when the application gains or loses focus.
        /// </summary>
        /// <param name="isFocused"></param>
        private void HandleApplicationLifecycleState(bool isFocused)
        {
            if (isFocused)
            {
                Debug.Log("[ => ShareReceiver] Check for shared assets", Instance);
                ShareReceiver.CheckForSharedAsset();
            }

            if (CurrentNetworkState < NetworkState.CONNECTING)
                return;

            CancelToken();

            if (isFocused)
            {
                Debug.Log("Application focused", Instance);
                UpdateNetworkState(NetworkState.RECONNECTING);
            }
            else
            {
                Debug.Log("Application unfocused", Instance);
                UpdateNetworkState(NetworkState.DISCONNECT);
            }
        }

        /// <summary>
        /// Setup a fake standalone server for testing purposes, simulating a networked environment without actual network communication.
        /// </summary>
        private void SetupFakeStandaloneServer()
        {
            Debug.Log("Setting up fake standalone server", Instance);

            if (StandaloneServer == null)
                StandaloneServer = gameObject.AddComponent<StandaloneServer>();

            Dispatcher.Enqueue(() =>
            {
                if (LocalPlayer == null)
                {
                    LocalPlayer = StandaloneServer.StandalonePlayer();
                    StandaloneServer.SetupManagers();

                    RuntimePlayers.Add(LocalPlayer);
                    OnUserConnected?.Invoke();

                    if (Match == null)
                    {
                        Match = new FakeMatch();
                        OnMatchJoined?.Invoke();
                    }

                    Debug.Log("Network transforms: " + JsonUtility.ToJson(LocalPlayer.syncedTransforms), this);
                }
            });
        }

        /// <summary>
        /// Update the network state and handle the transitions between different states such as connecting, joining, leaving, disconnecting and matchmaking.
        /// </summary>
        /// <param name="networkState"></param>
        /// <param name="stateContext"></param>
        public void UpdateNetworkState(NetworkState networkState, string stateContext = "") =>
            _ = UpdateNetworkStateAsync(networkState, stateContext);

        /// <summary>
        /// Asynchronously update the network state and handle the transitions between different states such as connecting, joining, leaving, disconnecting and matchmaking.
        /// </summary>
        /// <param name="networkState"></param>
        /// <param name="stateContext"></param>
        /// <returns></returns>
        public async Task UpdateNetworkStateAsync(NetworkState networkState, string stateContext = "")
        {
            if (CurrentNetworkState == NetworkState.OFFLINE && networkState == NetworkState.DISCONNECT || !Application.isPlaying)
                return;

            if (DeviceInfo.Instance.UseStandaloneSetup)
            {
                if (CurrentNetworkState != NetworkState.STANDALONE && networkState != NetworkState.STANDALONE)
                {
                    Debug.Log("Starting standalone server", Instance);

                    await LeaveMatchAsync();

                    CurrentNetworkState = NetworkState.STANDALONE;
                    SetupFakeStandaloneServer();

                    XRUIManager.Instance.dynamicUIRoot.gameObject.SetActive(false);
                    XRUIManager.Instance.StationaryUIRoot.gameObject.SetActive(false);
                }
                else if (CurrentNetworkState == NetworkState.STANDALONE && networkState == NetworkState.LEAVING)
                {
                    Debug.Log("Leaving standalone server", Instance);

                    LocalPlayer = null;
                    RuntimePlayers.Clear();

                    await LeaveMatchAsync();

                    Dispatcher.Enqueue(() =>
                    {
                        XRUIManager.Instance.dynamicUIRoot.gameObject.SetActive(true);
                        XRUIManager.Instance.StationaryUIRoot.gameObject.SetActive(true);
                    });
                    UpdateNetworkState(NetworkState.CONNECTING);
                }

                return;
            }

            Debug.Log("Updating network state: " + networkState, Instance);

            NetworkState previousNetworkState = CurrentNetworkState;
            bool stateUpdated = false;

            StartNewToken();
            var networkToken = networkTokenSource.Token;

            Dispatcher.Enqueue(() => XRUIManager.Instance.HideAll());
            await Awaitable.MainThreadAsync();

            while (!networkToken.IsCancellationRequested && !stateUpdated && Application.isPlaying)
            {
                switch (networkState)
                {
                    case NetworkState.OFFLINE:
                        CurrentNetworkState = NetworkState.OFFLINE;
                        break;
                    case NetworkState.CONNECTING:
                        CurrentNetworkState = NetworkState.CONNECTING;
                        await Connect(networkToken);

                        while (!networkToken.IsCancellationRequested && !SocketIsConnected() && Application.isPlaying)
                        {
                            CheckSocketConnection();
                            await Task.Delay(1000);
                        }

                        if (SocketIsConnected())
                        {
                            Debug.Log("NAKAMA Connected", Instance);
                            retryCount = 5;
                            string autoJoinSessionID = await GetAutojoinSession();

                            if (!string.IsNullOrEmpty(autoJoinSessionID) && !StopAutoJoin)
                            {
                                LastSessionStored = autoJoinSessionID;
                                await UpdateNetworkStateAsync(NetworkState.JOINING, autoJoinSessionID);
                            }
                            else
                            {
                                await UpdateNetworkStateAsync(NetworkState.MATCHMAKING);
                            }
                        }
                        else
                            UpdateNetworkState(NetworkState.RECONNECTING);
                        break;
                    case NetworkState.JOINING:
                        CurrentNetworkState = NetworkState.JOINING;
                        await JoinMatchAsync(stateContext);

                        if (Match != null)
                            await UpdateNetworkStateAsync(NetworkState.JOINED);
                        else
                            await UpdateNetworkStateAsync(NetworkState.LEAVING, "Could not join match, match is null");
                        break;
                    case NetworkState.JOINED:
                        CurrentNetworkState = NetworkState.JOINED;

                        Dispatcher.Enqueue(() =>
                        {
                            XRUIManager.Instance.LoadUserUI(true);
                            OnMatchJoined?.Invoke();
                        });
                        break;
                    case NetworkState.LEAVING:
                        CurrentNetworkState = NetworkState.LEAVING;
                        await LeaveMatchAsync();

                        if (!string.IsNullOrEmpty(stateContext))
                            await HyperSlidesStateManager.Instance.UpdateStateWithDelay("Could not leave match\n" + stateContext, 1f);

                        if (previousNetworkState != NetworkState.MATCHMAKING)
                            await UpdateNetworkStateAsync(NetworkState.MATCHMAKING);

                        OnMatchLeft?.Invoke();
                        break;
                    case NetworkState.DISCONNECT:
                        CurrentNetworkState = NetworkState.DISCONNECT;
                        await Disconnect(networkToken);
                        XRSlideManager.Instance.Reset();
                        break;
                    case NetworkState.RECONNECTING:
                        CurrentNetworkState = NetworkState.RECONNECTING;

                        NewSessionCodeDetected = false;

                        await LeaveMatchAsync();
                        await Connect(networkToken);

                        while (!networkToken.IsCancellationRequested && !SocketIsConnected())
                        {
                            CheckSocketConnection();
                            await Task.Delay(1000);
                        }

                        if (SocketIsConnected())
                        {
                            Debug.Log("NAKAMA Connected", Instance);
                            await UpdateNetworkStateAsync(NetworkState.MATCHMAKING);
                        }
                        break;
                    case NetworkState.MATCHMAKING:
                        CurrentNetworkState = NetworkState.MATCHMAKING;

                        if (Socket != null && Socket.IsConnected)
                        {
                            if (!string.IsNullOrEmpty(LastSessionStored))
                            {
                                await UpdateNetworkStateAsync(NetworkState.JOINING, LastSessionStored);
                                return;
                            }
                        }

                        XRSlideManager.Instance.Reset();

                        Dispatcher.Enqueue(() => XRUIManager.Instance.LoadUserUI());
                        Dispatcher.Enqueue(() => XRUIManager.Instance.InitMatchMakingUI());

                        await GetActiveMatches(networkToken);
                        break;
                    case NetworkState.ERROR:
                        if (CurrentNetworkState == NetworkState.ERROR)
                            break;

                        CurrentNetworkState = NetworkState.ERROR;
                        Debug.LogError("Network error occurred, disconnecting...\n" + stateContext, Instance);

                        HyperSlidesStateManager.Instance.RemoveAllButtonInteractions();

                        if (UnityEngine.Debug.isDebugBuild)
                        {
                            Debug.Log(retryCount);
                            await HyperSlidesStateManager.Instance.UpdateStateWithDelay("Retrying connection in 3 seconds...\n" + $"({retryCount} / 5)", 3f);
                            UpdateNetworkState(NetworkState.CONNECTING);
                        }
                        else
                        {
                            await HyperSlidesStateManager.Instance.UpdateStateWithButton("Max retries reached. Please use the button below or restart the app.", new List<ButtonInteraction> {
                                new ButtonInteraction {
                                    buttonText = "Reconnect",
                                    callback = () => {
                                        retryCount = 5;
                                        UpdateNetworkState(NetworkState.CONNECTING);
                                    }
                                },
                                new ButtonInteraction {
                                    buttonText = "Quit application",
                                    callback = () => {
                                        Application.Quit();
                                    }
                                }
                            });
                        }
                        break;
                }

                stateUpdated = true;
            }
        }

        /// <summary>
        /// Connect to the nakama server and setup the device
        /// </summary>
        /// <returns></returns>
        public async Task Connect(CancellationToken networkToken)
        {
            try
            {
                Debug.Log("Connecting loop running...", Instance);

                await CheckTokenAndAwaitTask(CreateClient(), networkToken);
                if (ShouldStop(networkToken)) return;

                await CheckTokenAndAwaitTask(CreateSession(networkToken), networkToken);
                if (ShouldStop(networkToken)) return;

                await CheckTokenAndAwaitTask(ReceiveAccount(networkToken), networkToken);
                if (ShouldStop(networkToken)) return;

                await CheckTokenAndAwaitTask(ReadPresentationStorageObject(networkToken), networkToken);
                if (ShouldStop(networkToken)) return;

                await CheckTokenAndAwaitTask(SendPlayerMetaDataAsync(), networkToken);
                if (ShouldStop(networkToken)) return;

                OnUserConnected?.Invoke();
            }
            catch (OperationCanceledException e)
            {
                Debug.LogWarning("Connect() was canceled: " + e.Message);
                UpdateNetworkState(NetworkState.ERROR, e.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in connecting to nakama: " + ex, Instance);
                UpdateNetworkState(NetworkState.ERROR, ex.Message);
            }
        }

        /// <summary>
        /// Check if the token is cancelled or network state is error before awaiting the task
        /// </summary>
        /// <param name="task"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task CheckTokenAndAwaitTask(Task task, CancellationToken token)
        {
            if (token.IsCancellationRequested || CurrentNetworkState == NetworkState.ERROR)
                return;

            await task;
        }

        /// <summary>
        /// Helper to check if we should stop further execution
        /// </summary>
        private bool ShouldStop(CancellationToken token) => token.IsCancellationRequested || CurrentNetworkState == NetworkState.ERROR;

        /// <summary>
        /// Disconnect from the nakama server and release all events
        /// </summary>
        public async Task Disconnect(CancellationToken networkToken)
        {
            try
            {
                if (Match != null)
                    await LeaveMatchAsync();

                if (SocketIsConnected())
                    await Socket.CloseAsync();

                XRSlideManager.Instance.Reset();
                OnUserConnected?.Invoke();

                Debug.Log("NAKAMA Disconnected", Instance);
            }
            catch (Exception e)
            {
                Debug.LogError("Error in disconnecting from nakama: " + e, Instance);
            }
        }

        /// <summary>
        /// Create a new nakama client
        /// </summary>
        /// <returns></returns>
        private async Task CreateClient()
        {
            Debug.Log("Creating client with: " + JsonUtility.ToJson(HyperSlidesStateManager.Instance.CurrentServerConnection), Instance);

            if (client == null)
                client = new Client(
                HyperSlidesStateManager.Instance.CurrentServerConnection.Protocol,
                HyperSlidesStateManager.Instance.CurrentServerConnection.NakamaIP,
                HyperSlidesStateManager.Instance.CurrentServerConnection.Port,
                HyperSlidesStateManager.Instance.CurrentServerConnection.ServerKey
                );

            var retryConfiguration = new RetryConfiguration(500, 5, delegate { Debug.Log("about to retry."); }, (history, baseDelay, random) =>
            {
                const int delayCap = 20000;
                var lastAttempt = history.Last();
                var jitter = Mathf.Min(delayCap, random.Next(baseDelay, lastAttempt.JitterBackoff * 3));
                return jitter;
            });

            client.GlobalRetryConfiguration = retryConfiguration;

            await Debug.LogQueue("Created client: " + client, Instance);
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
                    session = await client.AuthenticateDeviceAsync(DeviceInfo.Instance.DeviceId, canceller: token);
                else if (session.IsExpired || session.HasExpired(DateTime.UtcNow.AddDays(1)))
                {
                    try
                    {
                        session = await client.SessionRefreshAsync(session, canceller: token);
                        PlayerPrefs.SetString("nakama.authToken", session.AuthToken);
                    }
                    catch (ApiResponseException apiEx)
                    {
                        // Couldn't refresh the session so reauthenticate.
                        session = await client.AuthenticateDeviceAsync(DeviceInfo.Instance.DeviceId, canceller: token);
                        PlayerPrefs.SetString("nakama.refreshToken", session.RefreshToken);

                        await Debug.LogQueue("Refreshed session: " + session + "\n" + apiEx, Instance);
                    }
                }
                await Debug.LogQueue("Created session: " + session, Instance);
            }
            catch (OperationCanceledException e)
            {
                Debug.LogWarning("CreateSession() was canceled: " + e.Message, Instance);
                UpdateNetworkState(NetworkState.ERROR, e.Message);
            }
            catch (ApiResponseException e)
            {
                Debug.LogError("Error creating session: " + e.StatusCode + ": " + e.Message, Instance);
                Debug.LogError("Device ID: " + DeviceInfo.Instance.DeviceId, Instance);
                UpdateNetworkState(NetworkState.ERROR, $"({e.StatusCode}) Please check your server connection settings and try again.");
            }
            catch (Exception e)
            {
                Debug.Log("Error creating session:" + e.Message);
                UpdateNetworkState(NetworkState.ERROR, $"{e.Message}. Please check your network settings.");
            }
        }

        /// <summary>
        /// Receive the current account from nakama and check for session code changes
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ReceiveAccount(CancellationToken token)
        {
            try
            {
                account = await client.GetAccountAsync(session, canceller: token);
                DeviceInfo.Instance.Role = FetchUserRole(account.User);

                Dispatcher.Enqueue(() =>
                {
                    Debug.Log("Received account: " + account, Instance);
                    PlayerPrefs.SetString("DeviceName", account.User.Username);

                    JToken jTokenSessionCode = JObject.Parse(account.User.Metadata)["sessionCode"];

                    if (jTokenSessionCode != null
                    && !GetSessionCode().Equals(jTokenSessionCode.Value<string>())
                    && !jTokenSessionCode.Value<string>().Equals("000-000"))
                    {
                        SetSessionCode(jTokenSessionCode.Value<string>());
                        NewSessionCodeDetected = true;
                    }
                });
            }
            catch (ApiResponseException e)
            {
                Debug.LogError("Error receiving account: " + e.StatusCode + ": " + e.Message, Instance);
                UpdateNetworkState(NetworkState.ERROR);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("ReceiveAccount was canceled", Instance);
                UpdateNetworkState(NetworkState.ERROR);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in receiving account: " + ex, Instance);
                UpdateNetworkState(NetworkState.ERROR);
            }
        }

        /// <summary>
        /// Set the session code based on the input from the UI
        /// </summary>
        /// <param name="id"></param>
        private void SetSessionCode(string id)
        {
            string sessionCode = id;

            if (id.Length == 6 && !id.Contains("-"))
                sessionCode = id.Insert(3, "-");

            if (sessionCode.Length > 6 && sessionCode.Contains("-"))
            {
                LastSessionStored = "";
                PlayerPrefs.SetString("sessionCode", sessionCode);
                Debug.Log("Update session code to: " + sessionCode, Instance);
            }
        }

        /// <summary>
        /// Get the autojoin match ID from the user persistent id or return the match id directly, if no match was found.
        /// This will work with updated and previous backend versions
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetAutojoinSession()
        {
            string userPersistentId = "";
            string matchID = "";

            if (account != null &&
                JObject.Parse(account.User.Metadata)["autojoinSession"] != null &&
                JObject.Parse(account.User.Metadata)["autojoinSession"].Value<string>().ToLower() != "none")
                userPersistentId = JObject.Parse(account.User.Metadata)["autojoinSession"].Value<string>();

            if (!string.IsNullOrEmpty(userPersistentId))
                matchID = await GetMatchIDFromPersistentID(networkTokenSource.Token, userPersistentId);

            return string.IsNullOrEmpty(matchID) ? userPersistentId : matchID;
        }

        /// <summary>
        /// Get match ID from all matches and return the one with the persistentId equals to the users one
        /// </summary>
        /// <param name="token"></param>
        /// <param name="userPersistentId"></param>
        /// <returns></returns>
        private async Task<string> GetMatchIDFromPersistentID(CancellationToken token, string userPersistentId)
        {
            Debug.Log($"Checking for match with persistent id: {userPersistentId}", Instance);

            IApiMatchList allSessions = await client.ListMatchesAsync(session, 0, 100, 100, true, "", "", canceller: token);
            string foundMatchID = "";

            allSessions.Matches.ToList().ForEach((match) =>
            {
                if (
                    string.IsNullOrEmpty(foundMatchID) &&
                    JObject.Parse(match.Label)["persistentId"] != null &&
                    JObject.Parse(match.Label)["persistentId"].Value<string>().Equals(userPersistentId))
                {
                    foundMatchID = match.MatchId;
                }
            });

            return foundMatchID;
        }

        private string GetPersistentIDFromMatchLabel()
        {
            if (Match == null || string.IsNullOrEmpty(Match.Label))
                return "";

            try
            {
                JToken jTokenPersistentId = JObject.Parse(Match.Label)["persistentId"];
                if (jTokenPersistentId != null)
                    return jTokenPersistentId.Value<string>();
            }
            catch (Exception e)
            {
                Debug.LogError("Error in getting persistent ID from match label: " + e, Instance);
            }

            return "";
        }

        /// <summary>
        /// Set the device name based on the UI input
        /// </summary>
        /// <param name="newUsername">The new username</param>
        /// <returns>False if not connected</returns>
        public async Task SetDeviceName(string newUsername, CancellationToken token)
        {
            //Debug.Log("Attempt to update username: " + newUsername);

            if (session == null || string.IsNullOrEmpty(newUsername))
                return;

            //Debug.Log("NAKAMA Update account async", this);
            deviceUserName = newUsername;
            await client.UpdateAccountAsync(session, newUsername, deviceUserName.ToUpper(), canceller: token);
        }

        /// <summary>
        /// Return the role of the given userid
        /// </summary>
        /// <param name="userId">The userid coming from nakama</param>
        /// <returns>The updated user role</returns>
        public XRPlayer.Role FetchUserRole(IApiUser user)
        {
            XRPlayer.Role role = XRPlayer.Role.Participant;
            XRNetworkObjects.Metadata metadata = JsonConvert.DeserializeObject<XRNetworkObjects.Metadata>(user.Metadata);

            role = XRPlayer.StringToRole(metadata.role);

#if UNITY_EDITOR
            if (session != null &&
                session.UserId == user.Id &&
                OverrideRole != XRPlayer.Role.Inherit &&
                role != OverrideRole)
            {
                Debug.LogWarning($"Overriding user role from {role} to {OverrideRole} as set in inspector", Instance);

                DeviceInfo.Instance.Role = OverrideRole;
                return OverrideRole;
            }
#endif

            return role;
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

        /// <summary>
        /// Read presentation transform overrides from nakama storage
        /// This is used to override the presentation transforms on presentation level
        /// </summary>
        /// <returns></returns>
        public async Task ReadPresentationTransformOverrideStorage()
        {
            //Clear on every new presentation read
            SessionTransformOverrides.Clear();

            try
            {
                string presentationId = XRSlideManager.Instance.CurrentPresentation != null ? XRSlideManager.Instance.CurrentPresentation.id : "default_presentation";

                var readObjectId = new StorageObjectId
                {
                    Collection = "PresentationTransforms",
                    Key = presentationId,
                    UserId = ""
                };

                var result = await client.ReadStorageObjectsAsync(session, new[] { readObjectId });

                if (result.Objects.Any())
                {
                    var storageObject = result.Objects.First();
                    List<XRNetworkObjects.SessionTransformOverride> presentationOverrides = XRDataManager.Instance.HandleJsonResponse<XRNetworkObjects.SessionTransformOverride>(storageObject.Value);
                    UpdateSessionTransformOverrides(presentationOverrides);

                    Debug.Log("Read presentation transform overrides from nakama storage: " + result.Objects.First(), Instance);
                }
                else
                    Debug.Log($"No presentation transform overrides found in storage for presentation id: {presentationId}", Instance);

                OnSessionTransformOverride?.Invoke();
            }
            catch (ApiResponseException e)
            {
                Debug.LogWarning("Error in reading presentation transform overrides from storage: " + e, Instance);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Error in reading storage ", Instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error in reading presentation transform overrides from storage: " + e, Instance);
            }
        }

        /// <summary>
        /// Read session transform overrides from nakama storage
        /// This is used to override the session transforms for specific users
        /// </summary>
        /// <returns></returns>
        public async Task ReadSessionTransformOverrideStorage()
        {
            try
            {
                string matchLabelPersistentId = GetPersistentIDFromMatchLabel();

                var readObjectId = new StorageObjectId
                {
                    Collection = "SessionTransforms",
                    Key = matchLabelPersistentId,
                    UserId = ""
                };

                var result = await client.ReadStorageObjectsAsync(session, new[] { readObjectId });

                if (result.Objects.Any())
                {
                    var storageObject = result.Objects.First();
                    List<XRNetworkObjects.SessionTransformOverride> sessionOverrides = XRDataManager.Instance.HandleJsonResponse<XRNetworkObjects.SessionTransformOverride>(storageObject.Value);
                    UpdateSessionTransformOverrides(sessionOverrides);

                    Debug.Log("Read session transform overrides from nakama storage: " + result.Objects.First(), Instance);
                }
                else
                    Debug.Log($"No session transform overrides found in storage for persistent id: {matchLabelPersistentId}", Instance);

                OnSessionTransformOverride?.Invoke();
            }
            catch (ApiResponseException e)
            {
                Debug.LogWarning("Error in reading session transform overrides from storage: " + e, Instance);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Error in reading storage ", Instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error in reading session transform overrides from storage: " + e, Instance);
            }
        }

        /// <summary>
        /// Update/combine sessiontransform overrides with presentation first and session second
        /// </summary>
        /// <param name="newOverrides"></param>
        public void UpdateSessionTransformOverrides(List<XRNetworkObjects.SessionTransformOverride> newOverrides)
        {
            Debug.Log("Updating transform overrides. New overrides count: " + newOverrides.Count, Instance);

            if (newOverrides == null || !newOverrides.Any())
                return;

            newOverrides.ForEach(newOverride =>
            {
                int index = SessionTransformOverrides.FindIndex(existing => existing.guid.Equals(newOverride.guid));

                if (index >= 0)
                    SessionTransformOverrides[index] = newOverride;
                else
                    SessionTransformOverrides.Add(newOverride);
            });

            OnSessionTransformOverride?.Invoke();
        }

        /// <summary>
        /// Get all currently active matches
        /// </summary>
        public async Task GetActiveMatches(CancellationToken token)
        {
            if (session == null && token.IsCancellationRequested)
                return;

            Debug.Log("GetActiveMatches called", Instance);

            IApiMatchList allSessions = await client.ListMatchesAsync(session, 0, 100, 100, true, "", "", canceller: token);
            FilteredMatchList = new();

            string userSessionCode = GetSessionCode();

            allSessions.Matches.ToList().ForEach(match =>
            {
                string matchSessionCode = "000-000";

                if (JObject.Parse(match.Label)["sessionCode"] != null)
                    matchSessionCode = JObject.Parse(match.Label)["sessionCode"].Value<string>();

                if (!string.IsNullOrEmpty(matchSessionCode) && matchSessionCode.Equals(userSessionCode))
                    FilteredMatchList.Add(match);
            });

            OnMatchListUpdate?.Invoke(FilteredMatchList);
        }

        /// <summary>
        /// Receive any notification coming from the nakama server
        /// </summary>
        /// <param name="notification">The notification</param>
        private void SocketNotification(IApiNotification notification)
        {
            Dispatcher.Enqueue(async () =>
            {
                Debug.Log($"Received notification:", Instance);
                Debug.Log(notification);

                switch ((XRNetworkObjects.MsgType)notification.Code)
                {
                    case XRNetworkObjects.MsgType.SLIDE_UPDATE:
                        await ReadPresentationStorageObject(networkTokenSource.Token);
                        break;
                    case XRNetworkObjects.MsgType.ROLE_UPDATE:
                        Debug.Log($"Update user role. Using notification code: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        UpdateNetworkState(NetworkState.RECONNECTING);
                        break;
                    case XRNetworkObjects.MsgType.RESET_ANCHORS:
                        Debug.Log($"Reset anchor setup for user. Using notification code: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        XRAnchorManager.Instance.StartOverAnchorSetup();
                        break;
                    case XRNetworkObjects.MsgType.SPAWN_PANEL:
                        Debug.Log($"Spawn panel for user. Using notification code: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        XRUIManager.Instance.ShowParticipantUI();
                        break;
                    case XRNetworkObjects.MsgType.SESSION_TRANSFORM_OVERRIDE:
                        Debug.Log($"Update session transforms from network: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        await ReadPresentationTransformOverrideStorage();
                        await ReadSessionTransformOverrideStorage();
                        break;
                    case XRNetworkObjects.MsgType.SETTING_OVERRIDE:
                        Debug.Log($"Update session settings from network: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        OnSettingOverride?.Invoke(notification);
                        break;
                    default:
                        Debug.Log($"Received generic notification: Code:  {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}, Id: {notification.Id}, Content: {notification.Content}", Instance);
                        OnCustomNotification?.Invoke(notification);
                        break;
                }
            });
        }

        /// <summary>
        /// Receive the current matchstate and handle using the provided <see cref="XRNetworkObjects.MsgType"/>
        /// </summary>
        /// <param name="matchState">The state as byte array</param>
        private void SocketMatchState(IMatchState matchState)
        {
            switch ((XRNetworkObjects.MsgType)matchState.OpCode)
            {
                case XRNetworkObjects.MsgType.PRESENTATION_UPDATE:
                    Dispatcher.Enqueue(() =>
                    {
                        try
                        {
                            //Debug.Log("PRESENTATION_UPDATE: " + Encoding.UTF8.GetString(matchState.State), this);
                            XRNetworkObjects.networkSessionState = ConvertNetworkToRuntimeObject<XRNetworkObjects.XRSessionState>(matchState.State);
                            RuntimeHandler.editModeEnabled = XRNetworkObjects.networkSessionState.editMode;

                            lastTimestamp = DateTime.UtcNow;

                            OnNetworkSlideUpdate?.Invoke(XRNetworkObjects.networkSessionState);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("STATE_UPDATE Error: " + e, this);
                        }
                    });
                    break;
                case XRNetworkObjects.MsgType.UNIFIED_TRANSFORM_UPDATE:
                    Dispatcher.Enqueue(() =>
                    {
                        try
                        {
                            //Debug.Log(Encoding.UTF8.GetString(matchState.State));
                            CurrentSlideIndex = JObject.Parse(Encoding.UTF8.GetString(matchState.State))["presentationContentIndex"].Value<int>();

                            //Animate to correct index if falling behind
                            if (lastTimestamp == null)
                                lastTimestamp = DateTime.UtcNow;

                            if (
                                lastTimestamp.AddSeconds(3) < DateTime.UtcNow &&
                                XRSlideManager.Instance.CurrentPresentation &&
                                Mathf.Abs(XRSlideManager.Instance.NetworkSlide - CurrentSlideIndex) > 0)
                            {
                                Debug.Log("Slide index falling behind locally: " + XRSlideManager.Instance.NetworkSlide + " => " + CurrentSlideIndex);
                                XRSlideManager.Instance.SetSlide(CurrentSlideIndex);
                            }

                            //Debug.Log("UNIFIED_STATE_UPDATE: " + Encoding.UTF8.GetString(matchState.State), this);
                            //Update synced transform values
                            JArray syncedTransforms = (JArray)JObject.Parse(Encoding.UTF8.GetString(matchState.State))["syncedTransforms"];
                            if (syncedTransforms != null)
                            {
                                foreach (JObject syncedTrans in syncedTransforms)
                                {
                                    XRNetworkObjects.NetworkSyncedTransform newSyncedTransform = syncedTrans.ToObject<XRNetworkObjects.NetworkSyncedTransform>();
                                    XRNetworkObjects.NetworkSyncedTransform foundTransform = NetworkSyncedTransforms.GetValueOrDefault(syncedTrans.GetValue("guid").ToString());

                                    if (foundTransform == null)
                                        NetworkSyncedTransforms.Add(newSyncedTransform.guid, newSyncedTransform);
                                    else
                                        foundTransform.UpdateTransform(newSyncedTransform);
                                }
                            }

                            //Update synced values array
                            JArray syncedValues = (JArray)JObject.Parse(Encoding.UTF8.GetString(matchState.State))["syncedValues"];
                            if (syncedValues != null)
                            {
                                foreach (JObject syncedValue in syncedValues)
                                {
                                    XRNetworkObjects.NetworkSyncedValue newSyncedValue = syncedValue.ToObject<XRNetworkObjects.NetworkSyncedValue>();
                                    XRNetworkObjects.NetworkSyncedValue foundValue = NetworkSyncedValues.GetValueOrDefault(syncedValue.GetValue("guid").ToString());

                                    if (foundValue == null)
                                        NetworkSyncedValues.Add(newSyncedValue.guid, newSyncedValue);
                                    else
                                        foundValue.UpdateValue(newSyncedValue);
                                }
                            }

                            //Distinguish between moderator and other users for pointer usage
                            JObject transformsObject = (JObject)JObject.Parse(Encoding.UTF8.GetString(matchState.State))["transforms"];
                            if (transformsObject != null)
                            {
                                foreach (var node in transformsObject.Properties())
                                {
                                    if (transformsObject[node.Name]["role"].Value<string>().Equals(XRPlayer.Role.Moderator.ToString()))
                                    {
                                        XRNetworkObjects.XRPlayer newModerator = transformsObject[node.Name].ToObject<XRNetworkObjects.XRPlayer>();
                                        OnNetworkModeratorUpdate?.Invoke(newModerator);
                                    }
                                    else
                                    {
                                        XRNetworkObjects.XRPlayer newTransform = transformsObject[node.Name].ToObject<XRNetworkObjects.XRPlayer>();
                                        OnNetworkPlayerUpdate?.Invoke(newTransform);
                                    }
                                }
                            }

                            JObject activeUsers = (JObject)JObject.Parse(Encoding.UTF8.GetString(matchState.State))["presences"];
                            if (activeUsers != null)
                            {
                                UserPresences.Clear();

                                foreach (var node in activeUsers.Properties())
                                {
                                    XRNetworkObjects.XRPlayer newPlayer = node.Value.ToObject<XRNetworkObjects.XRPlayer>();
                                    UserPresences.Add(newPlayer);
                                }

                                UserPresences.Sort((a, b) => a.UserId.CompareTo(b.UserId));
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("UNIFIED_STATE_UPDATE Error: " + e, this);
                            Debug.Log(Encoding.UTF8.GetString(matchState.State));
                        }
                    });
                    break;
                case XRNetworkObjects.MsgType.STOP_SESSION:
                    Dispatcher.Enqueue(() =>
                    {
                        try
                        {
                            Debug.Log("STOP_SESSION: " + Encoding.UTF8.GetString(matchState.State), this);
                            HyperSlidesStateManager.Instance.UpdateAppState(HyperSlidesStateManager.AppState.RUNNING);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("STOP_SESSION Error: " + e, this);
                        }
                    });
                    break;
            }
        }

        /// <summary>
        /// Receive UDP match state updates for low latency updates
        /// </summary>
        /// <param name="udpStateMessage">The state as byte array</param>
        public void UDPMatchStateUpdate(XRNetworkObjects.MatchUpdate matchUpdate)
        {
            // Single validation check
            if (matchUpdate == null)
                return;

            switch (matchUpdate.Status)
            {
                case XRNetworkObjects.MatchUpdate.MessageType.CONNECTION_CHECK:
                    break;
                case XRNetworkObjects.MatchUpdate.MessageType.PLAYER_UPDATE:
                    // Data is already deserialized as PlayerStates
                    // Debug.Log($"Received PLAYER_UPDATE: {JsonUtility.ToJson(matchUpdate.Data)}", this);
                    if (matchUpdate.Data is XRNetworkObjects.PlayerStates playerState)
                    {
                        if (playerState.timeStamp <= lastUDPPlayerStateTimestamp)
                            return;

                        lastUDPPlayerStateTimestamp = playerState.timeStamp;

                        XRNetworkObjects.XRPlayer[] userArray = playerState.playerStates;

                        UserPresences.Clear();

                        foreach (XRNetworkObjects.XRPlayer user in userArray)
                        {
                            // Update client latency if this is the local player
                            if (LocalPlayer != null && user.UserId == LocalPlayer.UserId)
                            {
                                ClientLatencyMs = (float)(DateTime.UtcNow - user.timeStamp).TotalMilliseconds;
                            }

                            // Update the new user. The update timestamp is checked on the XRPlayer side
                            if (user.role == XRPlayer.Role.Moderator.ToString())
                                OnNetworkModeratorUpdate?.Invoke(user);
                            else
                                OnNetworkPlayerUpdate?.Invoke(user);

                            UserPresences.Add(user);
                        }

                        UserPresences.Sort((a, b) => a.UserId.CompareTo(b.UserId));
                    }
                    break;

                case XRNetworkObjects.MatchUpdate.MessageType.MATCH_UPDATE:
                    // Data is already deserialized as NetworkSyncState
                    if (matchUpdate.Data is XRNetworkObjects.MatchState networkSyncState)
                    {
                        if (networkSyncState.timeStamp <= lastUDPUpdateTimestamp)
                            return;

                        lastUDPUpdateTimestamp = networkSyncState.timeStamp;

                        foreach (XRNetworkObjects.NetworkSyncedValue syncedValue in networkSyncState.syncedValues)
                        {
                            XRNetworkObjects.NetworkSyncedValue foundValue = NetworkSyncedValues.GetValueOrDefault(syncedValue.guid);

                            if (foundValue == null)
                            {
                                foundValue = syncedValue;
                                NetworkSyncedValues.Add(syncedValue.guid, syncedValue);
                            }
                            else
                                foundValue.UpdateValue(syncedValue);
                        }


                        foreach (XRNetworkObjects.NetworkSyncedTransform syncedTransform in networkSyncState.syncedTransforms)
                        {
                            XRNetworkObjects.NetworkSyncedTransform foundTransform = NetworkSyncedTransforms.GetValueOrDefault(syncedTransform.guid);

                            if (foundTransform == null)
                            {
                                foundTransform = syncedTransform;
                                NetworkSyncedTransforms.Add(syncedTransform.guid, syncedTransform);
                            }
                            else
                                foundTransform.UpdateTransform(syncedTransform);
                        }
                    }
                    break;

                default:
                    Debug.Log($"Unknown message type: {matchUpdate.Status}");
                    break;
            }
        }

        /// <summary>
        /// Invoke an update to all networksynced transforms and values
        /// </summary>
        public void UpdateNetworkSyncedObjects()
        {
            foreach (var syncedTransform in NetworkSyncedTransforms.Values)
                OnNetworkTransformUpdate?.Invoke(syncedTransform);

            foreach (var syncedValue in NetworkSyncedValues.Values)
                OnNetworkValueUpdate?.Invoke(syncedValue);
        }

        /// <summary>
        /// Handle joining and leaving players in a match
        /// </summary>
        /// <param name="matchPresenceEvent"></param>
        private void SocketPresenceUpdate(IMatchPresenceEvent matchPresenceEvent)
        {
            Dispatcher.Enqueue(() =>
            {
                foreach (IUserPresence presence in matchPresenceEvent.Joins)
                {
                    //Debug.Log(string.Format("SERVER Player joining: {0}", presence.UserId), this);
                    CreatePlayer(presence, matchPresenceEvent.Joins.ToList().IndexOf(presence));
                }

                foreach (IUserPresence presence in matchPresenceEvent.Leaves)
                {
                    XRPlayer foundPlayer = RuntimePlayers.Find(p => p.UserId == presence.UserId);

                    if (foundPlayer != null)
                        RemovePlayer(foundPlayer);
                }

                OnUsersPresencesUpdated?.Invoke();
            });
        }

        /// <summary>
        /// Convert any bytearray to the passed object type
        /// </summary>
        /// <typeparam name="T">The type to convert to</typeparam>
        /// <param name="data">The data as byte array</param>
        /// <returns>A runtime object of type T</returns>
        private T ConvertNetworkToRuntimeObject<T>(byte[] data)
        {
            var byteString = Encoding.UTF8.GetString(data);
            T newRunTimeObject = JsonConvert.DeserializeObject<T>(byteString);
            return newRunTimeObject;
        }

        /// <summary>
        /// Create a player, either as local or triggered by remote presence lists
        /// </summary>
        /// <param name="id"></param>
        private void CreatePlayer(IUserPresence presence, int userIndex)
        {
            if (RuntimePlayers.Find(p => p.UserId == presence.UserId) != null)
                return;

            Dispatcher.Enqueue(async () =>
            {
                while (Match == null)
                    await Task.Delay(1);

                bool isLocalUser = session.UserId == presence.UserId;

                XRPlayer newPlayer = null;

                if (isLocalUser)
                {
                    if (IsObservingRole(DeviceInfo.Instance.Role))
                        return;

                    newPlayer = Instantiate(HyperSlidesStateManager.Instance.Settings.LocalPlayerPrefab).GetComponent<XRPlayer>();

                    LocalPlayer = newPlayer;
                    LocalPlayer.UpdatePlayer(presence, isLocalUser);
                    LocalPlayer.UpdateRole(DeviceInfo.Instance.Role);

                    if (DeviceInfo.Instance.Role is XRPlayer.Role.Simulation && XRSlideManager.Instance.CurrentPresentation != null)
                        XRCameraManager.Instance.UpdateXROriginPosition(3);

                    newPlayer.transform.SetParent(XRContentRoot.Instance.transform, false);
                    RuntimePlayers.Add(newPlayer);
                    OnUsersPresencesUpdated?.Invoke();
                }
                else
                {
                    IApiUsers returnedUsersByID = await client.GetUsersAsync(session, new string[] { presence.UserId });

                    XRNetworkObjects.Metadata userMetaData = JsonConvert.DeserializeObject<XRNetworkObjects.Metadata>(returnedUsersByID.Users.First().Metadata);
                    XRPlayer.Role newPlayerRole = returnedUsersByID.Users != null && returnedUsersByID.Users.Count() > 0 ?
                        FetchUserRole(returnedUsersByID.Users.First()) :
                        XRPlayer.Role.Participant;

                    if (IsObservingRole(newPlayerRole))
                        return;

                    newPlayer = Instantiate(HyperSlidesStateManager.Instance.Settings.PlayerPrefab).GetComponent<XRPlayer>();
                    newPlayer.UpdatePlayer(presence, isLocalUser);
                    newPlayer.UpdateRole(newPlayerRole);

                    newPlayer.transform.SetParent(XRContentRoot.Instance.transform, false);
                    RuntimePlayers.Add(newPlayer);
                    OnUsersPresencesUpdated?.Invoke();
                }
            });
        }

        /// <summary>
        /// Check if the given role is an observing role. Use this to avoid creation of runtime prefabs for other users in the session
        /// </summary>
        /// <param name="role">The role to check</param>
        /// <returns>True if the role is an observing role</returns>
        private bool IsObservingRole(XRPlayer.Role role) => role == XRPlayer.Role.Admin || role == XRPlayer.Role.Simulation || role == XRPlayer.Role.Broadcast;

        /// <summary>
        /// Remove players from players list, that have been disconnected on the server
        /// </summary>
        /// <param name="player"></param>
        private void RemovePlayer(XRPlayer player)
        {
            Dispatcher.Enqueue(() =>
            {
                player.DestroyDependencies();

                Destroy(player.gameObject);
                RuntimePlayers.Remove(player);

                OnUsersPresencesUpdated?.Invoke();
            });
        }

        /// <summary>
        /// Move the local player and send its update to the session clients
        /// </summary>
        public void MovePlayer()
        {
            LocalPlayer.localPosition = XRContentRoot.Instance.transform.InverseTransformPoint(XRInputManager.Instance.LocalPoseDriver.transform.position);
            LocalPlayer.localRotation = Quaternion.Inverse(XRContentRoot.Instance.transform.rotation) * XRInputManager.Instance.LocalPoseDriver.transform.rotation;

            LocalPlayer.transform.localPosition = LocalPlayer.localPosition;
            LocalPlayer.transform.localRotation = LocalPlayer.localRotation;
        }

        /// <summary>
        /// Send this within the tick rate of <see cref="RuntimeHandler.Tick"/>
        /// </summary>
        public void SendUpdatedPlayer()
        {
            if (Socket == null || Match == null || LocalPlayer == null || LocalPlayer.gameObject == null)
                return;

            //Do not send any player updates to the server as spectator
            if (DeviceInfo.Instance.Role == XRPlayer.Role.Simulation)
                return;

            //Use the simplified version for the player to send less data over network
            XRNetworkObjects.networkPlayer.UserId = LocalPlayer.UserId;
            XRNetworkObjects.networkPlayer.Username = LocalPlayer.Username;
            XRNetworkObjects.networkPlayer.deviceType = DeviceInfo.Instance.DeviceType.ToString();
            XRNetworkObjects.networkPlayer.timeStamp = DateTime.Now;
            XRNetworkObjects.networkPlayer.role = DeviceInfo.Instance.Role.ToString();
            XRNetworkObjects.networkPlayer.position = LocalPlayer.localPosition;
            XRNetworkObjects.networkPlayer.rotation = LocalPlayer.localRotation.eulerAngles;

            if (!udpConnection.connected)
            {
                XRNetworkObjects.networkPlayer.syncedValues = LocalPlayer.syncedValues.ToArray();
                XRNetworkObjects.networkPlayer.syncedTransforms = LocalPlayer.syncedTransforms.ToArray();
            }

            XRNetworkObjects.networkPlayer.Status = XRNetworkObjects.XRPlayer.MessageType.UPDATE;

            try
            {
                XRNetworkObjects.networkPlayer.LeftPointer = new XRNetworkObjects.XRPointer(LocalPlayer.UserId, Handedness.Left, LocalPlayer.xrPointerLeft.gameObject);
                XRNetworkObjects.networkPlayer.RightPointer = new XRNetworkObjects.XRPointer(LocalPlayer.UserId, Handedness.Right, LocalPlayer.xrPointerRight.gameObject);

                if (XRNetworkObjects.networkPlayer.role != XRPlayer.Role.Moderator.ToString())
                {
                    XRNetworkObjects.networkPlayer.LeftPointer = null;
                    XRNetworkObjects.networkPlayer.RightPointer = null;
                }

                string jsonString = JsonConvert.SerializeObject(XRNetworkObjects.networkPlayer, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                if (!udpConnection.connected)
                    Socket.SendMatchStateAsync(Match.Id, (long)XRNetworkObjects.MsgType.TRANSFORM_UPDATE, jsonString);
                else
                    SendUDPState();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error on sending match state async: " + e.Message, this);
            }

            LocalPlayer.syncedValues.Clear();
            LocalPlayer.syncedTransforms.Clear();
        }

        /// <summary>
        /// Send the current UDP state of the local player
        /// </summary>
        private void SendUDPState()
        {
            XRNetworkObjects.MatchUpdate updateMessage = new()
            {
                Status = XRNetworkObjects.MatchUpdate.MessageType.PLAYER_UPDATE,
                Data = XRNetworkObjects.networkPlayer
            };

            XRNetworkObjects.MatchState syncState = new()
            {
                syncedTransforms = LocalPlayer.syncedTransforms.ToArray(),
                syncedValues = LocalPlayer.syncedValues.ToArray()
            };

            XRNetworkObjects.MatchUpdate updateNetworkMessage = new()
            {
                Status = XRNetworkObjects.MatchUpdate.MessageType.MATCH_UPDATE,
                Data = syncState
            };

            SendUDPMessage(updateMessage);
            SendUDPMessage(updateNetworkMessage);
        }

        private void SendUDPMessage(XRNetworkObjects.MatchUpdate messageObject)
        {
            byte[] data = MessagePackSerializer.Serialize(messageObject);

            if (udpConnection.connected)
                udpConnection.udpClient.Send(data, data.Length);
            else
                Debug.LogWarning("UDP connection not established, cannot send UDP message", this);
        }

        /// <summary>
        /// Send the current slide update
        /// </summary>
        /// <param name="presentationID"></param>
        /// <param name="contentIndex"></param>
        public void SendSlideUpdate(XRPresentation presentation, int contentIndex)
        {
            if (presentation == null)
            {
                Debug.LogWarning("Cannot send slide update, presentation is null", Instance);
                return;
            }

            if (presentation.slides.Count > 0)
                contentIndex = contentIndex.Clamp(1, presentation.contents.Count);

            XRNetworkObjects.networkSessionState.presentationId = presentation.id;
            XRNetworkObjects.networkSessionState.presentationName = presentation.title;
            XRNetworkObjects.networkSessionState.presentationContentIndex = contentIndex;

            Debug.Log("Slide update sending by moderator: " + JsonUtility.ToJson(XRNetworkObjects.networkSessionState), this);

            if (DeviceInfo.Instance.UseStandaloneSetup)
            {
                Debug.Log("Standalone mode: Sending presentation update", Instance);
                XRSlideManager.Instance.SetSlide(contentIndex);
                return;
            }

            Socket.SendMatchStateAsync(Match.Id, (long)XRNetworkObjects.MsgType.PRESENTATION_UPDATE, JsonConvert.SerializeObject(XRNetworkObjects.networkSessionState, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            }));
        }

        /// <summary>
        /// Wrapper to send next slide asynchronously
        /// </summary>
        public void SendSlideNextUpdate()
        {
            if (SocketIsConnected() && Match != null)
                SendSlideNextUpdateAsync();

            if (DeviceInfo.Instance.UseStandaloneSetup)
            {
                Debug.Log("Standalone mode: Sending next slide update", Instance);
                XRSlideManager.Instance.CurrentSlidePosition += 1;
                XRSlideManager.Instance.SetSlide(XRSlideManager.Instance.CurrentSlidePosition);

                PlayerPrefs.SetInt("CurrentSlidePosition", XRSlideManager.Instance.CurrentSlidePosition);
            }
        }

        /// <summary>
        /// Wrapper to send previous slide asynchronously
        /// </summary>
        public void SendSlidePrevUpdate()
        {
            if (SocketIsConnected() && Match != null)
                SendSlideNPrevUpdateAsync();

            if (DeviceInfo.Instance.UseStandaloneSetup)
            {
                Debug.Log("Standalone mode: Sending previous slide update", Instance);
                XRSlideManager.Instance.CurrentSlidePosition -= 1;
                XRSlideManager.Instance.SetSlide(XRSlideManager.Instance.CurrentSlidePosition);

                PlayerPrefs.SetInt("CurrentSlidePosition", XRSlideManager.Instance.CurrentSlidePosition);
            }
        }

        /// <summary>
        /// Send the next slide update to the server
        /// </summary>
        private async void SendSlideNextUpdateAsync()
        {
            try
            {
                await Socket.SendMatchStateAsync(Match.Id, (long)XRNetworkObjects.MsgType.NEXT_SLIDE, "{}");
                Debug.Log($"Sent next slide update", Instance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send next slide: {ex.Message}", Instance);
            }
        }

        /// <summary>
        /// Send the previous slide update to the server
        /// </summary>
        private async void SendSlideNPrevUpdateAsync()
        {
            try
            {
                await Socket.SendMatchStateAsync(Match.Id, (long)XRNetworkObjects.MsgType.PREV_SLIDE, "{}");
                Debug.Log($"Sent previous slide update", Instance);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to send previous slide: {ex.Message}", Instance);
            }
        }

        /// <summary>
        /// Send the player metadata to the server
        /// </summary>
        private void SendPlayerMetaData() => _ = SendPlayerMetaDataAsync(XRPlayer.Role.Inherit);

        /// <summary>
        /// Send the player metadata to the server
        /// </summary>
        /// <param name="roleUpdate"></param>
        /// <returns></returns>
        public async Task SendPlayerMetaDataAsync(XRPlayer.Role roleUpdate = XRPlayer.Role.Inherit)
        {
            if (session == null)
                return;

            try
            {
                XRNetworkObjects.XRPlayerMetadata playerMetadata = new()
                {
                    userId = session.UserId,
                    metadata = Nakama.TinyJson.JsonParser.FromJson<XRNetworkObjects.Metadata>(account.User.Metadata)
                };

                //Update all values
                playerMetadata.metadata.ip = DeviceInfo.Instance.GetLocalIPAddress();
                playerMetadata.metadata.battery = (int)(SystemInfo.batteryLevel * 100);
                playerMetadata.metadata.sessionCode = GetSessionCode();
                playerMetadata.metadata.location = DeviceInfo.Instance.Location;
                playerMetadata.metadata.deviceType = DeviceInfo.Instance.DeviceType;

                if (roleUpdate != XRPlayer.Role.Inherit)
                    playerMetadata.metadata.role = $"{(int)roleUpdate}";
                else
                    playerMetadata.metadata.role = $"{(int)DeviceInfo.Instance.Role}";

                string metaString = JsonUtility.ToJson(playerMetadata);

                await client.RpcAsync(session, "UPDATE_USER", metaString);
            }
            catch (ApiResponseException e)
            {
                Debug.LogError("Error sending player metadata: " + e.Message, Instance);
                UpdateNetworkState(NetworkState.ERROR, e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending player metadata: " + e.Message, Instance);
                UpdateNetworkState(NetworkState.ERROR, e.Message);
            }
        }

        /// <summary>
        /// Join a match
        /// </summary>
        /// <param name="matchID">The match id</param>
        /// <returns></returns>
        public async Task JoinMatchAsync(string matchID)
        {
            try
            {
                Match = await Socket.JoinMatchAsync(matchID);

                if (Match == null)
                {
                    StopAutoJoin = true;

                    await Debug.LogQueue("Could not connect to match!", Instance);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error joining match {matchID}: " + e.Message, Instance);
            }
            finally
            {
                Dispatcher.Enqueue(async () =>
                {
                    LastSessionStored = GetUserMatchID();

                    if (Match != null && Match.Label != null)
                    {
                        foreach (var presence in Match.Presences)
                            CreatePlayer(presence, Match.Presences.ToList().IndexOf(presence));

                        await ReadPresentationTransformOverrideStorage();
                        await ReadSessionTransformOverrideStorage();

                        JObject labelObj = null;

                        try
                        {
                            labelObj = JObject.Parse(Match.Label);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("Error parsing match label: " + e.Message, Instance);
                            return;
                        }

                        try
                        {
                            string orkanIP = labelObj["udpServer"] != null ?
                                labelObj["udpServer"].Value<string>() :
                                "";
                            int orkanPort = labelObj["udpPort"] != null ?
                                labelObj["udpPort"].Value<int>() :
                                0;
                            if (!string.IsNullOrEmpty(orkanIP) && orkanPort != 0 && udpConnection.ConnectToMatch(orkanIP, orkanPort.ToString()))
                                Debug.Log("UDP Client created and connected", Instance);
                            else
                                Debug.LogWarning("Could not create UDP client", Instance);
                        }
                        catch { }

                        try
                        {
                            float tickRate = labelObj["tickRate"] != null ?
                                labelObj["tickRate"].Value<float>() :
                                0f;

                            HyperSlidesStateManager.Instance.Settings.updateRate = tickRate > 0 ? tickRate : HyperSlidesStateManager.Instance.Settings.backupUpdateRate;
                        }
                        catch { }
                    }

                    SendPlayerMetaData();
                });
            }
        }

        /// <summary>
        /// Leave the current match from UI buttons
        /// </summary>
        public void LeaveMatch()
        {
            LastSessionStored = "";
            UpdateNetworkState(NetworkState.LEAVING);
        }

        /// <summary>
        /// Leave match triggered by application quit to have updated values on runtime builds
        /// </summary>
        /// <returns></returns>
        public async Task LeaveMatchAsync()
        {
            try
            {
                StopAutoJoin = true;
                lastUDPPlayerStateTimestamp = DateTime.MinValue;
                lastUDPUpdateTimestamp = DateTime.MinValue;

                HyperSlidesStateManager.Instance.Settings.updateRate = HyperSlidesStateManager.Instance.Settings.backupUpdateRate;

                SessionTransformOverrides.Clear();
                NetworkSyncedTransforms.Clear();
                NetworkSyncedValues.Clear();

                //Destroy all current players and reset slide contents
                RuntimePlayers.ForEach(p => RemovePlayer(p));
                OnMatchLeft?.Invoke();

                if (Match != null)
                {
                    udpConnection.LeaveMatch();

                    if (SocketIsConnected())
                        await Socket.LeaveMatchAsync(Match);
                }

                Match = null;
            }
            catch (Nakama.ApiResponseException e)
            {
                Debug.LogError("Error leaving match: " + e.Message, this);
                UpdateNetworkState(NetworkState.ERROR, e.Message);
            }
            catch (Exception e)
            {
                Debug.LogError("Error leaving match: " + e.Message, this);
                UpdateNetworkState(NetworkState.ERROR, e.Message);
            }
        }

        /// <summary>
        /// Handle application quit, leave the match and close the socket
        /// </summary>
        private void OnApplicationQuit()
        {
            CancelToken();

            //Ignore async warning on application quit
            _ = LeaveMatchAsync();

            Socket = null;
            Match = null;
        }

        /// <summary>
        /// The matchstate which is created when first joining the match and getting the presentatino data
        /// </summary>
        [Serializable]
        public class InitialMatchState
        {
            public string presentationId;
            public string presentationName;

            public InitialMatchState(string id, string title)
            {
                presentationId = id;
                presentationName = title;
            }
        }

        public async Task<long> GetNakamaLatencySimple()
        {
            if (client == null || session == null)
                return -1;

            if (CurrentNetworkState != NetworkState.JOINED)
                return -1;

            try
            {
                var startTime = DateTime.UtcNow;

                // Use a lightweight operation like getting account info
                await client.GetAccountAsync(session);

                var endTime = DateTime.UtcNow;
                return (long)(endTime - startTime).TotalMilliseconds;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error measuring Nakama latency: " + e.Message, Instance);
                return -1;
            }
        }
    }
}