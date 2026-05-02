using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class AssetLoader : MonoBehaviour
{
    public string serverURL = "https://minio.nsynk.de/hyperslides/assets";

    public List<PresentationAssetPair> presentationAssetPairs;
    public bool fakeOffline = true;

    private string localCatalogPath;
    private AsyncOperationHandle<IResourceLocator> catalogHandle;

    [Serializable]
    public class PresentationAssetPair
    {
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

    private void OnGUI()
    {
        GUILayout.BeginVertical();

        presentationAssetPairs.ForEach(p =>
        {
            if (GUILayout.Button("Load " + p.projectName))
                LoadPresentation(p);
        });

        GUILayout.EndVertical();
    }

    async void LoadPresentation(PresentationAssetPair pair)
    {
        string projectPath = $"{Application.persistentDataPath}/{pair.projectName}{BuildTarget()}";

        if (!Directory.Exists(projectPath))
            Directory.CreateDirectory(projectPath);

        localCatalogPath = $"{projectPath}/{pair.catalogName}";

        if (File.Exists(localCatalogPath) && fakeOffline)
        {
            Debug.Log("Loading cached catalog");
            await LoadCatalog(localCatalogPath);
        }
        else
        {
            string url = GetFullLoadURL(pair);
            Debug.Log("Downloading catalog: " + url);
            await DownloadAndCacheCatalog(url);
        }

        await LoadAsset(pair.assetKey);
    }

    async Task DownloadAndCacheCatalog(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest request = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download catalog: {request.error}");
                return;
            }

            File.WriteAllBytes(localCatalogPath, request.downloadHandler.data);
            Debug.Log("Catalog downloaded and cached: " + localCatalogPath);

            await LoadCatalog(localCatalogPath);
        }
    }

    async Task LoadCatalog(string path)
    {
        catalogHandle = Addressables.LoadContentCatalogAsync(path);
        await catalogHandle.Task;

        if (catalogHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"Failed to load catalog from {path}");
            return;
        }

        Debug.Log("Catalog loaded successfully!");
    }

    async Task LoadAsset(string key)
    {
        if (catalogHandle.Result.Locate(key, typeof(object), out IList<IResourceLocation> locations) && locations.Count > 0)
        {
            foreach (var location in locations)
            {
                Debug.Log($"Asset '{key}' found! Type: {location.ResourceType}");

                switch (location.ResourceType.ToString())
                {
                    case "UnityEngine.Texture2D":
                        AsyncOperationHandle<Texture2D> assetHandle = Addressables.LoadAssetAsync<Texture2D>(location);
                        await assetHandle.Task;

                        if (assetHandle.Status == AsyncOperationStatus.Succeeded)
                        {
                            GameObject testSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            Material testMaterial = new Material(testSphere.GetComponent<MeshRenderer>().sharedMaterial);
                            testMaterial.name = "TEST MATERIAL";
                            testMaterial.mainTexture = assetHandle.Result;
                            testSphere.GetComponent<MeshRenderer>().sharedMaterial = testMaterial;

                            Debug.Log($"Instantiated asset: {key}");
                        }
                        else
                        {
                            Debug.LogError($"Failed to load asset: {key}");
                        }
                        // assetHandle.Release();
                        break;
                }
            }
        }
        else
        {
            Debug.LogError($"Asset '{key}' not found in Addressables.");
        }
    }
}
