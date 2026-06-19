using UnityEngine;
using UnityEngine.UI;
#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif
#if UNITY_IOS && ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
using VoxelBusters.ScreenRecorderKit;
#endif
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using Nakama;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.UI;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine.Events;

namespace NSYNK.HyperSlides.Runtime
{
    /// <summary>
    /// Manager for productions of videos using an iPad
    /// Starts and stops recordings
    /// </summary>
    public class VideoProductionManager : Singleton<VideoProductionManager>
    {
#if !ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
        [Header("Video Recording Warning")]
        public string missingPluginError = "Please add the VoxelBusters Plugin to your project to enable video recording.";
#endif
        public bool enableMicrophone = false;
        public UnityEvent<bool> OnRecordingToggled;

        public const int RecordingNotificationCode = 200;
        public const int OcclusionNotificationCode = 202;

        [Space(10)]
        [HyperSlides.ReadOnly, SerializeField]
        private AROcclusionManager occlusionManager;

#if UNITY_IOS && ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
        private IScreenRecorder recorder;
#endif

        [SerializeField]
        private RecordTimer[] timers = null;

        private bool bIsRecording = false;

        protected override void OnSingletonAwake()
        {
#if UNITY_IOS
            //get objects in next frame to be sure they are ready.
            this.ExecuteNextFrame(() =>
            {
                occlusionManager = FindFirstObjectByType<AROcclusionManager>();
                if (iOSUIManager.Instance)
                {
                    iOSUIManager.Instance.RecordButton.onClick.RemoveAllListeners();
                    iOSUIManager.Instance.RecordButton.onClick.AddListener(ToggleRecording);
                }
            });
#endif

#if UNITY_IOS
            //get objects in next frame to be sure they are ready.
            this.ExecuteNextFrame(() =>
            {
                occlusionManager = FindFirstObjectByType<AROcclusionManager>();
                if (iOSUIManager.Instance)
                {
                    iOSUIManager.Instance.RecordButton.onClick.RemoveAllListeners();
                    iOSUIManager.Instance.RecordButton.onClick.AddListener(ToggleRecording);
                }
            });

#if ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
            VideoRecorderRuntimeSettings settings = new(enableMicrophone);

            ScreenRecorderBuilder builder = ScreenRecorderBuilder.CreateVideoRecorder(settings);

            recorder = builder.Build();
            recorder.SetOnRecordingAvailable((result) =>
            {
                string path = result.Data as string;
                Debug.Log($"Recorder File: {path}");
                recorder.SaveRecording((result, error) =>
                {
                    if (error == null)
                        Debug.Log("Saved recording succesfully: " + result.Path);
                    else
                    {
#if UNITY_EDITOR
                        Debug.LogWarning("Cannot save recording in editor. Please test on a real device. Error: " + error);
#else
                        Debug.LogError($"Failed saving recording. Error: {error}");
#endif
                    }
                });
            });

            ARKitCameraSubsystem cameraSubsystem;
            AdvancedConfigurationSupported(out cameraSubsystem);

            FakeRecordAndStopToEnablePrompt();
#endif
            Debug.LogWarning("VoxelBusters Screen Recorder Kit is not enabled. Please add the plugin to enable video recording features.", Instance);
#endif
        }

#if UNITY_IOS && ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
        private async void FakeRecordAndStopToEnablePrompt()
        {
            if (recorder != null && recorder.CanRecord())
            {
                recorder.StartRecording();

                while (!recorder.IsRecording())
                    await Task.Delay(50);

                recorder.DiscardRecording();
            }
        }
#endif

        private void OnEnable()
        {
#if UNITY_IOS
            XRNetworkManager.Instance.OnCustomNotification += HandleCustomNotification;
#endif
        }

        private void OnDisable()
        {
#if UNITY_IOS
            if (XRNetworkManager.Instance)
                XRNetworkManager.Instance.OnCustomNotification -= HandleCustomNotification;
#endif
        }

        void Update()
        {
#if UNITY_IOS && ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
            // Update recording status with recorder state each frame
            SetRecordStatus(recorder.IsRecording());
#endif
        }

        private void HandleCustomNotification(IApiNotification notification)
        {
            switch (notification.Code)
            {
                case RecordingNotificationCode:
                    ToggleRecording();
                    break;
                case OcclusionNotificationCode:
                    TogglePeopleOcclusion();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Toggles the people occlusion on or off. This is used to hide or show people in the recording.
        /// </summary>
        public void TogglePeopleOcclusion()
        {
            if (occlusionManager != null)
            {
                occlusionManager.enabled = !occlusionManager.enabled;
                Debug.Log($"Toggled OcclusionManager to: {occlusionManager.enabled}");
            }
            else
            {
                Debug.LogWarning("OcclusionManager is not set or available.");
            }
        }

        public void TogglePeopleOcclusion(bool enabled)
        {
            if (occlusionManager != null)
            {
                occlusionManager.enabled = enabled;
                Debug.Log($"Set OcclusionManager to: {enabled}");
            }
            else
            {
                Debug.LogWarning("OcclusionManager is not set or available.");
            }
        }

        /// <summary>
        /// toggles the recording to start or stop
        /// </summary>
        public void ToggleRecording()
        {
#if UNITY_IOS && ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
            if (recorder.IsRecording())
            {
                recorder.StopRecording();
            }
            else if (recorder.CanRecord())
            {
                recorder.StartRecording();
            }
#else
            SetRecordStatus(!bIsRecording);
#endif

            OnRecordingToggled?.Invoke(bIsRecording);
        }

        /// <summary>
        /// updates record button and timer based on the recording state
        /// </summary>
        /// <param name="newIsRecording"></param>
        void SetRecordStatus(bool newIsRecording)
        {
            if (bIsRecording != newIsRecording)
            {
                bIsRecording = newIsRecording;
#if UNITY_IOS
                if (iOSUIManager.Instance)
                    iOSUIManager.Instance.ToggleRecordingButton(bIsRecording);

                OnRecordingToggled?.Invoke(bIsRecording);
#endif
                if (timers != null && timers.Length > 0)
                {
                    if (bIsRecording)
                    {
                        foreach (RecordTimer timer in timers)
                            timer.StartTimer();
#if UNITY_IOS
                        if (iOSUIManager.Instance)
                            iOSUIManager.Instance.ExtraHideUI();
#endif
                    }
                    else
                        foreach (RecordTimer timer in timers)
                            timer.StopTimer();
                }
            }
        }

#if UNITY_IOS
        /// <summary>
        /// Checks if device supports advanced camera configuration to allow adjusting camera settings.
        /// iPads can't do this...
        /// </summary>
        /// <param name="subsystem"></param>
        /// <returns></returns>
        bool AdvancedConfigurationSupported(out ARKitCameraSubsystem subsystem)
        {
            // This is inefficient. You should re-use a saved reference instead.
            var cameraManager = FindAnyObjectByType<ARCameraManager>();

            // check if arkit subsystem is available
            subsystem = cameraManager.subsystem as ARKitCameraSubsystem;
            if (subsystem == null)
            {
                Debug.LogError("Advanced camera configuration requires ARKit.");
                return false;
            }

            // check whether the device supports advanced camera configuration
            if (!subsystem.advancedCameraConfigurationSupported)
            {
                Debug.LogError("Advanced camera configuration is not supported on this device.");
                return false;
            }

            return true;
        }
#endif
    }
}