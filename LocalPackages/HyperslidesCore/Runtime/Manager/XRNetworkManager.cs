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

namespace NSYNK.HyperSlides.Network
{
    /// <summary>
    /// The nakama connection manager, which handles all local and remote player creation and updating for now
    /// </summary>
    public class XRNetworkManager : Singleton<XRNetworkManager>
    {
        public delegate void MatchListUpdates(List<IApiMatch> matches);
        public delegate void NetworkUpdate();
        public delegate void NetworkUpdate<T>(T networkObject);

        public static MatchListUpdates onMatchListUpdate;
        public static List<IApiMatch> FilteredMatchList = new List<IApiMatch>();
        public static NetworkUpdate onUserConnected;
        public static NetworkUpdate onMatchJoined, onMatchLeft, onSessionTransformOverride;
        public static NetworkUpdate<XRNetworkObjects.XRSessionState> onNetworkSlideUpdate;
        public static NetworkUpdate<XRNetworkObjects.XRPlayer> onNetworkPlayerUpdate;
        public static NetworkUpdate<XRNetworkObjects.XRModerator> onNetworkModeratorUpdate;
        public static NetworkUpdate<XRNetworkObjects.NetworkSyncedTransform> onNetworkTransformUpdate;
        public static NetworkUpdate<XRNetworkObjects.NetworkSyncedValue> onNetworkdValueUpdate;

        public static NetworkUpdate<IApiNotification> onCustomNotification, onSettingOverride;

        public static List<XRNetworkObjects.NetworkSyncedTransform> networkSyncedTransforms = new();
        public static List<XRNetworkObjects.NetworkSyncedValue> networkSyncedValues = new();
        public static List<XRNetworkObjects.SessionTransformOverride> sessionTransformOverrides = new();

        [ReadOnly]
        public string currentTick = "0";
        [ReadOnly]
        public int ticksSinceStartup, currentSlideIndex = 0;
        public List<XRNetworkObjects.NetworkSyncedTransform> inspectorSyncedTransforms = new();
        public List<XRNetworkObjects.NetworkSyncedValue> inspectorSyncedValues = new();
        public List<XRNetworkObjects.SessionTransformOverride> inspectorSessionTransformOverrides = new();

        public static XRPlayer localPlayer = null;
        public static CancellationTokenSource nakamaTokenSource = new CancellationTokenSource();
        public static IMatch match;
        public static bool IsConnected() => Instance.socket != null;
        public static string lastSessionStored = "";
        public static bool newSessionCodeDetected = false;
        public static bool stopAutoJoin = false;

        [Header("Development overrides")]
        public XRPlayer.Role editorRole;

        [Header("Predefined objects/components")]
        public Transform xrUserOrigin;
        public GameObject spectatorPrefab;
        public GameObject localPlayerPrefab;
        public GameObject playerPrefab;

        [Header("Runtime values")]
        public XRPlayer.Role inspectorRole;
        public List<XRPlayer> players = new List<XRPlayer>();

        private Client client;
        private ISession session;
        private ISocket socket;
        private IApiAccount account;
        private string username = "";
        private DateTime lastTimestamp;
        private CancellationTokenSource networkTokenSource = new CancellationTokenSource();
        private Thread mainThread;
        private readonly SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);

        private void OnEnable()
        {
            RuntimeHandler.slowTick += SendPlayerMetaData;

#if UNITY_VISIONOS
            XRCameraManager.windowUpdateEvent += VisionOSWindowUpdate;
#endif
        }

        private void OnDisable()
        {
            RuntimeHandler.slowTick -= SendPlayerMetaData;

#if UNITY_VISIONOS
            XRCameraManager.windowUpdateEvent -= VisionOSWindowUpdate;
#endif
        }

        public void Start()
        {
            mainThread = System.Threading.Thread.CurrentThread;
        }

        bool isMainThread()
        {
            return mainThread.Equals(System.Threading.Thread.CurrentThread);
        }

        /// <summary>
        /// Cancel the current network token source
        /// </summary>
        private void CancelToken()
        {
            if (networkTokenSource != null)
                networkTokenSource.Cancel(); // Cancel everything
        }

        /// <summary>
        /// Handle the application focus event for iOS
        /// </summary>
        /// <param name="focus"></param>
        private void OnApplicationFocus(bool focus)
        {
#if UNITY_IOS && !UNITY_EDITOR
            CancelToken();

            if (focus) 
            {
                Debug.Log("Reconnect when application returns from background");
                Reconnect();
            }
            else
            {
                Debug.Log("Disconnect on application went background");
                Disconnect();
            }
#endif
        }

        /// <summary>
        /// Handle the window update event for VisionOS
        /// </summary>
        /// <param name="state"></param>
        private void VisionOSWindowUpdate(VolumeCamera.WindowState state)
        {
#if UNITY_VISIONOS && !UNITY_EDITOR
            CancelToken();

            if (HyperSlidesStateManager.appState <= HyperSlidesStateManager.AppState.DEVICE_SETUP)
                return;

            if (state.WindowEvent != VolumeCamera.WindowEvent.Backgrounded)
            {
                Debug.Log("Reconnect when application returns from background", Instance);
                Reconnect();
            }
            else if (state.WindowEvent == VolumeCamera.WindowEvent.Backgrounded)
            {
                Debug.Log("Disconnect on application went background", Instance);
                Disconnect();
            }
#endif
        }

        /// <summary>
        /// Connect to the nakama server and create client, socket and session
        /// </summary>
        public async Task Init()
        {
            await Connect();
        }

        /// <summary>
        /// Connect to the nakama server and setup the device
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                Debug.Log("Connecting loop starting", Instance);

                bool setupDone = false;

                ReleaseSocketEvents();

                networkTokenSource = new CancellationTokenSource();
                CancellationToken networkToken = networkTokenSource.Token;

                while (!networkToken.IsCancellationRequested && !setupDone)
                {
                    await WaitForSessionIDInput();
                    await SelectServerRegion();
                    await CreateClient();
                    await CreateSession(networkToken);
                    await CreateSocket();
                    await ReceiveAccount(networkToken);
                    await ReadStorageObject(networkToken);
                    await SendPlayerMetaDataAsync();

                    onUserConnected?.Invoke();

                    await GetActiveMatches(networkToken);

                    SetupSocketDebugEvents();
                    setupDone = true;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Connect() was canceled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in Connect(): {ex} / {ex.InnerException}", Instance);
                CancelToken();
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Disconnect from the nakama server and release all events
        /// </summary>
        public void Disconnect()
        {
            ReleaseSocketEvents();

            // only dispatch to the main thread if we aren't already on it. Otherwise disconnect doesn't work on iOS focus loss
            if (isMainThread())
            {
                Debug.Log("Disconnecting on main thread");
                disconnectAction();
            }
            else
                Dispatcher.Enqueue(disconnectAction);
        }

        private async void disconnectAction()
        {
            await _connectionSemaphore.WaitAsync();
            try
            {
                if (socket != null)
                    await socket.CloseAsync();
                socket = null;
                client = null;
                session = null;
                account = null;
                Debug.Log("NAKAMA Disconnected", Instance);
            }
            finally
            {
                _connectionSemaphore.Release();
            }

        }

        /// <summary>
        /// Try reconnecting to the current socket, session and match
        /// </summary>
        public void Reconnect()
        {
            Dispatcher.Enqueue(async () => await ReconnectTask());
        }

        private async Task ReconnectTask()
        {
            ReleaseSocketEvents();

            Debug.LogWarning("NAKAMA Reconnecting...", Instance);
            await Connect();

            if (socket != null && socket.IsConnected)
            {
                if (match != null)
                    await JoinMatchAsync(match.Id);
            }
        }

        /// <summary>
        /// Waiting for the user input on the UI inputfield
        /// </summary>
        /// <returns>Returns back to previous task, when the sessioncode is valid</returns>
        public async Task WaitForSessionIDInput()
        {
            if (networkTokenSource.IsCancellationRequested)
                return;

            Dispatcher.Enqueue(() =>
            {
                Debug.Log("Current session code: " + GetSessionCode(), Instance);
            });

            if (GetSessionCode() == "000-000")
            {
                HyperSlidesStateManager.EnableInputfield("000-000", SetSessionCode);

                while (string.IsNullOrEmpty(GetSessionCode()) || GetSessionCode() == "000-000")
                    await HyperSlidesStateManager.UpdateStateWithDelay("Please enter your Session ID", 1);

                await SendPlayerMetaDataAsync();
                Reconnect();
            }
            else
            {
                Debug.Log("Current session code: " + GetSessionCode(), Instance);
            }
        }
        /// <summary>
        /// Select the server region based on the sessioncode or use the default one from settings
        /// </summary>
        /// <returns>Returns back when the node API has responded with the correct node</returns>
        public async Task SelectServerRegion()
        {
            if (DeviceInfo.ServerOverride)
            {
                if (!string.IsNullOrEmpty(DeviceInfo.ServerOverrideBackendIP))
                {
                    RuntimeHandler.Settings.NakamaProfile.IP = DeviceInfo.ServerOverrideNakamaIP;
                    RuntimeHandler.Settings.NakamaProfile.Port = DeviceInfo.ServerOverrideNakamaPort;
                    RuntimeHandler.Settings.NakamaProfile.SSL = DeviceInfo.ServerOverrideNakamaSSL;

                    await Debug.LogQueue($"Using local server from settings:  {RuntimeHandler.Settings.NakamaProfile.IP}", Instance);
                    return;
                }
                else
                    await Debug.LogQueue($"Server IP is empty, using settings file IP: {RuntimeHandler.Settings.NakamaProfile.IP}", Instance);
            }

            string url = RuntimeHandler.Settings.backend + "/api/utils/server-select";
            SessionCode code = new SessionCode(GetSessionCode());
            using UnityWebRequest www = UnityWebRequest.Post(url, JsonUtility.ToJson(code), "application/json");
            await www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("Could not get region, using default DE server! Error on: " + url +
                "\nSent JSON: " + JsonUtility.ToJson(code) +
                "\nError:" + www.error);
            else
            {
                try
                {
                    JToken jTokenIP = JObject.Parse(www.downloadHandler.text)["node"]["ip"];
                    JToken jTokenRegion = JObject.Parse(www.downloadHandler.text)["node"]["label"];

                    string ip = jTokenIP.Value<string>();
                    string region = jTokenRegion.Value<string>();

                    if (ip.Contains("https://"))
                        ip = ip.Remove(0, 8);

                    RuntimeHandler.Settings.NakamaProfile.IP = ip;
                    Debug.Log("Connecting to region " + jTokenRegion + $" ({ip})", Instance);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Error in getting server region ip: " + e);
                }
            }

            www.Dispose();
        }

        /// <summary>
        /// Set current session transform overrides from string
        /// </summary>
        /// <param name="jsonString"></param>
        private void SetMatchMovableRoots(string jsonString)
        {
            if (match == null)
                return;

            try
            {
                //Overrides transform overrides from matchstate if match label update is not available
                JArray syncedSessionTransformOverride = (JArray)JObject.Parse(jsonString)["sessionTransformOverride"];

                if (syncedSessionTransformOverride == null)
                    syncedSessionTransformOverride = (JArray)JObject.Parse(jsonString)["data"];

                if (syncedSessionTransformOverride != null)
                {
                    foreach (JObject transformOverride in syncedSessionTransformOverride)
                    {
                        XRNetworkObjects.SessionTransformOverride newOverride = transformOverride.ToObject<XRNetworkObjects.SessionTransformOverride>();
                        XRNetworkObjects.SessionTransformOverride foundOverride = sessionTransformOverrides.Find(t => t.guid == transformOverride.GetValue("guid").ToString());

                        if (foundOverride == null)
                            sessionTransformOverrides.Add(newOverride);
                        else
                            foundOverride.UpdateOverride(newOverride);
                    }
                }
                else
                    Debug.LogWarning("No session transform overrides found in match label update", Instance);

                onSessionTransformOverride?.Invoke();

#if UNITY_EDITOR
                Instance.inspectorSessionTransformOverrides = sessionTransformOverrides;
#endif
            }
            catch (Exception e)
            {
                Debug.LogError("Error in setting match movable roots: " + e, Instance);
            }
        }

        /// <summary>
        /// A session code wrapper to be stored
        /// </summary>
        [Serializable]
        private class SessionCode
        {
            public string sessionCode = "000-000";
            public SessionCode(string code) => sessionCode = code;
        }

        /// <summary>
        /// Create a new nakama client
        /// </summary>
        /// <returns></returns>
        private async Task CreateClient()
        {
            Debug.Log("Creating client with: " + JsonUtility.ToJson(RuntimeHandler.Settings.NakamaProfile), Instance);

            if (client == null)
                client = new Client(
                RuntimeHandler.Settings.NakamaProfile.Protocol,
                RuntimeHandler.Settings.NakamaProfile.IP,
                RuntimeHandler.Settings.NakamaProfile.Port,
                RuntimeHandler.Settings.NakamaProfile.ServerKey
                );

            await Debug.LogQueue("Created client: " + client, Instance);
        }

        /// <summary>
        /// Create a new nakama session
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task CreateSession(CancellationToken token)
        {
            if (session == null)
            {
                session = await client.AuthenticateDeviceAsync(DeviceInfo.DeviceID, canceller: token);
            }
            else if (session.IsExpired || session.HasExpired(DateTime.UtcNow.AddDays(1)))
            {
                try
                {
                    // Attempt to refresh the existing session.
                    session = await client.SessionRefreshAsync(session, canceller: token);
                }
                catch (ApiResponseException)
                {
                    // Couldn't refresh the session so reauthenticate.
                    session = await client.AuthenticateDeviceAsync(DeviceInfo.DeviceID, canceller: token);
                    PlayerPrefs.SetString("nakama.refreshToken", session.RefreshToken);
                }

                PlayerPrefs.SetString("nakama.authToken", session.AuthToken);
            }

            await Debug.LogQueue("Created session: " + session, Instance);
        }

        /// <summary>
        /// Create a new nakama socket connection
        /// </summary>
        /// <returns></returns>
        private async Task CreateSocket()
        {
            if (socket == null)
            {
                socket = client.NewSocket();

                await socket.ConnectAsync(session, true);
            }

            ReleaseSocketEvents();

            await Debug.LogQueue("Created socket: " + socket, Instance);
        }

        /// <summary>
        /// Receive the current account from nakama and check for session code changes
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ReceiveAccount(CancellationToken token)
        {
            account = await client.GetAccountAsync(session, canceller: token);
            DeviceInfo.Role = FetchUserRole(account.User);

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
                    newSessionCodeDetected = true;
                }
            });
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
                lastSessionStored = "";
                PlayerPrefs.SetString("sessionCode", sessionCode);
                Debug.Log("Update session code to: " + sessionCode, Instance);
            }
        }

        /// <summary>
        /// Get the current session code from the player prefs
        /// </summary>
        /// <returns></returns>
        public string GetSessionCode() => PlayerPrefs.GetString("sessionCode", "000-000");

        /// <summary>
        /// Get the current session code from the current match
        /// </summary>
        /// <returns></returns>
        public string GetUserMatchID() => match != null ? match.Id : "";

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
            username = newUsername;
            await client.UpdateAccountAsync(session, newUsername, username.ToUpper(), canceller: token);
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
            if (session != null && session.UserId == user.Id && editorRole != XRPlayer.Role.Inherit)
            {
                DeviceInfo.Role = editorRole;
                inspectorRole = DeviceInfo.Role;
                return editorRole;
            }
#endif

            inspectorRole = role;

            return role;
        }

        /// <summary>
        /// Read Nakama storage object to avoid SSL issues for now
        /// </summary>
        public async Task ReadStorageObject(CancellationToken token)
        {
            try
            {
                Debug.Log("1");
                var readObjectId = new StorageObjectId
                {
                    Collection = "Presentations",
                    Key = "Presentations",
                    UserId = ""
                };
                Debug.Log("2");

                var result = await client.ReadStorageObjectsAsync(session, new[] { readObjectId }, canceller: token);
                Debug.Log("3");
                if (result.Objects.Any())
                {
                    Debug.Log("4");
                    var storageObject = result.Objects.First();
                    Debug.Log("5");
                    XRDataManager.Instance.HandleJsonResponse(storageObject.Value);
                    Debug.Log("6");
                    //Debug.Log("Read presentation from nakama storage: " + storageObject.Version, this);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Error in reading storage ", Instance);

                XRDataManager.Instance.LoadResourceTextfile();
            }
            Debug.Log("7");
        }

        /// <summary>
        /// Get all currently active matches
        /// </summary>
        public async Task GetActiveMatches(CancellationToken token)
        {
            if (session == null && token.IsCancellationRequested)
                return;

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

            onMatchListUpdate?.Invoke(FilteredMatchList);
        }

        public void Stop()
        {
            ReleaseSocketEvents();

        }

        /// <summary>
        /// Query through active players list and update their positions visually based on the server data
        /// </summary>
        private void Update()
        {
            if (match == null || localPlayer == null || localPlayer.gameObject == null)
                return;
            else
                MovePlayer();

            players.ForEach(p => p.UpdatePlayerTransform());
        }

        /// <summary>
        /// Release all socket events to not call on null delegates
        /// </summary>
        private void ReleaseSocketEvents()
        {
            RuntimeHandler.tick -= SendUpdatedPlayer;

            if (socket == null)
                return;

            socket.Closed -= Reconnect;
            socket.ReceivedError -= SocketErrorMessage;
            socket.ReceivedNotification -= SocketNotification;
            socket.ReceivedMatchState -= SocketMatchState;
            socket.ReceivedMatchPresence -= SocketPresenceUpdate;
        }

        /// <summary>
        /// Setup socket debug events for handling closing, erroring and notifications
        /// </summary>
        private void SetupSocketDebugEvents()
        {
            if (socket == null)
                return;

            socket.Closed += Reconnect;
            socket.ReceivedError += SocketErrorMessage;
            socket.ReceivedNotification += SocketNotification;
        }

        /// <summary>
        /// Handle match joining and leaving event to update players list
        /// </summary>
        private void SetupSocketMatchEvents()
        {
            RuntimeHandler.tick += SendUpdatedPlayer;

            socket.ReceivedMatchState += SocketMatchState;
            socket.ReceivedMatchPresence += SocketPresenceUpdate;
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
                        await ReadStorageObject(networkTokenSource.Token);
                        break;
                    case XRNetworkObjects.MsgType.ROLE_UPDATE:
                        Debug.Log($"Update user role. Using notification code: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);

                        if (HyperSlidesStateManager.appState == HyperSlidesStateManager.AppState.ACTIVE_SESSION)
                            lastSessionStored = GetUserMatchID();
                        else
                            lastSessionStored = "";

                        HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.CONNECTION_SETUP);
#if UNITY_VISIONOS
                        if (DeviceInfo.Role == XRPlayer.Role.Simulation && RuntimeHandler.Settings.trackingType != Settings.TrackingType.Free)
                            XRAdminManager.ChooseTrackingType(Settings.TrackingType.Free);
#endif
                        break;
                    case XRNetworkObjects.MsgType.RESET_ANCHORS:
                        Debug.Log($"Reset anchor setup for user. Using notification code: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        XRAnchorManager.Instance.StartOverAnchorSetup();
                        break;
                    case XRNetworkObjects.MsgType.SPAWN_PANEL:
                        Debug.Log($"Spawn panel for user. Using notification code: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        UI.XRUIManager.Instance.ShowParticipantUI();
                        break;
                    case XRNetworkObjects.MsgType.SESSION_TRANSFORM_OVERRIDE:
                        Debug.Log($"Update session transforms from network: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        SetMatchMovableRoots(notification.Content);
                        break;
                    case XRNetworkObjects.MsgType.SETTING_OVERRIDE:
                        Debug.Log($"Update session settings from network: {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}", Instance);
                        onSettingOverride?.Invoke(notification);
                        break;
                    default:
                        Debug.Log($"Received generic notification: Code:  {notification.Code} => {(XRNetworkObjects.MsgType)notification.Code}, Id: {notification.Id}, Content: {notification.Content}", Instance);
                        onCustomNotification?.Invoke(notification);
                        break;
                }
            });
        }

        /// <summary>
        /// Receive any error message coming from the socket
        /// </summary>
        /// <param name="error"></param>
        private void SocketErrorMessage(Exception error) => Dispatcher.Enqueue(() => Debug.Log("ERROR Received: " + error, this));

        /// <summary>
        /// Receive the current matchstate and handle using the provided <see cref="XRNetworkObjects.MsgType"/>
        /// </summary>
        /// <param name="matchState">The state as byte array</param>
        private void SocketMatchState(IMatchState matchState)
        {
            ticksSinceStartup++;

            //Dispatcher.Enqueue(() =>
            //{
            //    if (string.IsNullOrEmpty(currentTick))
            //        currentTick = "0";

            //    string newTick = JObject.Parse(Encoding.UTF8.GetString(matchState.State))["currentTick"].Value<string>();

            //    if (int.Parse(newTick) <= int.Parse(currentTick))
            //        Debug.Log($"LAGGING BEHIND {newTick} <= {currentTick}");
            //});

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

                            onNetworkSlideUpdate?.Invoke(XRNetworkObjects.networkSessionState);
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
                            currentSlideIndex = JObject.Parse(Encoding.UTF8.GetString(matchState.State))["presentationContentIndex"].Value<int>();
                            currentTick = JObject.Parse(Encoding.UTF8.GetString(matchState.State))["currentTick"].Value<string>();

                            //Animate to correct index if falling behind
                            if (lastTimestamp == null)
                                lastTimestamp = DateTime.UtcNow;

                            if (
                                lastTimestamp.AddSeconds(3) < DateTime.UtcNow &&
                                XRSlideManager.CurrentPresentation &&
                                Mathf.Abs(XRSlideManager.NetworkSlide - currentSlideIndex) > 0)
                            {
                                Debug.Log("Slide index falling behind locally: " + XRSlideManager.NetworkSlide + " => " + currentSlideIndex);
                                XRSlideManager.Instance.SetSlide(currentSlideIndex);
                            }

                            //Debug.Log("UNIFIED_STATE_UPDATE: " + Encoding.UTF8.GetString(matchState.State), this);
                            //Update synced transform values
                            JArray syncedTransforms = (JArray)JObject.Parse(Encoding.UTF8.GetString(matchState.State))["syncedTransforms"];
                            if (syncedTransforms != null)
                            {
                                foreach (JObject syncedTrans in syncedTransforms)
                                {
                                    XRNetworkObjects.NetworkSyncedTransform newSyncedTransform = syncedTrans.ToObject<XRNetworkObjects.NetworkSyncedTransform>();
                                    XRNetworkObjects.NetworkSyncedTransform foundTransform = networkSyncedTransforms.Find(t => t.guid == syncedTrans.GetValue("guid").ToString());

                                    if (foundTransform == null)
                                        networkSyncedTransforms.Add(newSyncedTransform);
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
                                    XRNetworkObjects.NetworkSyncedValue foundValue = networkSyncedValues.Find(t => t.guid == syncedValue.GetValue("guid").ToString());

                                    if (foundValue == null)
                                        networkSyncedValues.Add(newSyncedValue);
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
                                        XRNetworkObjects.XRModerator newModerator = transformsObject[node.Name].ToObject<XRNetworkObjects.XRModerator>();
                                        onNetworkModeratorUpdate?.Invoke(newModerator);
                                    }
                                    else
                                    {
                                        XRNetworkObjects.XRPlayer newTransform = transformsObject[node.Name].ToObject<XRNetworkObjects.XRPlayer>();
                                        onNetworkPlayerUpdate?.Invoke(newTransform);
                                    }
                                }
                            }

                            UpdateNetworkSyncedObjects();
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
                            HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.MATCHMAKING);
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
        /// Invoke an update to all networksynced transforms and values
        /// </summary>
        public static void UpdateNetworkSyncedObjects()
        {
            networkSyncedTransforms.ForEach(t => onNetworkTransformUpdate?.Invoke(t));
            networkSyncedValues.ForEach(v => onNetworkdValueUpdate?.Invoke(v));

#if UNITY_EDITOR
            Instance.inspectorSyncedTransforms = networkSyncedTransforms;
            Instance.inspectorSyncedValues = networkSyncedValues;
#endif
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
                    XRPlayer foundPlayer = players.Find(p => p.UserId == presence.UserId);

                    if (foundPlayer != null)
                        RemovePlayer(foundPlayer);
                }
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
            if (players.Find(p => p.UserId == presence.UserId))
                return;

            Dispatcher.Enqueue(async () =>
            {
                while (match == null)
                    await Task.Delay(1);

                bool isLocalUser = session.UserId == presence.UserId;

                XRPlayer newPlayer = null;

                if (isLocalUser)
                {
                    newPlayer = Instantiate(
                        DeviceInfo.Role == XRPlayer.Role.Simulation ||
                        DeviceInfo.Role == XRPlayer.Role.Broadcast ?
                            spectatorPrefab :
                            localPlayerPrefab).GetComponent<XRPlayer>();

                    localPlayer = newPlayer;
                    localPlayer.UpdatePlayer(presence, isLocalUser);
                    localPlayer.UpdateRole(DeviceInfo.Role);

                    if (DeviceInfo.Role is XRPlayer.Role.Simulation && XRSlideManager.CurrentPresentation != null)
                        XRCameraManager.Instance.UpdateXROriginPosition(3);
                }
                else
                {
                    var result = await client.GetUsersAsync(session, new string[] { presence.UserId });

                    XRNetworkObjects.Metadata userMetaData = JsonConvert.DeserializeObject<XRNetworkObjects.Metadata>(result.Users.First().Metadata);
                    XRPlayer.Role newPlayerRole = result.Users != null && result.Users.Count() > 0 ?
                        FetchUserRole(result.Users.First()) :
                        XRPlayer.Role.Participant;

                    newPlayer = Instantiate(
                        newPlayerRole == XRPlayer.Role.Simulation ||
                        newPlayerRole == XRPlayer.Role.Broadcast ?
                            spectatorPrefab :
                            playerPrefab).GetComponent<XRPlayer>();

                    newPlayer.UpdatePlayer(presence, isLocalUser);
                    newPlayer.UpdateRole(newPlayerRole);
                }

                newPlayer.transform.SetParent(XRContentRoot.Instance.transform, false);

                players.Add(newPlayer);
            });
        }

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
                players.Remove(player);
            });
        }

        /// <summary>
        /// Move the local player and send its update to the session clients
        /// </summary>
        public void MovePlayer()
        {
            localPlayer.localPosition = XRContentRoot.Instance.transform.InverseTransformPoint(XRInputManager.Instance.localPoseDriver.transform.position);
            localPlayer.localRotation = Quaternion.Inverse(XRContentRoot.Instance.transform.rotation) * XRInputManager.Instance.localPoseDriver.transform.rotation;

            localPlayer.transform.localPosition = localPlayer.localPosition;
            localPlayer.transform.localRotation = localPlayer.localRotation;
        }

        /// <summary>
        /// Send this within the tick rate of <see cref="RuntimeHandler.tick"/>
        /// </summary>
        public void SendUpdatedPlayer()
        {
            if (socket == null || match == null || localPlayer == null || localPlayer.gameObject == null)
                return;

            //Do not send any player updates to the server as spectator
            if (DeviceInfo.Role == XRPlayer.Role.Simulation)
                return;

            //Use the simplified version for the player to send less data over network
            XRNetworkObjects.networkPlayer.UserId = localPlayer.UserId;
            XRNetworkObjects.networkPlayer.timeStamp = DateTime.Now;
            XRNetworkObjects.networkPlayer.role = DeviceInfo.Role.ToString();
            XRNetworkObjects.networkPlayer.position = localPlayer.localPosition;
            XRNetworkObjects.networkPlayer.rotation = localPlayer.localRotation.eulerAngles;

            XRNetworkObjects.networkPlayer.syncedValues = new(localPlayer.syncedValues);
            XRNetworkObjects.networkPlayer.syncedTransforms = new(localPlayer.syncedTransforms);

            try
            {
                if (DeviceInfo.Role == XRPlayer.Role.Moderator)
                {
                    XRNetworkObjects.networkModerator.CopyValues(XRNetworkObjects.networkPlayer);
                    XRNetworkObjects.networkModerator.leftPointer = new XRNetworkObjects.XRPointer(localPlayer.UserId, Handedness.Left, localPlayer.xrPointerLeft.gameObject);
                    XRNetworkObjects.networkModerator.rightPointer = new XRNetworkObjects.XRPointer(localPlayer.UserId, Handedness.Right, localPlayer.xrPointerRight.gameObject);

                    socket.SendMatchStateAsync(match.Id, (long)XRNetworkObjects.MsgType.TRANSFORM_UPDATE, JsonConvert.SerializeObject(XRNetworkObjects.networkModerator, Formatting.Indented, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }));
                }
                else
                {
                    socket.SendMatchStateAsync(match.Id, (long)XRNetworkObjects.MsgType.TRANSFORM_UPDATE, JsonConvert.SerializeObject(XRNetworkObjects.networkPlayer, Formatting.Indented, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error on sending match state async: " + e.Message);
            }

            localPlayer.syncedValues.Clear();
            localPlayer.syncedTransforms.Clear();
        }

        /// <summary>
        /// Send the current slide update
        /// </summary>
        /// <param name="presentationID"></param>
        /// <param name="contentIndex"></param>
        public void SendSlideUpdate(XRPresentation presentation, int contentIndex)
        {
            if (presentation.slides.Count > 0)
                contentIndex = contentIndex.Clamp(1, presentation.contents.Count);

            XRNetworkObjects.networkSessionState.presentationId = presentation.id;
            XRNetworkObjects.networkSessionState.presentationName = presentation.title;
            XRNetworkObjects.networkSessionState.presentationContentIndex = contentIndex;

            Debug.Log("Slide update sending by moderator: " + JsonUtility.ToJson(XRNetworkObjects.networkSessionState), this);

            socket.SendMatchStateAsync(match.Id, (long)XRNetworkObjects.MsgType.PRESENTATION_UPDATE, JsonConvert.SerializeObject(XRNetworkObjects.networkSessionState, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            }));
        }

        /// <summary>
        /// Wrapper to send next slide asynchronously
        /// </summary>
        public void SendSlideNextUpdate()
        {
            if (IsConnected() && match != null)
                SendSlideNextUpdateAsync();
        }

        /// <summary>
        /// Wrapper to send previous slide asynchronously
        /// </summary>
        public void SendSlidePrevUpdate()
        {
            if (IsConnected() && match != null)
                SendSlideNPrevUpdateAsync();
        }

        /// <summary>
        /// Send the next slide update to the server
        /// </summary>
        private async void SendSlideNextUpdateAsync()
        {
            try
            {
                await socket.SendMatchStateAsync(match.Id, (long)XRNetworkObjects.MsgType.NEXT_SLIDE, "{}");
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
                await socket.SendMatchStateAsync(match.Id, (long)XRNetworkObjects.MsgType.PREV_SLIDE, "{}");
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
            if (client == null || session == null || account == null)
                return;

            XRNetworkObjects.XRPlayerMetadata playerMetadata = new();
            playerMetadata.userId = session.UserId;
            playerMetadata.metadata = Nakama.TinyJson.JsonParser.FromJson<XRNetworkObjects.Metadata>(account.User.Metadata);

            //Update all values
            playerMetadata.metadata.ip = DeviceInfo.Instance.GetLocalIPAddress();
            playerMetadata.metadata.battery = (int)(SystemInfo.batteryLevel * 100);
            playerMetadata.metadata.sessionCode = GetSessionCode();
            playerMetadata.metadata.location = DeviceInfo.Location;
            playerMetadata.metadata.deviceType = DeviceInfo.DeviceType;

            if (roleUpdate != XRPlayer.Role.Inherit)
                playerMetadata.metadata.role = $"{(int)roleUpdate}";
            else
                playerMetadata.metadata.role = $"{(int)DeviceInfo.Role}";

            string metaString = JsonUtility.ToJson(playerMetadata);

            await client.RpcAsync(session, "UPDATE_USER", metaString);
        }

        /// <summary>
        /// Join a match
        /// </summary>
        /// <param name="matchID">The match id</param>
        /// <returns></returns>
        public async Task JoinMatchAsync(string matchID)
        {
            //await _connectionSemaphore.WaitAsync();
            try
            {
                SetupSocketMatchEvents();

                match = await socket.JoinMatchAsync(matchID);

                if (match == null)
                {
                    stopAutoJoin = true;

                    await Debug.LogQueue("Could not connect to match!", Instance);
                    return;
                }

                SetMatchMovableRoots(match.Label);

                Dispatcher.Enqueue(() =>
                {
                    lastSessionStored = GetUserMatchID();

                    foreach (var presence in match.Presences)
                        CreatePlayer(presence, match.Presences.ToList().IndexOf(presence));

                    onMatchJoined?.Invoke();
                    SendPlayerMetaData();
                });
            }
            catch (Exception e)
            {
                lastSessionStored = GetUserMatchID();

                foreach (var presence in match.Presences)
                    CreatePlayer(presence, match.Presences.ToList().IndexOf(presence));

                stopAutoJoin = true;

                await HyperSlidesStateManager.UpdateStateWithDelay("Could not join match\n" + e.StackTrace, 1f);

                HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.MATCHMAKING);
            }
            finally
            {
                Debug.Log("Finished joining match!");
                //_connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Leave the current match
        /// </summary>
        public void LeaveMatch() => HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.MATCHMAKING);

        /// <summary>
        /// Leave match triggered by application quit to have updated values on runtime builds
        /// </summary>
        /// <returns></returns>
        public async Task LeaveMatchAsync()
        {
            //await _connectionSemaphore.WaitAsync();
            try
            {
                RuntimeHandler.Settings.updateRate = RuntimeHandler.Settings.backupUpdateRate;

                ReleaseSocketEvents();
                SetupSocketDebugEvents();

                if (match == null || !IsConnected())
                    return;

                //Dispatcher.Enqueue(() => Debug.Log("Leaving match... ", this));
                //Resubscribe to default events

                networkSyncedTransforms.Clear();
                networkSyncedValues.Clear();

                //Leave the match
                await socket.LeaveMatchAsync(match);
                match = null;

                //Destroy all current players and reset slide contents
                players.ForEach(p => RemovePlayer(p));

                onMatchLeft?.Invoke();
            }
            finally
            {
                Debug.Log("Finished leaving match.");
                //_connectionSemaphore.Release();
            }

        }

        /// <summary>
        /// Handle application quit, leave the match and close the socket
        /// </summary>
        private async void OnApplicationQuit()
        {
            await LeaveMatchAsync();

            if (socket != null && socket.IsConnected)
                await socket.CloseAsync();
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
    }
}