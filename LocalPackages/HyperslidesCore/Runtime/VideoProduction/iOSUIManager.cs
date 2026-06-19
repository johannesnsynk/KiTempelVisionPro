using System;
using NSYNK.HyperSlides;
using NSYNK.HyperSlides.Core;
using NSYNK.HyperSlides.Input;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSYNK.HyperSlides.UI
{
    public class iOSUIManager : Singleton<iOSUIManager>
    {
        public GameObject UIParent;
        public GameObject iOSModeratorUI;
        public Button RecordButton;
        public Toggle HideOnRecToggle, UseMicToggle;

#if UNITY_IOS
        private bool bHideOnRec = true;
        private const string HideOnKey = "HS_HideOn";
        private const string UseMicKey = "HS_UseMic";

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            UIParent.SetActive(false);
        }

        private void OnEnable()
        {
            HyperSlidesStateManager.Instance.OnStateUpdate += OnAppStateChanged;

            XRNetworkManager.Instance.OnMatchJoined += LoadModeratorUI;
            XRNetworkManager.Instance.OnMatchLeft += HideModeratorUI;

            XRInputManager.Instance.OnTouchUpdate += HandleTouchUpdate;
        }

        private void OnDisable()
        {
            if (!HyperSlidesStateManager.Instance)
                return;

            HyperSlidesStateManager.Instance.OnStateUpdate -= OnAppStateChanged;

            XRNetworkManager.Instance.OnMatchJoined -= LoadModeratorUI;
            XRNetworkManager.Instance.OnMatchLeft -= HideModeratorUI;

            XRInputManager.Instance.OnTouchUpdate -= HandleTouchUpdate;
        }

        /// <summary>
        /// Handles touch updates to toggle the visibility of the UI parent.
        /// </summary>
        /// <param name="touchPhase"></param>
        private void HandleTouchUpdate(UnityEngine.InputSystem.TouchPhase touchPhase)
        {
            if (touchPhase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                if (EventSystem.current != null && !XRInputManager.Instance.IsPointerOverUI() && !UIParent.activeSelf)
                    ToggleUIParent();
            }
        }

        private void Start()
        {
            SetHideOnRec(PlayerPrefs.GetInt(HideOnKey, bHideOnRec ? 1 : 0) == 1);
            ToggleUseMicrophone(PlayerPrefs.GetInt(UseMicKey, HyperslidesAudioManager.Instance.UseMic ? 1 : 0) == 1);

            if (HideOnRecToggle)
            {
                HideOnRecToggle.isOn = bHideOnRec;
                HideOnRecToggle.onValueChanged.AddListener(SetHideOnRec);
            }
            else
                Debug.LogError("ToggleHideOnRec isn't set");

            if (UseMicToggle)
            {
                UseMicToggle.isOn = HyperslidesAudioManager.Instance.UseMic;
                UseMicToggle.onValueChanged.AddListener(ToggleUseMicrophone);
            }
            else
                Debug.LogError("ToggleUseMic isn't set");
        }

        private void Update()
        {
#if !ENABLE_VOXELBUSTERS_SCREEN_RECORDER_KIT
            ToggleRecordingButtonVisibility(false);
#else
            ToggleRecordingButtonVisibility(true);
#endif
        }

        private void OnAppStateChanged(HyperSlidesStateManager.AppState appState)
        {
            switch (appState)
            {
                case HyperSlidesStateManager.AppState.STARTING:
                    ShowUIParent();
                    break;
                default:
                    break;
            }
        }

        private void LoadModeratorUI() => ToggleModeratorUI(DeviceInfo.Instance.Role == XRPlayer.Role.Moderator);
        private void HideModeratorUI() => ToggleModeratorUI(false);

        private void ToggleModeratorUI(bool showUI)
        {
            iOSModeratorUI.SetActive(showUI);
        }

        public void ToggleRecordingButtonVisibility(bool canRecord)
        {
            RecordButton.gameObject.SetActive(canRecord);
            HideOnRecToggle.gameObject.SetActive(canRecord);
        }

        public void ToggleRecordingButton(bool isRecording) => RecordButton.image.color = isRecording ? Color.red : Color.green;

        public void ToggleUseMicrophone(bool useMic)
        {
            HyperslidesAudioManager.Instance.UseMic = useMic;
            PlayerPrefs.SetInt(UseMicKey, HyperslidesAudioManager.Instance.UseMic ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Sets whether the UI should be hidden when recording starts, and saves this preference to PlayerPrefs for persistence across sessions.
        /// </summary>
        /// <param name="newHideOn"></param>
        public void SetHideOnRec(bool newHideOn)
        {
            bHideOnRec = newHideOn;
            PlayerPrefs.SetInt(HideOnKey, bHideOnRec ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Extra method to hide the UI, can be called from other scripts when recording starts or stops
        /// </summary>
        /// <param name="touchPhase"></param>
        public void ExtraHideUI(UnityEngine.InputSystem.TouchPhase touchPhase = UnityEngine.InputSystem.TouchPhase.None)
        {
            if (bHideOnRec)
                HideUIParent();
        }

        /// <summary>
        /// Shows the UI parent GameObject, allowing it to be visible in the scene
        /// </summary>
        public void ShowUIParent() => UIParent.SetActive(true);
        /// <summary>
        /// Hides the UI parent GameObject, making it invisible in the scene
        /// </summary>
        public void HideUIParent() => UIParent.SetActive(false);
        /// <summary>
        /// Toggles the active state of the UI parent gameObject
        /// </summary>
        public void ToggleUIParent() => UIParent.SetActive(!UIParent.activeSelf);
#else
        //Fallback for non-iOS platforms - these methods won't do anything but allow the code to compile without errors
        public void ToggleRecordingButton(bool isRecording) { }
#endif
    }
}