using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

#if UNITY_IOS
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;
using UnityMainThreadDispatcher;
#endif

namespace NSYNK.HyperSlides.XR
{
    /// <summary>
    /// Saving and loading an
    /// <a href="https://developer.apple.com/documentation/arkit/arworldmap">ARWorldMap</a>.
    /// </summary>
    public class XRWorldMapManager : Singleton<XRWorldMapManager>
    {
        public TextMeshProUGUI worldmapText;

#if UNITY_IOS && !UNITY_EDITOR
        public static ARWorldMap worldMap = new ARWorldMap();
        public static ARKitSessionSubsystem sessionSubsystem;
        public static int scanAttempts = 0;
        public static ARWorldMappingStatus lastStatus = ARWorldMappingStatus.NotAvailable;

        public static async Task LoadWorldMapAsync() => await Load(XRAnchorManager.persistentPath);
        public static async Task SaveWorldMapAsync() => await Save(XRAnchorManager.persistentPath);

        public bool WorldmapSupported() => sessionSubsystem != null && ARKitSessionSubsystem.worldMapSupported;
        public bool WorldmapMapped() => sessionSubsystem != null && sessionSubsystem.worldMappingStatus == ARWorldMappingStatus.Mapped;
        public int WorldmapFeatures() => sessionSubsystem != null ? sessionSubsystem.requestedFeatures.Count() : 0;

        private void Update()
        {
            if(sessionSubsystem == null)
            {
                if (XRAnchorManager.Instance.arSession.subsystem != null)
                    sessionSubsystem = (ARKitSessionSubsystem)XRAnchorManager.Instance.arSession.subsystem;

                return;
            }

            if (!WorldmapSupported())
                return;

            if(worldmapText)
                worldmapText.text = sessionSubsystem.worldMappingStatus.ToString();

            //if (lastStatus == ARWorldMappingStatus.NotAvailable && sessionSubsystem.worldMappingStatus == ARWorldMappingStatus.Mapped || lastStatus == ARWorldMappingStatus.Limited && sessionSubsystem.worldMappingStatus == ARWorldMappingStatus.Mapped)
            //{
            //    Debug.Log("GOT WORLDMAP SCAN ATTEMPT");
            //    lastStatus = sessionSubsystem.worldMappingStatus;
            //    scanAttempts++;
            //}

            //if(lastStatus == ARWorldMappingStatus.Mapped && sessionSubsystem.worldMappingStatus == ARWorldMappingStatus.Limited)
            //    lastStatus = sessionSubsystem.worldMappingStatus;
        }

        /// <summary>
        /// Reset the <c>ARSession</c>, destroying any existing trackables, such as planes.
        /// Upon loading a saved <c>ARWorldMap</c>, saved trackables will be restored.
        /// </summary>
        public async void ResetARSession()
        {
            XRAnchorManager.Instance.arSession.Reset();

            await Save(XRAnchorManager.persistentPath, true);
        }

        /// <summary>
        /// Reset the <c>ARSession</c>, destroying any existing trackables and world mapping data.
        /// </summary>
        public async Task ResetARSessionCompletely()
        {
            // First delete the world map file
            if (File.Exists(XRAnchorManager.persistentPath))
            {
                try
                {
                    File.Delete(XRAnchorManager.persistentPath);
                    Debug.Log("Deleted world map file");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to delete world map: {e}");
                }
            }

            // Reset ARKit session
            if (sessionSubsystem != null)
            {
                // Create new empty world map to clear existing one
                worldMap = new ARWorldMap();
                sessionSubsystem.ApplyWorldMap(worldMap);
            }

            // Reset AR Session
            XRAnchorManager.Instance.arSession.Reset();

            // Wait for session to stabilize
            await Task.Delay(100);

            // Save empty world map
            await Save(XRAnchorManager.persistentPath, true);
        }

        static void SaveAndDisposeWorldMap(string savePath)
        {
            if (!worldMap.valid)
            {
                Debug.Log("No valid worldmap to save");
                return;
            }
            
            var data = worldMap.Serialize(Allocator.Temp);
            var file = File.Open(savePath, FileMode.Create);
            var writer = new BinaryWriter(file);
            writer.Write(data.ToArray());
            writer.Close();
            data.Dispose();
            file.Close();
        }

        static async Awaitable Save(string savePath, bool reset = false)
        {
            await Debug.LogQueue("Saving worldmap", Instance);

            if (sessionSubsystem == null)
                return;

            await RequestARWorldMapAsync(reset);

            SaveAndDisposeWorldMap(savePath);
        }

        static async Awaitable Load(string path)
        {
            await Debug.LogQueue("Loading worldmap");

            await RequestARWorldMapAsync();

            if (File.Exists(path))
            {
                FileStream file = null;
                BinaryReader binaryReader = null;
                try
                {
                    file = File.Open(path, FileMode.Open, FileAccess.Read);
                    const int bytesPerFrame = 1024 * 10;
                    var bytesRemaining = file.Length;
                    binaryReader = new BinaryReader(file);
                    var allBytes = new List<byte>();

                    while (bytesRemaining > 0)
                    {
                        var bytes = binaryReader.ReadBytes(bytesPerFrame);
                        allBytes.AddRange(bytes);
                        bytesRemaining -= bytesPerFrame;
                    }

                    var data = new NativeArray<byte>(allBytes.Count, Allocator.Temp);
                    data.CopyFrom(allBytes.ToArray());

                    if (ARWorldMap.TryDeserialize(data, out worldMap))
                    {
                        data.Dispose();

                        if (!worldMap.valid)
                            worldMap = new ARWorldMap();
                        else
                            Debug.Log("Valid worldmap");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
                finally
                {
                    if (binaryReader != null)
                        binaryReader.Close();

                    if (file != null)
                        file.Close();
                }
            }
            else
                Debug.Log("No worldmap file at " + path);

            if(sessionSubsystem != null)
                sessionSubsystem.ApplyWorldMap(worldMap);
        }

        public static async Awaitable RequestARWorldMapAsync(bool reset = false)
        {
            if (sessionSubsystem == null)
                return;

            if (reset)
                worldMap = new ARWorldMap();

            var request = sessionSubsystem.GetARWorldMapAsync();
            while (!request.status.IsDone())
                await Task.Yield();

            if (request.status != ARWorldMapRequestStatus.Success)
                worldMap = new ARWorldMap();
            else
                worldMap = request.GetWorldMap();
        }
#endif
    }
}