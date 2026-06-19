using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Network;

#if UNITY_VISIONOS
using Unity.PolySpatial;
#endif

using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.Runtime
{
    public class XRCameraManager : Singleton<XRCameraManager>
    {
        public static Camera CurrentCamera => DeviceInfo.Instance.Role == XRPlayer.Role.Simulation ? Instance.spectatorCamera : Instance.mainCamera;

        public List<CameraSettings> cameraSettings;

        [HideInInspector]
        public Camera mainCamera, spectatorCamera;

        [SerializeField]
        private GameObject spectatorCameraPrefab;

        [SerializeField]
        private AREnvironmentProbeManager probeManager;

        private Transform xrOriginParent;
        private UniversalAdditionalCameraData mainCameraData;
        private Camera storedCamera;

#if UNITY_VISIONOS
        public delegate void WindowUpdateEvent(VolumeCamera.WindowState state);
        public WindowUpdateEvent OnWindowUpdate;
        public VolumeCamera.WindowEvent lastEvent = VolumeCamera.WindowEvent.Opened;

        private VolumeCamera volumeCamera;
#endif

        protected override void OnSingletonAwake()
        {
            mainCamera = Camera.main;

            xrOriginParent = mainCamera.transform.parent;
            mainCameraData = mainCamera.GetUniversalAdditionalCameraData();

            GameObject storedCameraGO = new GameObject("Stored Camera");
            storedCameraGO.transform.parent = mainCamera.transform;
            storedCameraGO.SetActive(false);

            storedCamera = storedCameraGO.AddComponent<Camera>();
            storedCamera.CopyFrom(mainCamera);
            storedCamera.depth = -1;
        }

#if UNITY_VISIONOS
        private async void Start()
#else
        private void Start()
#endif
        {
            if (!spectatorCamera)
                spectatorCamera = Instantiate(spectatorCameraPrefab, XRContentRoot.Instance.transform, false).GetComponent<Camera>();

            spectatorCamera.gameObject.SetActive(false);

#if UNITY_VISIONOS
            await WaitForVolumeCamera();
#endif
        }

        private void OnEnable() => XRNetworkManager.Instance.OnUserConnected += SetupSpectatorCameras;
        private void OnDisable() => XRNetworkManager.Instance.OnUserConnected -= SetupSpectatorCameras;

        public void SetupSpectatorCameras()
        {
            Dispatcher.Enqueue(() =>
            {
                bool isSpectator = DeviceInfo.Instance.Role == XRPlayer.Role.Simulation;

                if (probeManager)
                    probeManager.enabled = !isSpectator;

                SetMainCamera(isSpectator);
                SetSpectatorCamera(isSpectator);

                DynamicGI.UpdateEnvironment();
                //Debug.Log("Set camera to spectator: " + isSpectator, this);
            });
        }

        private void SetMainCamera(bool spectator)
        {
            mainCamera.GetComponent<TrackedPoseDriver>().enabled = !spectator;
            mainCamera.GetComponent<ARCameraBackground>().enabled = !spectator;

            mainCamera.transform.SetParent(spectator ? spectatorCamera.transform : xrOriginParent, false);

            mainCameraData.renderType = spectator ? CameraRenderType.Overlay : CameraRenderType.Base;

            if (!spectator)
                mainCamera.CopyFrom(storedCamera);

            mainCamera.depth = 0;

            mainCamera.transform.localPosition = spectator ? Vector3.zero : mainCamera.transform.localPosition;
            mainCamera.transform.localRotation = spectator ? Quaternion.identity : mainCamera.transform.localRotation;

            UpdateCameraSettings();
        }

        private void SetSpectatorCamera(bool spectator)
        {
            if (spectatorCamera != null)
                spectatorCamera.gameObject.SetActive(spectator);

            var spectatorCameraData = spectatorCamera.GetUniversalAdditionalCameraData();

            if (spectator && !spectatorCameraData.cameraStack.Contains(mainCamera))
                spectatorCameraData.cameraStack.Add(mainCamera);
            else if (!spectator && spectatorCameraData.cameraStack.Contains(mainCamera))
                spectatorCameraData.cameraStack.Remove(mainCamera);
        }

        public void UpdateXROriginPosition(float duration)
        {
            if (XRNetworkManager.Instance.LocalPlayer && XRSlideManager.Instance.CurrentPresentation != null && spectatorCamera)
            {
                if (DeviceInfo.Instance.Role == XRPlayer.Role.Simulation)
                {
                    Vector3 nextPosition = XRSlideManager.Instance.GetCurrentSlide().cameraTransform.location;
                    Quaternion nextRotation = Quaternion.Euler(XRSlideManager.Instance.GetCurrentSlide().cameraTransform.rotation);

                    this.Animate(spectatorCamera.transform, Easing.AnimationType.LocalPosition, Easing.Ease.EaseInOutQuad, spectatorCamera.transform.localPosition, nextPosition, duration);
                    this.Animate(spectatorCamera.transform, Easing.RotationType.Local, Easing.Ease.EaseInOutQuad, startRotation: spectatorCamera.transform.localRotation, nextRotation, duration, 0);
                }
                else
                {
                    spectatorCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }
        }

#if UNITY_VISIONOS
        public async Awaitable WaitForVolumeCamera()
        {
            while (!volumeCamera)
            {
                volumeCamera = FindFirstObjectByType<VolumeCamera>();
                await Task.Delay(500);
            }

            volumeCamera.WindowStateChanged.AddListener(OnVolumeCameraUpdate);
        }

#if UNITY_EDITOR
        private void OnApplicationFocus(bool focus)
        {
            VolumeCamera.WindowState editorState = new();
            editorState.WindowEvent = focus ? VolumeCamera.WindowEvent.Focused : VolumeCamera.WindowEvent.Backgrounded;
            OnVolumeCameraUpdate(volumeCamera, editorState);
        }
#endif

        private void OnVolumeCameraUpdate(VolumeCamera camera, VolumeCamera.WindowState state)
        {
            // Local function to normalize Opened/Focused to Focused
            VolumeCamera.WindowEvent Normalize(VolumeCamera.WindowEvent e) =>
                (e == VolumeCamera.WindowEvent.Opened || e == VolumeCamera.WindowEvent.Focused)
                    ? VolumeCamera.WindowEvent.Focused
                    : e;

            if (Normalize(state.WindowEvent) != Normalize(lastEvent))
            {
                Debug.Log("New window event: " + state.WindowEvent, Instance);
                OnWindowUpdate?.Invoke(state);
                lastEvent = state.WindowEvent;
            }
        }
#endif

        /// <summary>
        /// Update camera settings based on platform-specific configurations
        /// </summary>
        private void UpdateCameraSettings()
        {
            CameraSettings settings = cameraSettings.Find(s => s.platform == Application.platform);

            if (settings == null || CurrentCamera == null)
                return;

            UniversalAdditionalCameraData cameraData = CurrentCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = settings.renderPostProcessing;
            cameraData.antialiasing = settings.antialiasingMode;

            if (settings.antialiasingMode == AntialiasingMode.TemporalAntiAliasing)
            {
                cameraData.taaSettings.contrastAdaptiveSharpening = settings.TAAContrastAdaptiveSharpening;
            }

            Debug.Log($"Applied camera settings for {Application.platform}: PP {settings.renderPostProcessing}, AA {settings.antialiasingMode}, TAA Sharpness {settings.TAAContrastAdaptiveSharpening}", this);
        }
    }
}