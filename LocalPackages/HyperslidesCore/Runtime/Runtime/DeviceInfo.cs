using System;
using System.Collections;
using System.Collections.Generic;
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
        public static RuntimePlatform RuntimePlatform => Application.platform;

        public static Vector3 Location = Vector3.zero;
        public static XRPlayer.Role Role = XRPlayer.Role.Participant;
        public static string DeviceID = "";
        public static string DeviceType = "";
        public static bool ServerOverride = false;
        public static string ServerOverrideBackendIP = "";
        public static string ServerOverrideNakamaIP = "";
        public static int ServerOverrideNakamaPort = 0;
        public static bool ServerOverrideNakamaSSL = false;

        [TextArea]
        public string deviceSummary;

        public string defaultSceneName;
        public List<PlatformScenePair> platformScenePairs;

#if (UNITY_IOS || UNITY_VISIONOS) && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool GetBoolSetting(string key);

        [DllImport("__Internal")]
        private static extern string GetStringSetting(string key);
#else
        [Header("Editor only override")]
        public bool EditorOverride = false;
        public string EditorOverrideBackendIP = "";
        public string EditorOverrideNakamaIP = "";
        public int EditorOverrideNakamaPort = 0;
        public bool EditorOverrideNakamaSSL = false;

        private bool GetBoolSetting(string key) => key == "server_override" ? EditorOverride : EditorOverrideNakamaSSL;
        private string GetStringSetting(string key) {
            switch (key)
            {
                case "server_override_backend_ip":
                    return EditorOverrideBackendIP;
                case "server_override_nakama_ip":
                    return EditorOverrideNakamaIP;
                case "server_override_nakama_port":
                    return EditorOverrideNakamaPort.ToString();
                default:
                    return "";
            }   
        }
#endif


        private void Start()
        {
            //PlayerPrefs.DeleteAll();
            DeviceID = PlayerPrefs.GetString("deviceId", SaveDeviceID());
            DeviceType = SystemInfo.deviceModel;

            CheckServerOverrideSettings();
        }

        public void CheckServerOverrideSettings()
        {
            Debug.Log("Checking for server IP override", Instance);

            ServerOverride = GetBoolSetting("server_override");
            ServerOverrideBackendIP = GetStringSetting("server_override_backend_ip");
            ServerOverrideNakamaIP = GetStringSetting("server_override_nakama_ip");
            ServerOverrideNakamaPort = int.Parse(GetStringSetting("server_override_nakama_port"));
            ServerOverrideNakamaSSL = GetBoolSetting("server_override_nakama_ssl");
        }

        public static async Awaitable GetUserLocation()
        {
            await Debug.LogQueue($"Getting user location", Instance);

#if UNITY_VISIONOS
            //await Instance.VisionOSLocation();
#elif UNITY_IOS
            await Instance.IOSLocation();
#else
            Dispatcher.Enqueue(() => Debug.Log($"Location services not supported, skipping", Instance));
            await Awaitable.MainThreadAsync();
#endif
        }

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

        private async Awaitable VisionOSLocation()
        {
            if (!UnityEngine.Input.location.isEnabledByUser)
            {
                await Debug.LogQueue("Location not enabled on device or app does not have permission to access location");
                return;
            }

            // Waits until the location service initializes
            int maxWait = 20;
            while (UnityEngine.Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                await Task.Delay(1000);
                maxWait--;
            }

            // If the service didn't initialize in 20 seconds this cancels location service use.
            if (maxWait < 1)
            {
                await Debug.LogQueue("Timed out");
                return;
            }

            // If the connection failed this cancels location service use.
            if (UnityEngine.Input.location.status == LocationServiceStatus.Failed)
            {
                await Debug.LogQueue("Unable to determine device location");
                return;
            }
            else
            {
                // If the connection succeeded, this retrieves the device's current location and displays it in the Console window.
                Location.x = UnityEngine.Input.location.lastData.longitude;
                Location.y = UnityEngine.Input.location.lastData.altitude;
                Location.z = UnityEngine.Input.location.lastData.latitude;

                await Debug.LogQueue($"Last Data: " + Location, Instance);
            }

            await Debug.LogQueue($"Status: " + UnityEngine.Input.location.status, Instance);
        }

        private async Awaitable IOSLocation()
        {
            await Awaitable.MainThreadAsync();
            //try
            //{
            //    locationService.Start();

            //    int attempts = 5;

            //    while (attempts > 0 || locationService.status != LocationServiceStatus.Running)
            //    {
            //        await Task.Delay(1000);
            //        attempts--;
            //    }

            //    if (locationService.status == LocationServiceStatus.Failed)
            //        return;
            //    else
            //    {
            //        Location.x = locationService.lastData.longitude;
            //        Location.y = locationService.lastData.altitude;
            //        Location.z = locationService.lastData.latitude;
            //    }
            //    //Stop retrieving location
            //    locationService.Stop();
            //}
            //catch (Exception e)
            //{
            //    HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.ERROR, "Location service failed: " + e);
            //}
        }

        private async Awaitable DefaultLocationService()
        {
            await Awaitable.MainThreadAsync();
        }

        /// <summary>
        /// Load platform dependent scenes or default
        /// </summary>
        public static async Awaitable LoadPlatformAssets()
        {
            PlatformScenePair foundScenePair = Instance.platformScenePairs.Find(psp => psp.platform == Application.platform);

            AsyncOperation asyncSceneLoad = null;

            if (foundScenePair != null)
                if (SceneManager.GetActiveScene().name != foundScenePair.sceneName)
                    asyncSceneLoad = SceneManager.LoadSceneAsync(foundScenePair.sceneName);
                else if (SceneManager.GetSceneByName(Instance.defaultSceneName).IsValid())
                    if (SceneManager.GetActiveScene().name != Instance.defaultSceneName)
                        asyncSceneLoad = SceneManager.LoadSceneAsync(Instance.defaultSceneName);
                    else
                    {
                        await Debug.LogQueue(
                            $"Cannot load scenes. Scene pairs available ({Instance.platformScenePairs.Count}). " +
                            (string.IsNullOrEmpty(Instance.defaultSceneName) ? "DefaultSceneName is empty" : $"Scene <b>{Instance.defaultSceneName}</b> not added to build list"), Instance);

                        return;
                    }

            while (asyncSceneLoad != null && !asyncSceneLoad.isDone)
                await Task.Delay(1);

            await GetUserLocation();
            await Debug.LogQueue($"Device OS / Platform: {RuntimePlatform}, ", Instance);
        }

        /// <summary>
        /// Is this device an actual XR headset
        /// </summary>
        /// <returns>True, if the device can handle foveated rendering</returns>
        public static bool IsXRDevice()
        {
            //Defaulting to visionos for now until we add more devices
            return RuntimePlatform == RuntimePlatform.VisionOS;
        }

        /// <summary>
        /// Save the deviceid to player prefs
        /// </summary>
        public static string SaveDeviceID()
        {
            var deviceId = PlayerPrefs.GetString("deviceId", SystemInfo.deviceUniqueIdentifier);

            if (deviceId == SystemInfo.unsupportedIdentifier)
                deviceId = Guid.NewGuid().ToString();

            PlayerPrefs.SetString("deviceId", deviceId);

            return deviceId;
        }

        /// <summary>
        /// Summarized device info string
        /// </summary>
        /// <returns>A string with all device info available</returns>
        public string DeviceInfoString()
        {
            string summary = "Platform: " + RuntimePlatform;

            summary += "\nDeviceID: " + DeviceID;
            summary += "\nDeviceType: " + DeviceType;
            summary += "\nXR Device: " + IsXRDevice();
            summary += "\nLocation: " + Location;

            return summary;
        }

        private void LateUpdate()
        {
            deviceSummary = DeviceInfoString();
        }
    }

    [Serializable]
    public class PlatformScenePair
    {
        public string sceneName;
        public RuntimePlatform platform;
    }
}