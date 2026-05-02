using System;
using Nakama;
using Newtonsoft.Json.Linq;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A custom runtime update, that updates only on the given amount (hz) in <see cref="Core.Settings.updateRate"/>
    /// </summary>
    public class RuntimeHandler : Singleton<RuntimeHandler>
    {
        public static bool editModeEnabled = false;
        public static Settings Settings
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
        public delegate void Tick();
        public static Tick tick;
        public static Tick slowTick;
        public float tickTime, slowTickTime = 0;

        [SerializeField]
        private Settings _settings;
        private Settings storedSettings;

        private void LateUpdate()
        {
            tickTime += Time.deltaTime;
            slowTickTime += Time.deltaTime;

            if (tickTime * 1000 > 1000 / Settings.updateRate)
            {
                //Debug.Log("Tick");
                tick?.Invoke();
                tickTime = 0;
            }

            if (slowTickTime * 1000 > 1000 / Settings.slowUpdateRate)
            {
                //Debug.Log("SlowTick");
                slowTick?.Invoke();
                slowTickTime = 0;
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            Instance.storedSettings = Instantiate(Settings);
            Settings = Instance.storedSettings;
#endif

            XRNetworkManager.onSettingOverride += HandleCustomNotification;
        }

        void OnDisable()
        {
            XRNetworkManager.onSettingOverride -= HandleCustomNotification;
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
            catch (System.Exception e)
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