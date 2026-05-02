using NSYNK.HyperSlides.UI;
using NSYNK.HyperSlides.Input;

using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityMainThreadDispatcher;

#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif

namespace NSYNK.HyperSlides.XR
{
    /// <summary>
    /// Responsible to update found anchors and fire events to update content anchored to specific worldanchors
    /// </summary>
    public class XRAnchorManager : Singleton<XRAnchorManager>
    {
        public ARSession arSession;
        public ARMeshManager arMeshManager;
        public ARAnchorManager arAnchorManager;
        public ARTrackedImageManager arTrackedImageManager;
        public ARTrackedObjectManager arTrackedObjectManager;
        public XRWorldAnchor positionAnchor, rotationAnchor;

        public delegate void OnTrackingUpdate();
        public static OnTrackingUpdate onTrackingUpdate;
        public static ImageWorldAnchors imageWorldAnchors = new ImageWorldAnchors();
        public static bool arSupported = false;
        public static string persistentPath => Path.Combine(Application.persistentDataPath, "my_session.worldmap");

        // those are not anchors
        // private List<ARTrackedImage> imageAnchors = new List<ARTrackedImage>();
        private List<ARTrackable> trackables = new List<ARTrackable>();
        private List<ARAnchor> worldAnchors = new List<ARAnchor>();

        public void OnEnable()
        {
            arTrackedImageManager.trackablesChanged.AddListener(UpdateAnchors);
            arTrackedObjectManager.trackablesChanged.AddListener(UpdateAnchors);
            arAnchorManager.trackablesChanged.AddListener(UpdateAnchors);
        }

        public void OnDisable()
        {
            arTrackedImageManager.trackablesChanged.RemoveAllListeners();
            arTrackedObjectManager.trackablesChanged.RemoveAllListeners();
            arAnchorManager.trackablesChanged.RemoveAllListeners();
        }

        public void Start()
        {
            imageWorldAnchors = LoadAnchorIDsFromDisk();
        }

        /// <summary>
        /// Check if ar is not supported or should not be used
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsARSupported()
        {
            while (ARSession.state == ARSessionState.Installing)
                await Task.Delay(500);

            while (ARSession.state == ARSessionState.SessionInitializing)
                await Task.Delay(500);

            return ARSession.state != ARSessionState.Unsupported && ARSession.state != ARSessionState.None;
        }

        /// <summary>
        /// Check tracking status dependent on the current type
        /// </summary>
        /// <returns></returns>
        public static bool IsTracking()
        {
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Anchors)
                return Instance.positionAnchor.IsTracking() && Instance.rotationAnchor.IsTracking();
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Image)
                return Instance.positionAnchor.IsTracking();

            return RuntimeHandler.Settings.trackingType == Settings.TrackingType.Free;
        }

        /// <summary>
        /// The initial ARSession checks, setup and initiating of setup
        /// </summary>
        public async Awaitable Init()
        {
            arSupported = await IsARSupported();

            //If not real device, skip ar session checks
            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Free || !arSupported)
            {
                await Debug.LogQueue("AR Supported: " + arSupported + ". Fallback to non tracked runtime.", this);

                Dispatcher.Enqueue(() =>
                {
                    positionAnchor.transform.position = XRInputManager.inputUserPosition;
                    positionAnchor.transform.rotation = Quaternion.Euler(0, XRInputManager.inputUserRotation.eulerAngles.y, 0);

                    rotationAnchor.transform.position = positionAnchor.transform.position + positionAnchor.transform.forward * 1;
                    rotationAnchor.transform.LookAt(positionAnchor.transform);

#if !UNITY_EDITOR
                    positionAnchor.gameObject.SetActive(false);
                    rotationAnchor.gameObject.SetActive(false);
#endif
                });

                await SetAnchorSetupManagers(false);
            }
            else
            {
                await Debug.LogQueue("AR Supported: " + arSupported, this);

                while (ARSession.state != ARSessionState.SessionTracking)
                {
                    await Debug.LogQueue("Waiting for ar session tracking: " + ARSession.state, this);
                    await Task.Delay(500);
                }

                Dispatcher.Enqueue(() =>
                {
                    XRUIManager.Instance.InitAnchorSetup();
                });

                await Awaitable.MainThreadAsync();

                await SetAnchorSetupManagers(true);
                await RunAnchorSetupProcess();
            }
        }

        /// <summary>
        /// Check for anchors in the AR world and start setup process if not found
        /// </summary>
        /// <returns></returns>
        private async Awaitable RunAnchorSetupProcess()
        {
            Dispatcher.Enqueue(() =>
            {
                positionAnchor.gameObject.SetActive(true);
                rotationAnchor.gameObject.SetActive(true);
            });

            await SetAnchorSetupManagers(true);
#if UNITY_IOS

            if (RuntimeHandler.Settings.trackingType == Settings.TrackingType.Anchors)
                await UIAnchorSetup.UpdateState(UIAnchorSetup.SetupState.WorldMapping);
            else
                await UIAnchorSetup.UpdateState(UIAnchorSetup.SetupState.ImageTrackingPosition);
#else
            await UIAnchorSetup.UpdateState(UIAnchorSetup.SetupState.Searching);
#endif
            await SetAnchorSetupManagers(false);
        }

        /// <summary>
        /// Enable or disable all meshes for visual candy on setup process
        /// </summary>
        /// <param name="active"></param>
        public async Awaitable SetAnchorSetupManagers(bool active)
        {
            Dispatcher.Enqueue(() =>
            {
                // TODO Turn off object tracking for now
                // arTrackedObjectManager.enabled = false;

                if (!active)
                {
                    try
                    {
                        arMeshManager.DestroyAllMeshes();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("ARMeshManager cant destroy meshes! " + e, this);
                    }
                }

#if UNITY_IOS
                //Disable because only pro ipads are supported with lidar
                arMeshManager.enabled = false;
#else
                arMeshManager.enabled = active;
#endif

                HandleARAnchorManager(active);
                HandleARTrackedImageManager(active);

                onTrackingUpdate?.Invoke();
            });

            await Awaitable.MainThreadAsync();
        }

        private void HandleARAnchorManager(bool active)
        {
            if (!arAnchorManager.enabled)
            {
                Debug.Log("ARAnchorManager => " + active, Instance);

                arAnchorManager.enabled = active;
            }
        }

        private void HandleARTrackedImageManager(bool active)
        {
            bool setImageTrackingEnabled = RuntimeHandler.Settings.trackingType != Settings.TrackingType.Image ? active : true;

            if (setImageTrackingEnabled != arTrackedImageManager.enabled)
            {
                Debug.Log("ARTrackedImageManager => " + setImageTrackingEnabled, Instance);

                if (setImageTrackingEnabled)
                {
                    arTrackedImageManager.enabled = setImageTrackingEnabled;
                    arTrackedImageManager.trackablesChanged.AddListener(UpdateAnchors);
                    // not sure if arTrackeObjectManager needs a separate Handle method
                    // or should it be one for both tracking solutions
                    arTrackedObjectManager.trackablesChanged.AddListener(UpdateAnchors);
                }
                else if (arTrackedImageManager && arTrackedImageManager.subsystem != null && arTrackedImageManager.subsystem.running)
                {
                    arTrackedImageManager.trackablesChanged.RemoveAllListeners();
                    arTrackedObjectManager.trackablesChanged.RemoveAllListeners();
                    arTrackedImageManager.enabled = setImageTrackingEnabled;
                }
            }
        }

        /// <summary>
        /// The tracked image event that gets fired by the <see cref="ARTrackedImageManager"/>
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateAnchors(ARTrackablesChangedEventArgs<ARTrackedImage> obj)
        {
            if (obj == null || obj.added == null || obj.updated == null || obj.removed == null)
                return;

            foreach (ARTrackable trackable in obj.added)
                if (trackable)
                    // UpdateTrackedImageOnWorldAnchor(trackedImage);
                    UpdateWorldAnchorWithTrackable(trackable);

            foreach (ARTrackable trackable in obj.updated)
                if (trackable)
                    // UpdateTrackedImageOnWorldAnchor(trackedImage);
                    UpdateWorldAnchorWithTrackable(trackable);

            foreach (KeyValuePair<TrackableId, ARTrackedImage> trackedImage in obj.removed)
            {
                // if (!trackedImage.Value)
                //     continue;

                // trackables.Remove(trackedImage.Value);
                RemoveAnchorWithTrackable(trackedImage.Value);
            }
        }
        /// <summary>
        /// The tracked object event that gets fired by the <see cref="ARTrackedObjectManager"/>
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateAnchors(ARTrackablesChangedEventArgs<ARTrackedObject> obj)
        {
            if (obj == null || obj.added == null || obj.updated == null || obj.removed == null)
                return;

            foreach (ARTrackable trackable in obj.added)
                if (trackable)
                    // UpdateTrackedImageOnWorldAnchor(trackedImage);
                    UpdateWorldAnchorWithTrackable(trackable);

            foreach (ARTrackable trackable in obj.updated)
                if (trackable)
                    // UpdateTrackedImageOnWorldAnchor(trackedImage);
                    UpdateWorldAnchorWithTrackable(trackable);

            foreach (KeyValuePair<TrackableId, ARTrackedObject> trackedObject in obj.removed)
            {
                // if (!trackedObject.Value)
                //     continue;

                // trackables.Remove(trackedObject.Value);
                RemoveAnchorWithTrackable(trackedObject.Value);
            }
        }

        /// <summary>
        /// The anchor event that gets fired by the <see cref="ARAnchorManager"/>
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateAnchors(ARTrackablesChangedEventArgs<ARAnchor> obj)
        {
            foreach (ARAnchor anchor in obj.added)
                UpdateRootTransform(anchor);

            foreach (ARAnchor anchor in obj.updated)
                UpdateRootTransform(anchor);

            foreach (KeyValuePair<TrackableId, ARAnchor> anchor in obj.removed)
            {
                if (worldAnchors.Contains(anchor.Value))
                    worldAnchors.Remove(anchor.Value);

                if (anchor.Key.ToString().Equals(imageWorldAnchors.positionAnchorID))
                    positionAnchor.RemoveAnchor(anchor.Value);
                if (anchor.Key.ToString().Equals(imageWorldAnchors.rotationAnchorID))
                    rotationAnchor.RemoveAnchor(anchor.Value);
            }
        }
        [System.Obsolete("Use UpdateAnchors method instead")] 
        private void UpdateTrackedImageOnWorldAnchor(ARTrackedImage trackedImage)
        {
            if (trackedImage && trackedImage.trackingState == TrackingState.Tracking)
            {
                if (trackedImage.referenceImage.name.Split('_')[0] == "PositionMarker")
                {
                    positionAnchor.UpdateAnchor(trackedImage);
                    positionAnchor.transform.position = trackedImage.transform.position;
                }
                if (trackedImage.referenceImage.name.Split('_')[0] == "RotationMarker")
                {
                    rotationAnchor.UpdateAnchor(trackedImage);
                    rotationAnchor.transform.position = trackedImage.transform.position;
                }

            }
        }
        
        protected virtual void UpdateWorldAnchorWithTrackable(ARTrackable trackable)
        {
            string name = "";
            if (trackable is ARTrackedObject aRTrackedObject) 
            { 
                name = aRTrackedObject.referenceObject.name.Split('_')[0];
            } 
            else if (trackable is ARTrackedImage aRTrackedImage)
            {
                name = aRTrackedImage.referenceImage.name.Split('_')[0];
            }
            if (trackable && trackable.trackingState == TrackingState.Tracking)
            {
                if (name == "PositionMarker")
                {
                    positionAnchor.UpdateAnchor(trackable);
                    positionAnchor.transform.position = trackable.transform.position;
                }
                if (name == "RotationMarker")
                {
                    rotationAnchor.UpdateAnchor(trackable);
                    rotationAnchor.transform.position = trackable.transform.position;
                }
            }
        }

        protected virtual void RemoveAnchorWithTrackable(ARTrackable trackable)
        {
            if (!trackable)
                return;
            trackables.Remove(trackable);
        }

        /// <summary>
        /// Update the root transform position and rotation
        /// </summary>
        /// <param name="anchor"></param>
        private void UpdateRootTransform(ARAnchor anchor)
        // Function doesn't really update root transfrom, while root transform updates
        // independently in XRContentRoot script. That naming is misleading
        {
            if (imageWorldAnchors == null)
                imageWorldAnchors = LoadAnchorIDsFromDisk();

            if (anchor == null)
                return;

            //Debug.Log("ANCHOR FOUND: " + anchor.trackableId + " ?=> " + imageWorldAnchors.positionAnchorID + anchor.trackableId.ToString().Equals(imageWorldAnchors.positionAnchorID));

            if (anchor.trackableId.ToString().Equals(imageWorldAnchors.positionAnchorID))
            {
                if (!worldAnchors.Contains(anchor))
                    worldAnchors.Add(anchor);

                positionAnchor.UpdateAnchor(anchor);
            }
            if (anchor.trackableId.ToString().Equals(imageWorldAnchors.rotationAnchorID))
            {
                if (!worldAnchors.Contains(anchor))
                    worldAnchors.Add(anchor);

                rotationAnchor.UpdateAnchor(anchor);
            }
        }

        /// <summary>
        /// Removing the gameobject will trigger the removal of the anchor (per docs)
        /// </summary>
        /// <param name="calling"></param>
        public void RemoveAnchor(GameObject calling) => Destroy(calling);

        /// <summary>
        /// Restart anchor setup
        /// </summary>
        public void StartOverAnchorSetup()
        {
            RemoveAllAnchors();
            HyperSlidesStateManager.UpdateAppState(HyperSlidesStateManager.AppState.DEVICE_SETUP);
        }

        /// <summary>
        /// Remove all current anchors from the ARSession and reset world mapping for iOS
        /// </summary>
        private async void RemoveAllAnchors()
        {
            positionAnchor.RemoveAnchor();
            rotationAnchor.RemoveAnchor();  

            worldAnchors.ForEach(anchor =>
            {
                if (anchor != null)
                    Destroy(anchor);
            });

            worldAnchors.Clear();

#if UNITY_IOS
            // Reset session and clear world map
            await XRWorldMapManager.Instance.ResetARSessionCompletely();
#endif

            imageWorldAnchors = new ImageWorldAnchors();
            SaveAnchorIDsToDisk();
        }

        /// <summary>
        /// Load the position and rotation anchors from disk
        /// </summary>
        /// <returns></returns>
        public ImageWorldAnchors LoadAnchorIDsFromDisk()
        {
            if (!File.Exists(Application.persistentDataPath + "/AnchorIDs.json"))
            {
                imageWorldAnchors = new ImageWorldAnchors();
                SaveAnchorIDsToDisk();
            }

            string fileContent = File.ReadAllText(Application.persistentDataPath + "/AnchorIDs.json");

            Debug.Log("Load anchors from local file: " + fileContent, this);

            return JsonUtility.FromJson<ImageWorldAnchors>(fileContent);
        }

        /// <summary>
        /// Save the current created anchors to a file
        /// </summary>
        public void SaveAnchorIDsToDisk()
        {
            string json = JsonUtility.ToJson(imageWorldAnchors);

            Debug.Log("Save anchors to local file: " + json, this);
            File.WriteAllText(Application.persistentDataPath + "/AnchorIDs.json", json);
        }

        [Serializable]
        public class ImageWorldAnchors
        {
            public string positionAnchorID = "";
            public string rotationAnchorID = "";

            /// <summary>
            /// Update the current anchor ids and save them to disk, if they have changed
            /// </summary>
            /// <param name="type">The <see cref="XRWorldAnchor.AnchorType"/> being set to distinguish default, rotation or position anchoring.</param>
            /// <param name="id">The ARSession managed trackable id of the created anchor.</param>
            public void UpdateAnchorID(XRWorldAnchor.AnchorType type, string id)
            {
                bool newID = type == XRWorldAnchor.AnchorType.Position ? !positionAnchorID.Equals(id) : !rotationAnchorID.Equals(id);

                if (type == XRWorldAnchor.AnchorType.Position)
                    positionAnchorID = id;
                if (type == XRWorldAnchor.AnchorType.Rotation)
                    rotationAnchorID = id;

                if (newID)
                    Instance.SaveAnchorIDsToDisk();

#if UNITY_IOS
                XRWorldMapManager.SaveWorldMapAsync();
#endif
            }

            public bool CheckAnchorUpdate(ImageWorldAnchors anchors)
            {
                return positionAnchorID.Equals(anchors.positionAnchorID) && rotationAnchorID.Equals(anchors.rotationAnchorID);
            }
        }
    }
}