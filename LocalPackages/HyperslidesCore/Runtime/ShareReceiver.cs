using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NSYNK.HyperSlides.Runtime;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif
using UnityEngine;

namespace NSYNK.HyperSlides.Core
{
    /// <summary>
    /// This class is responsible for receiving shared assets via deep links and loading them into the application. It listens for deep link activations and checks for shared files in the app group container, specifically targeting HDRI textures to apply to the scene.
    /// </summary>
    public class ShareReceiver : Singleton<ShareReceiver>
    {
        private static string AppGroup => ResolveAppGroupIdentifier();

        private static string ResolveAppGroupIdentifier()
        {
            string bundleIdentifier = Application.identifier;
            if (string.IsNullOrEmpty(bundleIdentifier))
            {
                return "group.com.nsynk.hyperslides-core";
            }

            if (bundleIdentifier.EndsWith(".share", StringComparison.OrdinalIgnoreCase))
            {
                bundleIdentifier = bundleIdentifier.Substring(0, bundleIdentifier.Length - ".share".Length);
            }

            return "group." + bundleIdentifier;
        }

        /// <summary>
        /// Receive deep links and check for shared assets when the app is opened via a deep link or when the app starts.
        /// </summary>
        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            Application.deepLinkActivated += HandleDeepLink;

            if (!string.IsNullOrEmpty(Application.absoluteURL))
                HandleDeepLink(Application.absoluteURL);

            LoadSavedFile();
        }

        private void LoadSavedFile()
        {
            string lastFile = PlayerPrefs.GetString("last_shared_file_name", null);

            if (File.Exists(Path.Combine(Application.persistentDataPath, lastFile)))
            {
                Debug.Log($"Found previously loaded shared file: {lastFile}. Attempting to load.");
                CheckForSharedAsset(lastFile, false);
            }
            else
                Debug.Log("No previously loaded shared file found or file no longer exists.");
        }

        /// <summary>/ 
        /// OnValidate is called in the editor when the script is loaded or a value changes in the inspector
        /// Here we set the iOS URL scheme to ensure deep linking works correctly when building for iOS. 
        /// </summary>
        void OnValidate()
        {
#if UNITY_EDITOR
            //Set deep link supported url scheme
            string urlScheme = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS).Split('.').Last().ToLower();
            PlayerSettings.iOS.iOSUrlSchemes = new[] { urlScheme };
#endif
        }

        /// <summary>
        /// Checks for a shared asset in the app group container, loads it, and applies it as an HDRI texture if found. This is intended to be called on app start or when a deep link is received.
        /// </summary>
        public static void CheckForSharedAsset(string filename = null, bool isDeepLink = true)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Debug.Log($"CheckForSharedAsset called with filename: {filename}");

            filename = ResolveFilename(filename);

            try
            {
                int pointerLength;
                string fileToRead = string.IsNullOrEmpty(filename) ? "HDRIMap.exr" : filename;

                IntPtr ptr = GetSharedFileBytes(fileToRead, AppGroup, out pointerLength);
                if (ptr == IntPtr.Zero || pointerLength == 0 || !isDeepLink)
                {
                    Debug.Log("No shared image found in app group container. Trying to load previously saved file if available.");
                    byte[] data = new byte[pointerLength];
                    if (File.Exists(Path.Combine(Application.persistentDataPath, fileToRead)))
                    {
                        data = File.ReadAllBytes(Path.Combine(Application.persistentDataPath, fileToRead));
                        Texture2D texture = TryCreateTextureFromSharedData(fileToRead, data);
                        if (texture != null)
                        {
                            Debug.Log("Successfully loaded previously saved shared image into Texture2D.");

                            if (HDRIReplace.Instance != null)
                                HDRIReplace.Instance.ApplyHDRITexture(texture);

                            CleanupProcessedSharedFile(fileToRead);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No previously saved shared file found. Aborting shared asset load.");
                        return;
                    }
                }
                else
                {
                    byte[] data = new byte[pointerLength];
                    Marshal.Copy(ptr, data, 0, pointerLength);
                    FreeSharedByteArray(ptr);

                    string savePath = Path.Combine(Application.persistentDataPath, fileToRead);
                    File.WriteAllBytes(savePath, data);

                    Debug.Log($"Received {fileToRead} ({pointerLength} bytes), saved to: {savePath}");
                    PlayerPrefs.SetString("last_shared_file_name", fileToRead);

                    Texture2D texture = TryCreateTextureFromSharedData(fileToRead, data);
                    if (texture == null)
                    {
                        Debug.LogWarning("Failed to load shared image data into Texture2D.");
                        return;
                    }

                    Debug.Log("Successfully loaded shared image into Texture2D.");

                    if (HDRIReplace.Instance != null)
                        HDRIReplace.Instance.ApplyHDRITexture(texture);
                        
                    CleanupProcessedSharedFile(fileToRead);
                }

            }
            catch (Exception e)
            {
                Debug.LogWarning("Error checking shared file: " + e.Message);
            }
#else
            Debug.Log("Not running on iOS or running in Editor — skipping share-check.");
#endif
        }

        /// <summary>
        /// Resolves the filename to read from the deep link parameter or falls back to the last shared file name stored.
        /// </summary>
        private static string ResolveFilename(string filename)
        {
            if (!string.IsNullOrEmpty(filename)) return filename;

            try
            {
                return UserDefaultsGetString("last_shared_file_name", AppGroup);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed reading last_shared_file_name from shared defaults: " + e.Message);
                return filename;
            }
        }

        /// <summary>
        /// Attempts to create a Texture2D from the shared data. For HDR images, it tries to use a native path for better performance, and falls back to ImageConversion.LoadImage if that fails or if it's not an HDR format.
        /// </summary>
        private static Texture2D TryCreateTextureFromSharedData(string fileToRead, byte[] data)
        {
            string extension = Path.GetExtension(fileToRead)?.ToLowerInvariant() ?? string.Empty;
            bool isHDRSource = extension == ".exr" || extension == ".hdr";

            if (isHDRSource)
            {
                string localPath = Path.Combine(Application.persistentDataPath, fileToRead);
                Texture2D localTexture = TryCreateNativeHDRTextureFromLocalPath(localPath);
                if (localTexture != null)
                {
                    return localTexture;
                }

                Texture2D sharedTexture = TryCreateNativeHDRTextureFromAppGroup(fileToRead);
                if (sharedTexture != null)
                {
                    return sharedTexture;
                }

                Debug.LogWarning($"Native HDR decode failed for {fileToRead} (local + app group). Falling back to ImageConversion.LoadImage.");
            }

            Texture2D texture = new Texture2D(2, 2);
            return ImageConversion.LoadImage(texture, data) ? texture : null;
        }

        /// <summary>
        /// Attempts to create a Texture2D from a local file path using a native method to decode HDR
        /// </summary>
        private static Texture2D TryCreateNativeHDRTextureFromLocalPath(string localPath)
        {
            if (!File.Exists(localPath))
                return null;

            IntPtr hdrPtr = GetHDRRGBAHalfFromFilePath(localPath, out int width, out int height, out int length);
            return CreateHDRTextureFromNativePointer(hdrPtr, width, height, length, localPath);
        }

        /// <summary>
        /// Attempts to create a Texture2D from a native pointer obtained from the app group shared data.
        /// </summary>
        private static Texture2D TryCreateNativeHDRTextureFromAppGroup(string fileName)
        {
            IntPtr hdrPtr = GetSharedHDRRGBAHalf(fileName, AppGroup, out int width, out int height, out int length);
            return CreateHDRTextureFromNativePointer(hdrPtr, width, height, length, fileName);
        }

        /// <summary>
        /// Creates a Texture2D from a native pointer containing HDR RGBAHalf data.
        /// </summary> 
        /// <param name="hdrPointerToFile">Pointer to the HDR image data in RGBAHalf format.</param>
        /// <param name="width">Width of the HDR image.</param>
        /// <param name="height">Height of the HDR image.</param>
        /// <param name="length">Length in bytes of the HDR image data.</param>
        /// <param name="sourceName">Name of the source file or path.</param>
        private static Texture2D CreateHDRTextureFromNativePointer(IntPtr hdrPointerToFile, int width, int height, int length, string sourceName)
        {
            if (hdrPointerToFile == IntPtr.Zero || width <= 0 || height <= 0 || length <= 0)
            {
                return null;
            }

            byte[] hdrRaw = new byte[length];
            try
            {
                Marshal.Copy(hdrPointerToFile, hdrRaw, 0, length);
            }
            finally
            {
                FreeSharedByteArray(hdrPointerToFile);
            }

            // EXR files store data bottom-to-top, Unity expects top-to-bottom - flip vertically
            FlipTextureDataVertically(hdrRaw, width, height, 8); // 8 bytes per pixel for RGBAHalf (4 channels × 2 bytes)

            Texture2D hdrTexture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true);
            hdrTexture.LoadRawTextureData(hdrRaw);
            hdrTexture.Apply(false, false);
            Debug.Log($"Decoded HDR texture via native path: {width}x{height} ({sourceName})");
            return hdrTexture;
        }

        /// <summary>
        /// Flips raw texture data vertically (row by row) in place.
        /// </summary>
        private static void FlipTextureDataVertically(byte[] data, int width, int height, int bytesPerPixel)
        {
            int rowSize = width * bytesPerPixel;
            byte[] tempRow = new byte[rowSize];

            for (int y = 0; y < height / 2; y++)
            {
                int topRowStart = y * rowSize;
                int bottomRowStart = (height - 1 - y) * rowSize;

                Buffer.BlockCopy(data, topRowStart, tempRow, 0, rowSize);
                Buffer.BlockCopy(data, bottomRowStart, data, topRowStart, rowSize);
                Buffer.BlockCopy(tempRow, 0, data, bottomRowStart, rowSize);
            }
        }

        [DllImport("__Internal")]
        static extern IntPtr GetSharedFileBytes(string fileName, string group, out int length);

        [DllImport("__Internal")]
        static extern IntPtr GetSharedHDRRGBAHalf(string fileName, string group, out int width, out int height, out int length);

        [DllImport("__Internal")]
        static extern IntPtr GetHDRRGBAHalfFromFilePath(string filePath, out int width, out int height, out int length);

        [DllImport("__Internal")]
        static extern void FreeSharedByteArray(IntPtr ptr);

        [DllImport("__Internal")]
        static extern int DeleteSharedFileFromAppGroup(string fileName, string group);

        [DllImport("__Internal")]
        static extern int RemoveSharedDefaultsKey(string key, string group);

        [DllImport("__Internal")]
        static extern IntPtr GetAppGroupContainerPath(string group);

        [DllImport("__Internal")]
        static extern void FreeSharedCString(IntPtr ptr);

        private static string UserDefaultsGetString(string key, string suiteName)
        {
#if UNITY_IOS && !UNITY_EDITOR
            // This key is mirrored by the share extension into app-group defaults.
            return NSUserDefaultsBridge.GetStringFromSuite(key, suiteName);
#else
            return string.Empty;
#endif
        }

        private static void CleanupProcessedSharedFile(string fileName)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                int fileDeleted = DeleteSharedFileFromAppGroup(fileName, AppGroup);
                int removedNameKey = RemoveSharedDefaultsKey("last_shared_file_name", AppGroup);
                int removedTimestampKey = RemoveSharedDefaultsKey("last_shared_image", AppGroup);

                Debug.Log($"Share cleanup: fileDeleted={fileDeleted}, removedNameKey={removedNameKey}, removedTimestampKey={removedTimestampKey}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to clean up processed shared file: " + e.Message);
            }
#endif
        }

        /// <summary>
        /// Handles incoming deep links, extracts the shared file name parameter, and initiates the process to check for and load the shared asset. This is called when the app is opened via a deep link.
        /// </summary>
        private static void HandleDeepLink(string url)
        {
            Debug.Log($"App opened via deep link: {url}");
            var fname = GetQueryParameter(url, "file");
            CheckForSharedAsset(fname);
        }

        /// <summary>
        /// Extracts the value of a query parameter from a URL.
        /// </summary>
        private static string GetQueryParameter(string url, string key)
        {
            try
            {
                var uri = new Uri(url);
                var q = uri.Query; // starts with '?'
                if (string.IsNullOrEmpty(q)) return null;
                var parts = q.TrimStart('?').Split('&');
                foreach (var p in parts)
                {
                    var kv = p.Split(new char[] { '=' }, 2);
                    if (kv.Length == 2)
                    {
                        var k = Uri.UnescapeDataString(kv[0]);
                        if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                        {
                            return Uri.UnescapeDataString(kv[1]);
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }

    internal static class NSUserDefaultsBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr GetStringFromSharedDefaults(string key, string group);

        [DllImport("__Internal")]
        private static extern void FreeSharedCString(IntPtr ptr);
#endif

        public static string GetStringFromSuite(string key, string group)
        {
#if UNITY_IOS && !UNITY_EDITOR
            IntPtr ptr = GetStringFromSharedDefaults(key, group);
            string result = Marshal.PtrToStringAuto(ptr) ?? string.Empty;
            FreeSharedCString(ptr);
            return result;
#else
            return string.Empty;
#endif
        }
    }
}