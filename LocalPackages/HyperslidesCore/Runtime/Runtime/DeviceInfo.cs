using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NSYNK.HyperSlides.Network;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.Runtime
{
    public class DeviceInfo : Singleton<DeviceInfo>
    {
        private RuntimePlatform runtimePlatform => Application.platform;

        public string StandalonePresentationId = "";
        public GameObject videoProductionPrefab;
        public GameObject standalonePlayerPrefab;

        public XRPlayer.Role Role { get; set; } = XRPlayer.Role.Participant;
        public string DeviceId { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public bool UseStandaloneSetup = false;
        public Vector3 Location { get; set; } = Vector3.zero;

        [TextArea, ReadOnly]
        public string deviceSummary;

        //Load the current server connection from settings on runtime or from custom override in editor
#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        [DllImport("__Internal")]
        public static extern bool GetBoolSetting(string key);

        [DllImport("__Internal")]
        public static extern string GetStringSetting(string key);

        [DllImport("__Internal")]
        private static extern void SetKeyChainValueFromNative(string key, string value, string group);

        [DllImport("__Internal")]
        private static extern IntPtr GetKeyChainValueFromNative(string key, string group);

        public void SetKeyChainValue(string key, string value, string accessGroup)
        {
            SetKeyChainValueFromNative(key, value, accessGroup);
        }

        public string GetKeyChainValue(string key, string accessGroup)
        {
            IntPtr ptr = GetKeyChainValueFromNative(key, accessGroup);
            if (ptr == IntPtr.Zero)
                return "";

            string result = Marshal.PtrToStringAnsi(ptr);
            return result;
        }
#else
        public void SetKeyChainValue(string key, string value, string accessGroup) { }
        public string GetKeyChainValue(string key, string accessGroup) => "";

        /// <summary>
        /// Editor and standalone version of getting the correct boolean values from file or settings
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool GetBoolSetting(string key)
        {
            switch (key)
            {
                case "use_standalone_setup":
                    return Instance.UseStandaloneSetup;
                case "sentry_logging":
                    return HyperSlidesStateManager.Instance.CurrentServerConnection.SentryLogging;
                case "server_override_nakama_ssl":
                    return HyperSlidesStateManager.Instance.CurrentServerConnection.SSL;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Editor and standalone version of getting the correct string values from file or settings
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetStringSetting(string key)
        {
            switch (key)
            {
                case "standalone_presentation_id":
                    return Instance.StandalonePresentationId;
                case "server_override_mode":
                    return HyperSlidesStateManager.Instance.CurrentServerConnection.ConnectionName;
                case "server_override_backend_ip":
                    return HyperSlidesStateManager.Instance.CurrentServerConnection.BackendIP;
                case "server_override_nakama_ip":
                    return HyperSlidesStateManager.Instance.CurrentServerConnection.NakamaIP;
                case "server_override_nakama_port":
                    return HyperSlidesStateManager.Instance.CurrentServerConnection.Port.ToString();
                default:
                    return "";
            }
        }
#endif

        /// <summary>
        /// Get default device info on awake
        /// </summary>
        protected override void OnSingletonAwake()
        {
            if (string.IsNullOrEmpty(GetKeyChainValue("DeviceId", "com.NSYNK.sharedvalues")))
            {
                DeviceId = PlayerPrefs.GetString("DeviceId", SaveDeviceId());
                SetKeyChainValue("DeviceId", DeviceId, "com.NSYNK.sharedvalues");
            }
            else
                DeviceId = GetKeyChainValue("DeviceId", "com.NSYNK.sharedvalues");

            DeviceType = SystemInfo.deviceModel;

            Debug.Log($"Device OS / Platform: {runtimePlatform}, Device Type: {DeviceType}", Instance);
        }

        /// <summary>
        /// Load the server connection from settings or custom override
        /// </summary>
        public void Start()
        {
            CheckServerOverrideSettings();
        }

        /// <summary>
        /// Check if there are override settings for the server connection and apply them
        /// </summary>
        public void CheckServerOverrideSettings()
        {
            Debug.Log("Checking for server IP override", Instance);

#if !UNITY_EDITOR && !UNITY_VISIONOS && !UNITY_IOS
            GetServerConnectionFromDataPath();
#endif

            string serverMode = GetStringSetting("server_override_mode");

            UseStandaloneSetup = GetBoolSetting("use_standalone_setup");
            StandalonePresentationId = GetStringSetting("standalone_presentation_id");

            bool serverOverride = string.Equals(serverMode, "custom", StringComparison.OrdinalIgnoreCase);

            if (serverOverride)
            {
                ServerConnection customConnection = new ServerConnection
                {
                    ConnectionName = "custom",
                    BackendIP = GetStringSetting("server_override_backend_ip"),
                    NakamaIP = GetStringSetting("server_override_nakama_ip"),
                    Port = int.TryParse(GetStringSetting("server_override_nakama_port"), out int nakamaPort) ? nakamaPort : 0,
                    SSL = GetBoolSetting("server_override_nakama_ssl"),
                    SentryLogging = GetBoolSetting("sentry_logging")
                };

                HyperSlidesStateManager.Instance.AddNewConnectionAndSet(customConnection);
            }
            else
            {
                HyperSlidesStateManager.Instance.SetServerConnection(serverMode);
            }

            LoadCorrectPrefab();
        }

        private void LoadCorrectPrefab()
        {
#if UNITY_IOS
            if (UseStandaloneSetup)
                Instantiate(standalonePlayerPrefab);
            else
                Instantiate(videoProductionPrefab);
#endif
        }
        /// <summary>
        /// Load a server connection from a json file in the persistent data path
        /// </summary>
        /// <returns></returns>
        private void GetServerConnectionFromDataPath()
        {
            string path = Application.persistentDataPath + "/" + "LocalServerConnection.json";

            try
            {
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    Debug.Log("Loaded ServerConnection from: " + path + " " + json, this);
                    ServerConnection serverConnection = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerConnection>(json);
                    HyperSlidesStateManager.Instance.AddNewConnectionAndSet(serverConnection);
                }
                else
                {
                    HyperSlidesStateManager.Instance.SetServerConnection();
                    Debug.LogWarning("No ServerConnection found at: " + path + " Using default from inspector.", this);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error loading ServerConnection from: " + path + " Using default from inspector. " + e.Message, this);
            }
        }

        /// <summary>
        /// Get the local IP address of the device
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new System.Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// Is this device an actual XR headset
        /// </summary>
        /// <returns>True, if the device can handle foveated rendering</returns>
        public bool IsXRDevice()
        {
            //Defaulting to visionos for now until we add more devices
            return runtimePlatform == RuntimePlatform.VisionOS;
        }

        /// <summary>
        /// Get the DeviceId from player prefs
        /// </summary>
        /// <returns></returns>
        public string GetDeviceId() => DeviceId;

        /// <summary>
        /// Save the DeviceId to player prefs
        /// </summary>
        public string SaveDeviceId()
        {
            DeviceId = PlayerPrefs.GetString("DeviceId", SystemInfo.deviceUniqueIdentifier);

            if (DeviceId == SystemInfo.unsupportedIdentifier)
                DeviceId = Guid.NewGuid().ToString();

            PlayerPrefs.SetString("DeviceId", DeviceId);

            return DeviceId;
        }

        /// <summary>
        /// Summarized device info string
        /// </summary>
        /// <returns>A string with all device info available</returns>
        public string DeviceInfoString()
        {
            string summary = "Platform: " + runtimePlatform;

            summary += "\nDeviceId: " + DeviceId;
            summary += "\nDeviceType: " + DeviceType;
            summary += "\nXR Device: " + IsXRDevice();

            return summary;
        }

        private void LateUpdate()
        {
            deviceSummary = DeviceInfoString();
        }
    }
}