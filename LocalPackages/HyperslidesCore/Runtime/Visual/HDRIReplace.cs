using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.XR;
using System.Threading.Tasks;
using UnityEngine;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.Runtime
{
    [RequireComponent(typeof(RemoteAssetLoader))]
    [RequireComponent(typeof(ReflectionProbe))]

    public class HDRIReplace : MonoBehaviour
    {
        public RemoteAssetLoader assetLoader;
        public ReflectionProbe reflectionProbe;

        public bool autoLoadOnEnable = true;

        public Material DefaultSkyboxMaterial;
        private Material skyboxMaterial;
        private Material storedSkyboxMaterial;

        void OnEnable()
        {
            if(RenderSettings.skybox == null)
            {
                Debug.LogWarning("No skybox material found in RenderSettings. Creating a new one.", this);
                CreateSkyboxOnRenderSettings();
            }

            storedSkyboxMaterial = RenderSettings.skybox;            

            skyboxMaterial = new Material(storedSkyboxMaterial.shader);
            skyboxMaterial.CopyPropertiesFromMaterial(storedSkyboxMaterial);

            RenderSettings.skybox = skyboxMaterial;

            XRAnchorManager.onTrackingUpdate += (() => UpdateHDRIRotation());

            StorePreviousSkybox();

            if (autoLoadOnEnable)
                if (assetLoader != null && assetLoader.presentationAssetPairs.Count > 0)
                    ReplaceHDRI(assetLoader.presentationAssetPairs[0]);
        }

        void OnDisable() => RenderSettings.skybox = storedSkyboxMaterial;

        void OnValidate()
        {
            assetLoader = GetComponent<RemoteAssetLoader>();
            reflectionProbe = GetComponent<ReflectionProbe>();
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
                Debug.Log("Previous skybox texture stored: " + previousTexture.name, this);
            }
            else
            {
                Debug.LogWarning("No previous skybox texture found.", this);
            }
        }

        void RestorePreviousSkybox()
        {
            RenderSettings.skybox = storedSkyboxMaterial;
            UpdateReflectionsAndGI();
        }

        private async void ReplaceHDRI(RemoteAssetLoader.PresentationAssetPair pair)
        {
            // Load the HDRI texture from the remote asset loader
            Task<object> loadTask = assetLoader.LoadPresentation(pair);

            await loadTask.ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && task.Result != null)
                {
                    Texture2D texture = task.Result as Texture2D;
                    ApplyHDRITexture(texture);
                }
                else
                {
                    Debug.LogError("Failed to load HDRI texture: " + pair.assetKey, this);
                }
            });
        }

        void ApplyHDRITexture(Texture2D texture)
        {
            Dispatcher.Enqueue(() =>
            {
                skyboxMaterial.SetTexture("_MainTex", texture);
                UpdateHDRIRotation(false);
                RenderSettings.skybox = skyboxMaterial;
                UpdateReflectionsAndGI();
                Debug.Log("HDRI texture applied to skybox.", this);
            });
        }

        void UpdateHDRIRotation(bool apply = true)
        {
            float storedRotation = storedSkyboxMaterial.GetFloat("_Rotation");
            float newRotation = (-1*XRContentRoot.Instance.transform.rotation.eulerAngles.y) + storedRotation;
            newRotation = newRotation % 360f;
            skyboxMaterial.SetFloat("_Rotation", newRotation);

            Debug.Log($"HDRI rotation set to {newRotation}, storedRotation: {storedRotation}.", this);

            if (apply)
            {
                RenderSettings.skybox = skyboxMaterial;
                UpdateReflectionsAndGI();
                Debug.Log("HDRI texture applied to skybox.", this);
            }
        }

        void UpdateReflectionsAndGI()
        {
            if (reflectionProbe != null)
            {
                // Update the reflection probe
                reflectionProbe.RenderProbe();
                Debug.Log("Reflection probe updated.", this);
            }
            else
            {
                Debug.LogWarning("Reflection probe is not assigned.", this);
            }

            // Update global illumination
            DynamicGI.UpdateEnvironment();

            Debug.Log("Reflections and GI updated.", this);
        }
    }
}