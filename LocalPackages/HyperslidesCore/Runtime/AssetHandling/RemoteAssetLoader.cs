using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Handles loading of remote assets from a server, caching them locally.
    /// </summary>
    public class RemoteAssetLoader : Singleton<RemoteAssetLoader>
    {
        public string serverURL = "https://minio.nsynk.de/hyperslides/assets";

        public List<PresentationAssetPair> presentationAssetPairs;
        public List<Texture2D> loadedTextures;
        public bool fakeOffline = true;

        private string localCatalogPath;
        private AsyncOperationHandle<IResourceLocator> catalogHandle;

        [Serializable]
        public class PresentationAssetPair
        {
            public PresentationAssetPair(string projectName, string catalogName, string assetKey)
            {
                this.projectName = projectName;
                this.catalogName = catalogName;
                this.assetKey = assetKey;
            }

            public string projectName = "";
            public string catalogName = "";
            public string assetKey = "";
        }

        private string GetFullLoadURL(PresentationAssetPair pair) => $"{serverURL}/{pair.projectName}{BuildTarget()}/{pair.catalogName}";

        private string BuildTarget()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.VisionOS:
                    return "/VisionOS";
                case RuntimePlatform.Android:
                    return "/Android";
                case RuntimePlatform.IPhonePlayer:
                    return "/iOS";
                default:
#if UNITY_EDITOR
                    return $"/{UnityEditor.EditorUserBuildSettings.activeBuildTarget}";
#else
                return "";
#endif
            }
        }

        // private void OnGUI()
        // {
        //     GUILayout.BeginVertical();

        //     presentationAssetPairs.ForEach(p =>
        //     {
        //         if (GUILayout.Button("Load " + p.projectName))
        //             LoadPresentation(p);
        //         if (GUILayout.Button("Delete cached file " + p.projectName))
        //             DeleteCachedFile(p);
        //     });

        //     GUILayout.EndVertical();
        // }

        private void DeleteCachedFile(PresentationAssetPair pair)
        {
            string projectPath = $"{Application.persistentDataPath}/{pair.projectName}{BuildTarget()}";
            string catalogPath = $"{projectPath}/{pair.catalogName}";

            if (File.Exists(catalogPath))
            {
                File.Delete(catalogPath);
                Debug.Log($"Deleted cached file: {catalogPath}", this);
            }
            else
            {
                Debug.LogWarning($"No cached file found to delete: {catalogPath}", this);
            }
        }

        public async Task<object> LoadPresentation(PresentationAssetPair pair, bool forceUpdate = false)
        {
            try
            {
                string projectPath = $"{Application.persistentDataPath}/{pair.projectName}{BuildTarget()}";

                if (!Directory.Exists(projectPath))
                    Directory.CreateDirectory(projectPath);

                localCatalogPath = $"{projectPath}/{pair.catalogName}";

                bool localCatalogPathExists = File.Exists(localCatalogPath);

                Debug.Log($"Local catalog path: {localCatalogPath} exists {localCatalogPathExists}", this);

                bool isOffline = Application.internetReachability == NetworkReachability.NotReachable || fakeOffline;

                if (isOffline)
                {
                    Debug.Log("Loading cached catalog", this);
                    await LoadCatalog(localCatalogPath);
                }
                else
                {
                    string url = GetFullLoadURL(pair);
                    Debug.Log("Downloading catalog: " + url, this);
                    await DownloadAndCacheCatalog(url);
                }

                Task<object> assetLoader = LoadAsset(pair.assetKey);
                await assetLoader;

                if (assetLoader.Result == null)
                {
                    Debug.LogError($"Failed to load asset: {pair.assetKey}", this);
                    return null;
                }
                else
                {
                    Debug.Log($"Successfully loaded asset: {pair.assetKey}", this);
                    return assetLoader.Result;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading project: {pair.projectName} - {e.Message}", this);
                return null;
            }
        }

        async Task DownloadAndCacheCatalog(string url)
        {
            using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
            {
                request.timeout = 5;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Failed to download catalog: {request.error}, trying to load from cache.", this);
                    
                    if (string.IsNullOrEmpty(localCatalogPath) || !File.Exists(localCatalogPath))
                        Debug.LogError("No cached catalog available.", this);
                    else
                        await LoadCatalog(localCatalogPath);
                        
                    return;
                }

                File.WriteAllBytes(localCatalogPath, request.downloadHandler.data);
                Debug.Log("Catalog downloaded and cached: " + localCatalogPath, this);

                await LoadCatalog(localCatalogPath);
            }
        }

        async Task LoadCatalog(string path)
        {
            catalogHandle = Addressables.LoadContentCatalogAsync(path);
            await catalogHandle.Task;

            if (catalogHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning($"Failed to load catalog from {path}", this);
                return;
            }

            Debug.Log("Catalog loaded successfully!", this);
        }

        async Task<object> LoadAsset(string key)
        {
            if (catalogHandle.Result.Locate(key, typeof(object), out IList<IResourceLocation> locations) && locations.Count > 0)
            {
                foreach (var location in locations)
                {
                    Debug.Log($"Asset '{key}' found! Type: {location.ResourceType}", this);

                    switch (location.ResourceType.ToString())
                    {
                        case "UnityEngine.Texture2D":
                            AsyncOperationHandle<Texture2D> assetHandle = Addressables.LoadAssetAsync<Texture2D>(location);
                            await assetHandle.Task;

                            if (assetHandle.Status == AsyncOperationStatus.Succeeded)
                            {
                                Texture2D preloadedTexture = loadedTextures.Find(t => t.name == assetHandle.Result.name);

                                if (preloadedTexture == null)
                                {
                                    loadedTextures.Add(assetHandle.Result);
                                    preloadedTexture = assetHandle.Result;
                                }
                                else
                                    assetHandle.Release();

                                return preloadedTexture;
                            }
                            else
                            {
                                Debug.LogError($"Failed to load asset: {key}", this);
                                return null;
                            }
                            // assetHandle.Release();
                            // break;
                    }
                }

                return null;
            }
            else
            {
                Debug.LogError($"Asset '{key}' not found in Addressables.", this);
                return null;
            }
        }
    }
}