using Nakama;
using NSYNK.HyperSlides;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using UnityEngine;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System;
using Ping = System.Net.NetworkInformation.Ping;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.Core
{
    public class RuntimeLogging : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI loggingText;
        public TMPro.TextMeshProUGUI matchLoggingText;
        public TMPro.TextMeshProUGUI memoryUsageText;

        private string lastLatencyLog = "";
        private float lastLatencyTestTime = 0f;


#if UNITY_EDITOR
        void Start()
        {
            if (TryGetComponent<Canvas>(out var canvas))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = 2;
            }
            else
                Debug.LogWarning("RuntimeLogging requires a Canvas component to be attached to the GameObject.");

            if (TryGetComponent<CanvasScaler>(out var canvasScaler))
                canvasScaler.scaleFactor = 2;
            else
                Debug.LogWarning("RuntimeLogging requires a CanvasScaler component to be attached to the GameObject.");
        }
#endif

        void Update()
        {
            // Update logging text every 5 seconds
            if (Time.time - lastLatencyTestTime > 10f)
            {
                lastLatencyTestTime = Time.time;
                GetNetworkLatency();
            }

            loggingText.text = LogNetworkManager();
            loggingText.text += LogDeviceNetworkStats();
            loggingText.text += LogLastLatencyTest();

            matchLoggingText.text = LogNakamaMatch();

            memoryUsageText.text = GetMemoryUsage();
        }

        private string LogDeviceNetworkStats()
        {
            string log = "<b>DEVICE NETWORK STATUS:</b>\n";

            log += $"<b>Device ID:</b> {DeviceInfo.Instance.DeviceId}\n" +
                   $"<b>Device Type:</b> {DeviceInfo.Instance.DeviceType}\n" +
                   $"<b>Device Role:</b> {DeviceInfo.Instance.Role}\n";

            // Network connectivity info
            log += $"<b>Internet Reachability:</b> {Application.internetReachability}\n";
            log += $"<b>Network Available:</b> {Application.internetReachability != NetworkReachability.NotReachable}\n";

            log += "\n";
            return log;
        }

        private string LogNakamaMatch()
        {
            if (XRNetworkManager.Instance == null || XRNetworkManager.Instance.Socket == null)
            {
                return "<b>NAKAMA MATCH:</b> Not connected to Nakama.\n";
            }

            if (XRNetworkManager.Instance.Match == null)
            {
                return "<b>NAKAMA MATCH:</b> No match found.\n";
            }

            string log = "<b>NAKAMA MATCH STATUS:</b>\n";

            log += $"<b>Match ID:</b> {XRNetworkManager.Instance.Match.Id}\n\n" +
                         $"<b>Connected users:</b>";

            foreach (XRNetworkObjects.XRPlayer player in XRNetworkManager.Instance.UserPresences)
            {
                log += $"\n<b>{player.Username}</b>\n<size=75%>({(XRPlayer.Role)int.Parse(player.role)})</size>\n";
            }

            log += "\n";
            return log;
        }

        private string LogNetworkManager()
        {
            if (HyperSlidesStateManager.Instance == null || XRNetworkManager.Instance == null)
            {
                return "<b>NETWORK MANAGER STATUS:</b>\n Not initialized.\n\n";
            }
            
            string log = "<b>NETWORK MANAGER STATUS:</b>\n";

            log += $"<b>Current app state:</b> {HyperSlidesStateManager.Instance.CurrentAppState}\n" +
                         $"<b>Current network state:</b> {XRNetworkManager.Instance.CurrentNetworkState}\n" +
                         $"<b>Current session code:</b> {XRNetworkManager.Instance.GetSessionCode()}\n" +
                         $"<b>Last session stored:</b> {XRNetworkManager.Instance.LastSessionStored}\n";

            if (XRNetworkManager.Instance.Socket != null)
                log += $"Socket connected: {XRNetworkManager.Instance.Socket.IsConnected}\n";

            log += "\n";
            return log;
        }

        private string LogLastLatencyTest()
        {
            string log = "<b>NETWORK LATENCY:</b>\n";
            if (string.IsNullOrEmpty(lastLatencyLog))
            {
                log += "No latency test performed yet.\n";
            }
            else
            {
                log += lastLatencyLog;
            }

            log += "\n";
            return log;
        }

        private void GetNetworkLatency() => _ = GetNetworkLatencyInternal();

        private async Task GetNetworkLatencyInternal()
        {
            // Test Nakama latency if connected
            if (XRNetworkManager.Instance.SocketIsConnected())
            {
                long nakamaLatency = await XRNetworkManager.Instance.GetNakamaLatencySimple();
                if (nakamaLatency > 0)
                {
                    lastLatencyLog = $"<b>Nakama Latency:</b> {nakamaLatency}ms\n";
                }
            }
        }

        private string GetMemoryUsage()
        {
            long totalMemory = GC.GetTotalMemory(false);
            long maxSystemMemory = SystemInfo.systemMemorySize * 1024 * 1024; // Convert to bytes
            return $"<b>Memory Usage:</b> {totalMemory / (1024 * 1024)} MB/{maxSystemMemory / (1024 * 1024)} MB\n";
        }
    }
}