using Nakama;
using Newtonsoft.Json.Linq;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.XR;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.Runtime
{
    [RequireComponent(typeof(ReflectionProbe))]

    public class HDRIReplace : Singleton<HDRIReplace>
    {
        public ReflectionProbe reflectionProbe;
        public bool autoLoadOnEnable = true;
        public bool restorePreviousSkybox = true;
        public Material DefaultSkyboxMaterial;

        private Material skyboxMaterial;
        private Material storedSkyboxMaterial;
        private string previousLoadedPair = "";

        private SessionTransformOverride sessionTransformOverride;

        void OnEnable()
        {
            previousLoadedPair = PlayerPrefs.GetString("LastLoadedHDRI", previousLoadedPair);

            if (RenderSettings.skybox == null)
            {
                Debug.LogWarning("No skybox material found in RenderSettings. Creating a new one.", this);
                CreateSkyboxOnRenderSettings();
            }

            storedSkyboxMaterial = RenderSettings.skybox;

            skyboxMaterial = new Material(storedSkyboxMaterial.shader);
            skyboxMaterial.CopyPropertiesFromMaterial(storedSkyboxMaterial);

            RenderSettings.skybox = skyboxMaterial;

            StorePreviousSkybox();

            XRAnchorManager.Instance.OnTrackingUpdate += UpdateHDRIRotation;
            XRNetworkManager.Instance.OnCustomNotification += LoadHDRIFromNotification;
            sessionTransformOverride = this.GetComponent<NSYNK.HyperSlides.Runtime.SessionTransformOverride>();

            if (sessionTransformOverride != null)
                sessionTransformOverride.OnSessionTransformOverrideUpdated += UpdateHDRIRotation;
        }

        void OnDisable()
        {
            RenderSettings.skybox = storedSkyboxMaterial;

            if (XRAnchorManager.Instance != null)
                XRAnchorManager.Instance.OnTrackingUpdate -= UpdateHDRIRotation;

            if (XRNetworkManager.Instance != null)
                XRNetworkManager.Instance.OnCustomNotification -= LoadHDRIFromNotification;

            if (sessionTransformOverride != null)
                sessionTransformOverride.OnSessionTransformOverrideUpdated -= UpdateHDRIRotation;
        }

        void OnValidate()
        {
            reflectionProbe = GetComponent<ReflectionProbe>();
        }

        void Start()
        {
            HandleAutoLoadOnEnable();
        }

        private void HandleAutoLoadOnEnable()
        {
            if (autoLoadOnEnable)
            {
                if (RemoteAssetLoader.Instance)
                {
                    if (!string.IsNullOrEmpty(previousLoadedPair) && restorePreviousSkybox)
                    {
                        Debug.Log("Auto-loading previous HDRI from PlayerPrefs: " + previousLoadedPair, this);
                        ReplaceHDRI(JsonUtility.FromJson<RemoteAssetLoader.PresentationAssetPair>(previousLoadedPair));
                    }
                    else if (RemoteAssetLoader.Instance.presentationAssetPairs.Count > 0)
                    {
                        Debug.Log("Auto-loading first HDRI from presentationAssetPairs: " + RemoteAssetLoader.Instance.presentationAssetPairs[0], this);
                        ReplaceHDRI(RemoteAssetLoader.Instance.presentationAssetPairs[0]);
                    }
                    else
                        Debug.LogWarning("No previous HDRI found in PlayerPrefs and presentationAssetPairs is empty.", this);
                }
            }
        }

        /// <summary>
        /// Handles incoming Nakama notifications to update the HDRI skybox.
        /// </summary>
        /// <param name="notification"></param>
        void LoadHDRIFromNotification(IApiNotification notification)
        {
            Debug.Log($"Received notification of type: {notification.Code} with content: {notification.Content}", this);

            if (notification.Code == 251)
            {
                try
                {
                    JObject contentObj = JObject.Parse(notification.Content);
                    string dataString = contentObj["data"]?.Value<string>();
                    JObject dataObj = JObject.Parse(dataString);

                    RemoteAssetLoader.PresentationAssetPair pair = new RemoteAssetLoader.PresentationAssetPair(
                        dataObj["projectName"]?.Value<string>(),
                        dataObj["catalogName"]?.Value<string>(),
                        dataObj["assetKey"]?.Value<string>()
                    );

                    Debug.Log("Received HDRIUpdate notification and created pair: " + JsonUtility.ToJson(pair), this);

                    ReplaceHDRI(pair, true);
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Failed to parse HDRIUpdate notification: " + e.Message, this);
                }
            }
        }

        private void CreateSkyboxOnRenderSettings()
        {
            if (DefaultSkyboxMaterial)
                RenderSettings.skybox = DefaultSkyboxMaterial;
            else
                RenderSettings.skybox = new Material(Shader.Find("Skybox/Panoramic"));
        }

        void StorePreviousSkybox()
        {
            skyboxMaterial = RenderSettings.skybox;
            Texture2D previousTexture = skyboxMaterial.GetTexture("_MainTex") as Texture2D;

            if (previousTexture != null)
            {
                // Debug.Log("Previous skybox texture stored: " + previousTexture.name, this);
            }
            else
            {
                // Debug.LogWarning("No previous skybox texture found.", this);
            }
        }

        void RestorePreviousSkybox()
        {
            RenderSettings.skybox = storedSkyboxMaterial;
            UpdateReflectionsAndGI();
        }

        private async void ReplaceHDRI(RemoteAssetLoader.PresentationAssetPair pair, bool forceUpdate = false)
        {
            // Load the HDRI texture from the remote asset loader
            Task<object> loadTask = RemoteAssetLoader.Instance.LoadPresentation(pair, forceUpdate);

            await loadTask.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && task.Result != null)
                {
                    Texture2D texture = task.Result as Texture2D;
                    ApplyHDRITexture(texture);

                    Dispatcher.Enqueue(() => PlayerPrefs.SetString("LastLoadedHDRI", JsonUtility.ToJson(pair)));
                }
                else
                {
                    Dispatcher.Enqueue(() => Debug.LogError("Failed to load HDRI texture: " + pair.assetKey, this));
                }
            });
        }

        public void ApplyHDRITexture(Texture2D texture)
        {
            Dispatcher.Enqueue(() =>
            {
                CleanupLoadedTexture();
                skyboxMaterial.SetTexture("_MainTex", texture);
                UpdateHDRIRotation(false);
                RenderSettings.skybox = skyboxMaterial;
                UpdateReflectionsAndGI();
                Debug.Log("HDRI texture applied to skybox.", this);
            });
        }

        private void UpdateHDRIRotation() => UpdateHDRIRotation(true);

        private void UpdateHDRIRotation(bool apply = true)
        {
            float storedRotation = storedSkyboxMaterial.GetFloat("_Rotation");
            float newRotation = (-1 * XRContentRoot.Instance.transform.rotation.eulerAngles.y) + this.transform.rotation.eulerAngles.y;
            newRotation = newRotation % 360f;
            skyboxMaterial.SetFloat("_Rotation", newRotation);

            // Debug.Log($"HDRI rotation set to {newRotation}, storedRotation: {storedRotation}.", this);

            if (apply)
            {
                RenderSettings.skybox = skyboxMaterial;
                UpdateReflectionsAndGI();
                // Debug.Log("HDRI texture applied to skybox.", this);
            }
        }

        private void UpdateReflectionsAndGI()
        {
            if (reflectionProbe != null)
            {
                // Update the reflection probe
                reflectionProbe.RenderProbe();
                // Debug.Log("Reflection probe updated.", this);
            }
            else
            {
                Debug.LogWarning("Reflection probe is not assigned.", this);
            }

            // Update global illumination
            DynamicGI.UpdateEnvironment();

            // Debug.Log("Reflections and GI updated.", this);
        }

        private void CleanupLoadedTexture()
        {
            Texture currentTexture = skyboxMaterial.GetTexture("_MainTex");

            if (currentTexture != null)
            {
                skyboxMaterial.SetTexture("_MainTex", null);
                Destroy(currentTexture);
                // Debug.Log("Cleaned up loaded HDRI texture.", this);
            }
        }
    }
}