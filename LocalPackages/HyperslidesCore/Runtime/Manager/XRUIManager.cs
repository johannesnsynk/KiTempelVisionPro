using NSYNK.HyperSlides.XR;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Runtime;

using System.Threading.Tasks;
using System.Timers;

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// Handles the UI updates like switching between client and moderator and other events.
    /// TODO implement events and methods to handle different states
    /// </summary>
    public class XRUIManager : Singleton<XRUIManager>
    {
        public static Timer timer = new();
        public static Timer dissolveTimer = new();
        public Transform palmUIRoot;
        public Transform headUIRoot;
        public Transform stationaryUIRoot;
        public Material uiMaterial;

        public GameObject setupUI;
        public GameObject matchMakerUI;
        public GameObject moderatorUI;
        public GameObject moderatorHeadUI;
        public GameObject popUPUI;
        public GameObject moderatorNotesUI;
        public GameObject participantUI;

        private GameObject setupUIRuntime;
        private GameObject matchMakerUIRuntime;
        private GameObject moderatorUIRuntime;
        private GameObject moderatorHeadUIRuntime;
        private GameObject popUPUIRuntime;
        private GameObject moderatorNotesUIRuntime;
        private GameObject participantUIRuntime;

        private Dictionary<GameObject, bool> uiLastState = new Dictionary<GameObject, bool>();

        private bool loadingActionInProgress = false;
        private float loadingActionProgress = 0;

        public bool LoadingActionInProgress
        {
            get => loadingActionInProgress;
            set
            {
                loadingActionInProgress = value;
            }
        }
        public float LoadingActionProgress
        {
            get => loadingActionProgress;
            set
            {
                loadingActionProgress = value;
                UIButton.onUpdateUI?.Invoke();
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            stationaryUIRoot.transform.localPosition = new Vector3(0, 0, 1);
#endif
        }

        /// <summary>
        /// Hide all UI elements and store their latest state to avoid reactivating them when already disabled
        /// </summary>
        public void HideAll()
        {
            StoreObjectAndDeactivate(setupUIRuntime);
            StoreObjectAndDeactivate(matchMakerUIRuntime);
            StoreObjectAndDeactivate(moderatorUIRuntime);
            StoreObjectAndDeactivate(moderatorHeadUIRuntime);
            StoreObjectAndDeactivate(popUPUIRuntime);
            StoreObjectAndDeactivate(participantUIRuntime);
            StoreObjectAndDeactivate(moderatorNotesUIRuntime);
        }

        /// <summary>
        /// Restore last UI element state from <see cref="uiLastState"/>
        /// </summary>
        public void RestoreLastUIState()
        {
            ResetObjectToLastState(setupUIRuntime);
            ResetObjectToLastState(matchMakerUIRuntime);
            ResetObjectToLastState(moderatorUIRuntime);
            ResetObjectToLastState(moderatorHeadUIRuntime);
            ResetObjectToLastState(popUPUIRuntime);
            ResetObjectToLastState(participantUIRuntime);
            ResetObjectToLastState(moderatorNotesUIRuntime);
        }

        /// <summary>
        /// Store the current state of the UI element
        /// </summary>
        /// <param name="uiObject">The gameobject to save its enabled state</param>
        private void StoreObjectAndDeactivate(GameObject uiObject)
        {
            if (!uiObject)
                return;

            if (uiLastState.ContainsKey(uiObject))
                uiLastState[uiObject] = uiObject.activeInHierarchy;
            else
                uiLastState.TryAdd(uiObject, uiObject.activeInHierarchy);

            uiObject.SetActive(false);
        }

        /// <summary>
        /// Reset to latest state from the list
        /// </summary>
        /// <param name="uiObject">The gameobject to read its last state</param>
        private void ResetObjectToLastState(GameObject uiObject)
        {
            if (!uiObject)
                return;

            if (uiLastState.TryGetValue(uiObject, out bool wasActive))
                uiObject.SetActive(wasActive);
        }

        /// <summary>
        /// Init the anchor setup screen if supported
        /// </summary>
        public void InitAnchorSetup()
        {
            HideAll();

            if (!setupUIRuntime)
                setupUIRuntime = Instantiate(setupUI, stationaryUIRoot, false);
            else
                setupUIRuntime.SetActive(true);
        }

        /// <summary>
        /// Init the matchmaking UI when the device setup is done
        /// </summary>
        public void InitMatchMakingUI()
        {
            HideAll();

            palmUIRoot.gameObject.SetActive(true);
            headUIRoot.gameObject.SetActive(true);

            if (!matchMakerUIRuntime)
                matchMakerUIRuntime = Instantiate(matchMakerUI, stationaryUIRoot, false);
            else
                matchMakerUIRuntime.SetActive(true);

            if (!setupUIRuntime)
                setupUIRuntime = Instantiate(setupUI, stationaryUIRoot, false);

            setupUIRuntime.SetActive(false);

            //Maybe use this later for always on top materials
            //matchMakerUIRuntime.GetComponentsInChildren<UnityEngine.UI.ILayoutElement>().ToList().ForEach(element =>
            //{
            //if (element.GetType().GetProperty("material") != null)
            //    element.GetType().GetProperty("material").SetValue(element, uiMaterial);
            //});

            matchMakerUIRuntime.GetComponent<Canvas>().worldCamera = Camera.main;
        }

        /// <summary>
        /// load the current user UI like matchmaking (when not in a session) and moderator UI when in a session and the correct role
        /// </summary>
        /// <param name="inMatch">Is the user in a match/session?</param>
        public void LoadUserUI(bool inMatch = false)
        {
            HideAll();

            if (!inMatch)
            {
                InitMatchMakingUI();
                return;
            }

            headUIRoot.gameObject.SetActive(false);
            palmUIRoot.gameObject.SetActive(false);

            if (DeviceInfo.Role != XRPlayer.Role.Moderator)
                return;

            if (!moderatorUIRuntime)
            {
                if (DeviceInfo.IsXRDevice())
                {
                    moderatorUIRuntime = Instantiate(moderatorUI, palmUIRoot, false);

                    //Divide by 1000 because Unity UI
                    moderatorUIRuntime.transform.localScale = Vector3.one / 1000 * RuntimeHandler.Settings.uiHandScale;
                }
                else
                    moderatorUIRuntime = Instantiate(moderatorUI, headUIRoot, false);

                if (moderatorUIRuntime.TryGetComponent(out Canvas canvas))
                    canvas.worldCamera = Camera.main;
            }

            if (!moderatorHeadUIRuntime)
            {
                moderatorHeadUIRuntime = Instantiate(moderatorHeadUI, headUIRoot, false);

                if (moderatorHeadUIRuntime.TryGetComponent(out Canvas canvas))
                    canvas.worldCamera = Camera.main;
            }

            moderatorUIRuntime.SetActive(true);
            moderatorHeadUIRuntime.SetActive(true);

            ShowModeratorNotesUI();
        }

        /// <summary>
        /// Show the moderator notes when in a match
        /// </summary>
        public void ShowModeratorNotesUI()
        {
            if (Instance.moderatorNotesUIRuntime == null)
            {
                var newPosition = XRTrackedUser.Instance.Rotation() * RuntimeHandler.Settings.uiPresenterNotesOffset;
                Vector3 moderatorNotesPosition = XRTrackedUser.Instance.Position() + newPosition;

                var target = moderatorNotesPosition - XRTrackedUser.Instance.Position();
                Quaternion moderatorNotesRotation = Quaternion.LookRotation(target, Vector3.up);

                Instance.moderatorNotesUIRuntime = Instantiate(Instance.moderatorNotesUI, moderatorNotesPosition, moderatorNotesRotation);

                if (moderatorNotesUIRuntime.TryGetComponent(out Canvas canvas))
                    canvas.worldCamera = Camera.main;
            }
            else
            {
                moderatorNotesUIRuntime.SetActive(true);

                var newPosition = XRTrackedUser.Instance.Rotation() * RuntimeHandler.Settings.uiPresenterNotesOffset;
                Instance.moderatorNotesUIRuntime.transform.position = XRTrackedUser.Instance.Position() + newPosition;

                var target = Instance.moderatorNotesUIRuntime.transform.position - XRTrackedUser.Instance.Position();
                Instance.moderatorNotesUIRuntime.transform.rotation = Quaternion.LookRotation(target, Vector3.up);
            }
        }

        /// <summary>
        /// Hide/destroy the moderator notes when leaving matches or closing the notes panel
        /// </summary>
        public void HideModeratorNotesUI()
        {
            if (Instance.moderatorNotesUIRuntime != null)
                Destroy(Instance.moderatorNotesUIRuntime.gameObject);
            Instance.moderatorNotesUIRuntime = null;
        }

        //private void Update()
        //{
        //    uiRoot.position = Vector3.Lerp(uiRoot.position, XRTrackedUser.instance.Position() + XRTrackedUser.instance.Forward() * 2, Time.deltaTime * RuntimeHandler.Settings.uiFollowEasing);
        //    uiRoot.rotation = Quaternion.Lerp(uiRoot.rotation, XRTrackedUser.instance.Rotation(), Time.deltaTime * RuntimeHandler.Settings.uiFollowEasing);
        //}

        public void ShowParticipantUI() => HandleParticipantUI(true);
        public void HideParticipantUI() => HandleParticipantUI(false);

        /// <summary>
        /// Show or hide a custom participant UI triggered from backend when needed
        /// </summary>
        /// <param name="show">Show the panel?</param>
        private void HandleParticipantUI(bool show)
        {
            if (XRNetworkManager.localPlayer && DeviceInfo.Role == XRPlayer.Role.Moderator)
            {
                ShowModeratorNotesUI();
                return;
            }

            if (!participantUIRuntime)
                // if (RuntimeHandler.isRealDevice)
                //     participantUIRuntime = Instantiate(participantUI, palmUIRoot, false);
                // else
                participantUIRuntime = Instantiate(participantUI, headUIRoot, false);

            participantUIRuntime.SetActive(show);
        }

        /// <summary>
        /// Request a confirmation from the user, to avoid accidentally triggering methods like leaving a match or resetting anchor setup
        /// </summary>
        /// <param name="message">The message prompt to be shown while waiting for user input</param>
        /// <returns>Returns true, if the user aknowledges the prompt or false if not</returns>
        public static async Task<bool> RequestConfirmation(string message)
        {
            Instance.HideAll();

#if UNITY_VISIONOS
            if (Instance.popUPUIRuntime == null)
                Instance.popUPUIRuntime = Instantiate(Instance.popUPUI, Instance.stationaryUIRoot);
#else
            if (Instance.popUPUIRuntime == null)
                Instance.popUPUIRuntime = Instantiate(Instance.popUPUI, Instance.headUIRoot);

#endif
            Instance.popUPUIRuntime.SetActive(true);

            Instance.popUPUIRuntime.transform.localPosition = new Vector3(0, 0, -0.25f);

            EventSystem.current.UpdateModules();

            Task<bool> waitForUser = UIPopUp.Instance.WaitForUserInput(message);
            await waitForUser;

            Instance.popUPUIRuntime.SetActive(false);

            Instance.RestoreLastUIState();

            return waitForUser.Result;
        }

        /// <summary>
        /// Show the moderator UI
        /// </summary>
        public void ShowModeratorUI()
        {
            palmUIRoot.gameObject.SetActive(true);
            headUIRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the moderator UI
        /// </summary>
        public void HideModeratorUI()
        {
            palmUIRoot.gameObject.SetActive(false);
            headUIRoot.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (XRHandPalm.Left && DeviceInfo.IsXRDevice())
                MoveUIToHand();

            MoveUIToUserHead();
#if UNITY_IOS || UNITY_STANDALONE || UNITY_EDITOR
            MoveStationaryToUserFront();
#endif
            // if (XRNetworkManager.match == null)
            // {
            //     MoveUIToUserHead();
            //     return;
            // }
            // else
            // {
            //     if(XRHandPalm.instance && RuntimeHandler.isRealDevice)
            //         MoveUIToHand();
            //     else
            //         MoveUIToUserHead();
            // }
        }

        private void MoveStationaryToUserFront()
        {
            Vector3 targetPos = XRTrackedUser.Instance.Position() + XRTrackedUser.Instance.Forward() * 2;
            targetPos.y = XRTrackedUser.Instance.Position().y;

            Vector3 lookPos = XRTrackedUser.Instance.Position() - targetPos;
            lookPos.y = 0;

            Instance.stationaryUIRoot.position = targetPos;
            Instance.stationaryUIRoot.rotation = Quaternion.LookRotation(lookPos);
            Instance.stationaryUIRoot.Rotate(0, 180, 0);
        }

        /// <summary>
        /// Move UI in front of the user camera position with <see cref="Settings.uiHeadDistance"/> distance
        /// </summary>
        private void MoveUIToUserHead()
        {
            if (!headUIRoot.gameObject.activeInHierarchy)
                headUIRoot.gameObject.SetActive(true);

            headUIRoot.localScale = Vector3.one * RuntimeHandler.Settings.uiHeadScale;

            //headUIRoot.position = Vector3.Lerp(headUIRoot.position, XRTrackedUser.Instance.Position() + XRTrackedUser.Instance.Forward() * RuntimeHandler.Settings.uiHeadDistance, Time.deltaTime * RuntimeHandler.Settings.uiFollowEasing);
            //headUIRoot.rotation = Quaternion.Lerp(headUIRoot.rotation, XRTrackedUser.Instance.Rotation(), Time.deltaTime * RuntimeHandler.Settings.uiRotateEasing);

            headUIRoot.SetPositionAndRotation(XRTrackedUser.Instance.Position() + XRTrackedUser.Instance.Forward() * RuntimeHandler.Settings.uiHeadDistance, XRTrackedUser.Instance.Rotation());
        }

        /// <summary>
        /// Move the UI towards the hand palm based on the <see cref="XRHandPalm"/> position and rotation
        /// </summary>
        private void MoveUIToHand()
        {
            if (moderatorUIRuntime)
            {
                Vector3 targetPos = new Vector3(RuntimeHandler.Settings.uiHandOffset.x, RuntimeHandler.Settings.uiHandOffset.y, RuntimeHandler.Settings.uiHandOffset.z);

                moderatorUIRuntime.transform.localPosition =
                Vector3.Lerp(
                    moderatorUIRuntime.transform.localPosition,
                    targetPos,
                    Time.deltaTime * RuntimeHandler.Settings.uiFollowEasing
                    );

                moderatorUIRuntime.transform.LookAt(XRTrackedUser.Instance.Position());
                moderatorUIRuntime.transform.Rotate(0, 180, 0, Space.Self);
            }

            palmUIRoot.transform.position =
            Vector3.Lerp(
                palmUIRoot.transform.position,
                XRHandPalm.Left.transform.position,
                Time.deltaTime * RuntimeHandler.Settings.uiFollowEasing
                );
        }

        /// <summary>
        /// Restart and check the timer to block slide change calls
        /// </summary>
        /// <returns></returns>
        public static void StartTimer(Timer currentTimer, float timeout = 0)
        {
            if (!currentTimer.Enabled || currentTimer.Interval < timeout * 1000)
            {
                currentTimer.Stop();
                currentTimer.Interval = (timeout > 0 ? timeout : RuntimeHandler.Settings.uiTimeout) * 1000;
                currentTimer.Start();
                currentTimer.AutoReset = false;
                currentTimer.Enabled = true;
            }
        }

        public static bool TimerRunning()
        {
            return timer.Enabled || dissolveTimer.Enabled;
        }
    }
}