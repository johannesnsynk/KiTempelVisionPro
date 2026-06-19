using Nakama;

using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.XR;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace NSYNK.HyperSlides.Network
{
    /// <summary>
    /// The player class holding all necessary values to sync
    /// </summary>
    [System.Serializable]
    public class XRPlayer : MonoBehaviour
    {
        public string UserId, Username;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public enum Role
        {
            Inherit = -1,
            Broadcast = 0,
            Simulation = 1,
            Participant = 2,
            Moderator = 3,
            Admin = 4
        };

        public enum FilterMethod
        {
            None = 0,
            Lerp = 1,
            Kalman = 2,
            KalmanQueue = 3,
            OneEuro = 4
        }

        public FilterMethod filterMethod = FilterMethod.OneEuro;

        /// <summary>
        /// Kalman filter used to smooth the position updates over time.
        /// </summary>
        private KalmanFilter positionKalmanFilter = new KalmanFilter(0.1f, 0.1f, Vector3.zero, Vector3.one);
        private KalmanFilter leftPointerPositionKalmanFilter = new KalmanFilter(0.1f, 0.1f, Vector3.zero, Vector3.one);
        private KalmanFilter rightPointerPositionKalmanFilter = new KalmanFilter(0.1f, 0.1f, Vector3.zero, Vector3.one);
        /// <summary>
        /// Kalman filter used to smooth the rotation updates over time.
        /// </summary>
        private QuaternionKalmanFilter quaternionKalmanFilter = new QuaternionKalmanFilter(0.1f, 0.1f, Quaternion.identity, 1.0f);
        private QuaternionKalmanFilter leftPointerQuaternionKalmanFilter = new QuaternionKalmanFilter(0.1f, 0.1f, Quaternion.identity, 1.0f);
        private QuaternionKalmanFilter rightPointerQuaternionKalmanFilter = new QuaternionKalmanFilter(0.1f, 0.1f, Quaternion.identity, 1.0f);

        private OneEuroFilter positionOneEuroFilter = new OneEuroFilter(60f, 1.0f, 0.0f, 1.0f);
        private OneEuroFilter leftPointerOneEuroFilter = new OneEuroFilter(60f, 1.0f, 0.0f, 1.0f);
        private OneEuroFilter rightPointerOneEuroFilter = new OneEuroFilter(60f, 1.0f, 0.0f, 1.0f);
        private OneEuroFilterQuaternion rotationOneEuroFilter = new OneEuroFilterQuaternion(60f, 1.0f, 0.0f, 1.0f);
        private OneEuroFilterQuaternion leftPointerRotationOneEuroFilter = new OneEuroFilterQuaternion(60f, 1.0f, 0.0f, 1.0f);
        private OneEuroFilterQuaternion rightPointerRotationOneEuroFilter = new OneEuroFilterQuaternion(60f, 1.0f, 0.0f, 1.0f);

        [Header("Player Runtime Objects")]
        public Role role;
        public Transform head, persona;
        public XRPointer xrPointerLeft, xrPointerRight;
        public GameObject xrHandLeft, xrHandRight;
        public Transform xrFingertipLeft, xrFingertipRight;
        public Transform xrWristLeft, xrWristRight;
        public HandProcessor handProcessor;
        public List<GameObject> pointerGestureHolder;
        public bool LocalPlayer => localPlayer;
        public TMPro.TextMeshPro nameTag;

        public List<XRNetworkObjects.NetworkSyncedTransform> syncedTransforms = new();
        public List<XRNetworkObjects.NetworkSyncedValue> syncedValues = new();

        private bool localPlayer = false;
        private Vector3 xrPointerLeftPos, xrPointerRightPos = Vector3.zero;
        private Quaternion xrPointerLeftRot, xrPointerRightRot = Quaternion.identity;
        public float pointerHideTimer = 0;
        private Vector3 yRotation = new();
        private XRHandSubsystem m_HandSubsystem;
        private DateTime lastUpdateTime = DateTime.MinValue;
        private GameObject avatarPrefabInstance;

        private void OnEnable()
        {
            XRNetworkManager.Instance.OnNetworkPlayerUpdate += UpdatePlayer;
            XRNetworkManager.Instance.OnNetworkModeratorUpdate += UpdateModerator;

            positionOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
            rotationOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
            leftPointerOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
            rightPointerOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
            leftPointerRotationOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
            rightPointerRotationOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
        }

        private void OnDisable()
        {
            if (XRNetworkManager.Instance == null)
                return;

            XRNetworkManager.Instance.OnNetworkPlayerUpdate -= UpdatePlayer;
            XRNetworkManager.Instance.OnNetworkModeratorUpdate -= UpdateModerator;

            if (RuntimeHandler.Instance == null)
                return;
        }

        /// <summary>
        /// Add or update the passed <see cref="XRNetworkObjects.NetworkSyncedTransform"/> on this player reference
        /// </summary>
        /// <param name="syncedTransform">The synced transform coming from interacting with the element</param>
        public void AddOrUpdateSyncedItem(XRNetworkObjects.NetworkSyncedTransform syncedTransform)
        {
            XRNetworkObjects.NetworkSyncedTransform foundTransform = syncedTransforms.Find(o => o.guid == syncedTransform.guid);

            if (foundTransform == null)
                syncedTransforms.Add(syncedTransform);
            else
                foundTransform.UpdateTransform(syncedTransform);
        }

        /// <summary>
        /// Add or update the passed <see cref="XRNetworkObjects.NetworkSyncedValue"/> on this player reference
        /// </summary>
        /// <param name="syncedTransform">The synced value coming from interacting with the element</param>
        public void AddOrUpdateSyncedItem(XRNetworkObjects.NetworkSyncedValue syncedValue)
        {
            XRNetworkObjects.NetworkSyncedValue foundValue = syncedValues.Find(o => o.guid == syncedValue.guid);

            if (foundValue == null)
                syncedValues.Add(syncedValue);
            else
                foundValue.UpdateValue(syncedValue);
        }

        /// <summary>
        /// Remove any object with new owner (NOT BEING USED RIGHT NOW)
        /// </summary>
        /// <param name="guid"></param>
        public void RemoveDueToNewOwner(string guid)
        {
            syncedTransforms.RemoveAll(v => v.guid == guid);
            syncedValues.RemoveAll(v => v.guid == guid);
        }

        public static Role StringToRole(string value)
        {
            if (value.Length < 2)
                return (Role)int.Parse(value);
            else
                return Role.Participant;
        }

        /// <summary>
        /// Update the value with network message values
        /// </summary>
        /// <param name="xrPlayer"></param>
        public void UpdatePlayer(XRNetworkObjects.XRPlayer xrPlayer)
        {
            if (xrPlayer.UserId != UserId || role == Role.Simulation || role == Role.Broadcast)
                return;

            localPosition = xrPlayer.position;
            localRotation = Quaternion.Euler(xrPlayer.rotation);
            lastUpdateTime = xrPlayer.timeStamp;
        }

        /// <summary>
        /// Update the moderator including pointer
        /// </summary>
        /// <param name="xrModerator"></param>
        public void UpdateModerator(XRNetworkObjects.XRPlayer xrModerator)
        {
            UpdatePlayer(xrModerator);

            if (xrModerator.LeftPointer != null)
                UpdatePointerPosition(xrModerator.LeftPointer);

            if (xrModerator.RightPointer != null)
                UpdatePointerPosition(xrModerator.RightPointer);
        }

        /// <summary>
        /// Update the player with nakama event values
        /// </summary>
        /// <param name="nakamaPlayer"></param>
        public void UpdatePlayer(IUserPresence nakamaPlayer, bool isLocalPlayer)
        {
            UserId = nakamaPlayer.UserId;
            Username = nakamaPlayer.Username;
            gameObject.name = nakamaPlayer.UserId;
            localPlayer = isLocalPlayer;
        }

        /// <summary>
        /// Update the role and instantiate different behaviours depending on the role
        /// </summary>
        /// <param name="_role"></param>
        public void UpdateRole(Role _role)
        {
            role = _role;

            if (role < Role.Participant)
                return;

            HandleHandTracking();
        }

        /// <summary>
        /// Show or hide the name tag based on the player settings and the player role, and update the text to the current username
        /// </summary>
        private void HandleNameTag()
        {
            if (!nameTag)
                return;

            nameTag.gameObject.SetActive(HyperSlidesStateManager.Instance.Settings.showNameTags && role >= Role.Participant);
            nameTag.SetText(Username);
        }

        /// <summary>
        /// Instantiate the avatar prefab if the player role is above the set visibility role in the settings, so spectators don't have any unnecessary objects in their scene
        /// </summary>
        private void HandleAvatarPrefab()
        {
            if (!HyperSlidesStateManager.Instance.Settings.avatarPrefab || localPlayer)
            {
                if (avatarPrefabInstance != null)
                {
                    Destroy(avatarPrefabInstance);
                    avatarPrefabInstance = null;
                }
                return;
            }

            if (role >= HyperSlidesStateManager.Instance.Settings.avatarVisibilityRole && avatarPrefabInstance == null)
                avatarPrefabInstance = Instantiate(HyperSlidesStateManager.Instance.Settings.avatarPrefab, head);
        }

        /// <summary>
        /// Enable the hand system and show the pointers for the moderator, disable for other roles. This is necessary to trigger the hand updates in the hand processor and show the correct pointer visibility based on the networked values
        /// </summary>
        private void HandleHandTracking()
        {
            if (role >= Role.Participant)
            {
                //Enable hand system for local player to trigger hand updates in the hand processor and show pointers for moderators
                EnableHandSystem();

                if (!xrHandLeft || !xrHandRight)
                {
                    Debug.LogError("Hand references not set for player: " + UserId, this);
                    return;
                }

                xrHandLeft.transform.SetParent(transform.parent, XRContentRoot.Instance);
                xrHandRight.transform.SetParent(transform.parent, XRContentRoot.Instance);

                // Set pointer parent for moderators
                if (role == Role.Moderator)
                {
                    if (!xrPointerLeft || !xrPointerRight)
                    {
                        Debug.LogError("Pointer references not set for player: " + UserId, this);
                        return;
                    }

                    xrPointerLeft.transform.SetParent(XRContentRoot.Instance.transform);
                    xrPointerRight.transform.SetParent(XRContentRoot.Instance.transform);
                }
            }
            else
                DisableHandSystem();
        }

        /// <summary>
        /// Update the pointer position and rotation baesd on the networkobject
        /// </summary>
        /// <param name="networkObject">The networkobject with all relevant values</param>
        private void UpdatePointerPosition(XRNetworkObjects.XRPointer networkObject)
        {
            if (UserId == networkObject.UserId && !localPlayer)
            {
                if (role == Role.Moderator && HyperSlidesStateManager.Instance.Settings.ShowModeratorPointer)
                {
                    if (networkObject.handedness == Handedness.Left.ToString())
                    {
                        xrPointerLeft.gameObject.SetActive(networkObject.visible);
                        xrPointerLeftPos = networkObject.position;
                        xrPointerLeftRot = Quaternion.Euler(networkObject.rotation);
                    }
                    if (networkObject.handedness == Handedness.Right.ToString())
                    {
                        xrPointerRight.gameObject.SetActive(networkObject.visible);
                        xrPointerRightPos = networkObject.position;
                        xrPointerRightRot = Quaternion.Euler(networkObject.rotation);
                    }
                }
                else
                {
                    xrPointerLeft.gameObject.SetActive(false);
                    xrPointerRight.gameObject.SetActive(false);
                }

                HidePointerAfterTime();
            }
        }

        /// <summary>
        /// Update the player transform based on all presences within a match from <see cref="XRNetworkManager.Update"/>
        /// </summary>
        public void UpdatePlayerTransform()
        {
            if (localPlayer)
                return;

            yRotation = localRotation.eulerAngles;
            yRotation.x = 0;
            yRotation.z = 0;

            switch (filterMethod)
            {
                case FilterMethod.None:
                    transform.localPosition = localPosition;
                    transform.localRotation = Quaternion.Euler(0, localRotation.eulerAngles.y, 0);
                    if (head)
                        head.localRotation = Quaternion.Euler(localRotation.eulerAngles.x, 0, 0);
                    break;
                case FilterMethod.Lerp:
                    transform.localPosition = Vector3.Lerp(transform.localPosition, localPosition, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.playerPositionEasing);
                    transform.localRotation = Quaternion.LerpUnclamped(transform.localRotation, Quaternion.Euler(0, localRotation.eulerAngles.y, 0), Time.deltaTime * HyperSlidesStateManager.Instance.Settings.playerRotationEasing);

                    if (head)
                        head.localRotation = Quaternion.LerpUnclamped(head.localRotation, Quaternion.Euler(localRotation.eulerAngles.x, 0, 0), Time.deltaTime * HyperSlidesStateManager.Instance.Settings.playerHeadRotationEasing);
                    break;
                case FilterMethod.Kalman:
                    //Kalman filtering not implemented for player transform yet
                    transform.localPosition = positionKalmanFilter.Update(localPosition);
                    Quaternion tempRotation = quaternionKalmanFilter.Update(localRotation);
                    transform.localRotation = Quaternion.Euler(0, tempRotation.eulerAngles.y, 0);
                    if (head)
                        head.localRotation = Quaternion.Euler(tempRotation.eulerAngles.x, 0, 0);
                    break;
                case FilterMethod.KalmanQueue:
                    //Kalman queue filtering not implemented for player transform yet
                    Debug.Log("Kalman queue filtering not implemented for player transform yet");
                    break;
                case FilterMethod.OneEuro:
                    //OneEuro filtering not implemented for player transform yet
                    transform.localPosition = positionOneEuroFilter.Update(localPosition);
                    Quaternion tempOneEuroRotation = rotationOneEuroFilter.Update(localRotation);
                    transform.localRotation = Quaternion.Euler(0, tempOneEuroRotation.eulerAngles.y, 0);
                    if (head)
                        head.localRotation = Quaternion.Euler(tempOneEuroRotation.eulerAngles.x, 0, 0);
                    break;
            }


        }

        private void HidePointerAfterTime()
        {
            pointerHideTimer = HyperSlidesStateManager.Instance.Settings.pointerHideTimer;
        }

        /// <summary>
        /// Update all pointer or position if spectator
        /// </summary>
        private void Update()
        {
            HandleNameTag();
            HandleAvatarPrefab();

            //Use spectator specific position and rotation code without any pointer logic
            if (role == Role.Simulation || role == Role.Broadcast || role == Role.Admin)
                return;

            //Only process if not spectator and all inspector values have been set
            if (!localPlayer && HyperSlidesStateManager.Instance.Settings.ShowModeratorPointer)
            {
                if (xrPointerLeft.gameObject.activeInHierarchy)
                    LerpLocalPointerPosition(xrPointerLeft.transform, xrPointerLeftPos, xrPointerLeftRot, HyperSlidesStateManager.Instance.Settings.pointerEasing);

                if (xrPointerRight.gameObject.activeInHierarchy)
                    LerpLocalPointerPosition(xrPointerRight.transform, xrPointerRightPos, xrPointerRightRot, HyperSlidesStateManager.Instance.Settings.pointerEasing);

                pointerHideTimer -= Time.deltaTime;

                if (pointerHideTimer <= 0)
                {
                    xrPointerRight.gameObject.SetActive(false);
                    xrPointerLeft.gameObject.SetActive(false);
                }
            }
            else
            {
                //LerpPointerPosition(xrPointerLeft.transform, xrFingertipLeft.position, xrFingertipLeft.rotation, Space.World, easingSpeed);
                //LerpPointerPositionAndRotation(xrPointerRight.transform, xrFingertipRight.position, xrFingertipRight.rotation, Space.World, easingSpeed);
                if (role != Role.Moderator || !HyperSlidesStateManager.Instance.Settings.ShowModeratorPointer)
                    return;

                if (xrPointerRight.gameObject.activeInHierarchy)
                    MovePointer(xrPointerRight, xrFingertipRight, xrWristRight, Space.World);

                if (xrPointerLeft.gameObject.activeInHierarchy)
                    MovePointer(xrPointerLeft, xrFingertipLeft, xrWristLeft, Space.World);
            }
        }

        /// <summary>
        /// Move the moderator pointer to the new position
        /// </summary>
        /// <param name="pointer">The pointer</param>
        /// <param name="fingerTip">The fingertip joint</param>
        /// <param name="wrist">The wrist joint</param>
        /// <param name="space">Unused space value (maybe needed later)</param>
        private void MovePointer(XRPointer pointer, Transform fingerTip, Transform wrist, Space space)
        {
            Vector3 averageDirection = fingerTip.forward.normalized + wrist.forward.normalized;
            averageDirection = averageDirection.normalized;

            pointer.transform.LookAt(fingerTip.position);

            pointer.transform.position = Vector3.Lerp(
                    pointer.transform.position,
                    fingerTip.position + averageDirection,
                    Time.deltaTime * HyperSlidesStateManager.Instance.Settings.localPointerEasing +
                    Time.deltaTime * HyperSlidesStateManager.Instance.Settings.localPointerEasing *
                    Vector3.Distance(pointer.transform.position, fingerTip.position + averageDirection)
                    );
        }

        /// <summary>
        /// Lerp the position and rotation for the pointer objects towards the fingertips or netobject target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="space"></param>
        private void LerpLocalPointerPosition(Transform target, Vector3 pos, Quaternion rot, float speed)
        {
            bool isLeft = target == xrPointerLeft.transform;
            switch (filterMethod)
            {
                case FilterMethod.None:
                    target.localPosition = pos;
                    target.localRotation = rot;
                    return;
                case FilterMethod.Lerp:
                    //Handled below
                    target.localPosition = Vector3.Lerp(target.localPosition, pos, Time.deltaTime * speed);
                    target.localRotation = Quaternion.Lerp(target.localRotation, rot, Time.deltaTime * speed);
                    break;
                case FilterMethod.Kalman:
                    target.localPosition = (isLeft ? leftPointerPositionKalmanFilter : rightPointerPositionKalmanFilter).Update(pos);
                    target.localRotation = (isLeft ? leftPointerQuaternionKalmanFilter : rightPointerQuaternionKalmanFilter).Update(rot);
                    return;
                case FilterMethod.KalmanQueue:
                    //Kalman queue filtering not implemented for pointer yet
                    Debug.Log("Kalman queue filtering not implemented for pointer yet");
                    return;
                case FilterMethod.OneEuro:
                    target.localPosition = (isLeft ? leftPointerOneEuroFilter : rightPointerOneEuroFilter).Update(pos);
                    target.localRotation = (isLeft ? leftPointerRotationOneEuroFilter : rightPointerRotationOneEuroFilter).Update(rot);
                    return;
            }
        }

        /// <summary>
        /// Setup the hand system on local players, so we can trigger hand updates
        /// </summary>
        private void EnableHandSystem()
        {
            var handSubsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(handSubsystems);

            for (var i = 0; i < handSubsystems.Count; ++i)
            {
                var handSubsystem = handSubsystems[i];
                if (handSubsystem.running)
                {
                    m_HandSubsystem = handSubsystem;
                    break;
                }
            }

            if (m_HandSubsystem != null)
                m_HandSubsystem.updatedHands += OnUpdatedHands;

            pointerGestureHolder.ForEach(g => g.SetActive(true));

            if (handProcessor != null)
            {
                handProcessor.leftHandSmoothingFactor = HyperSlidesStateManager.Instance.Settings.handSmoothing;
                handProcessor.rightHandSmoothingFactor = HyperSlidesStateManager.Instance.Settings.handSmoothing;
            }
        }

        void OnUpdatedHands(XRHandSubsystem subsystem,
                            XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags,
                            XRHandSubsystem.UpdateType updateType)
        {
            switch (updateType)
            {
                case XRHandSubsystem.UpdateType.Dynamic:
                    // Update game logic that uses hand data
                    break;
                case XRHandSubsystem.UpdateType.BeforeRender:
                    // Update visual objects that use hand data
                    break;
            }
        }

        /// <summary>
        /// Disable all hand gesture scripts
        /// </summary>
        private void DisableHandSystem()
        {
            pointerGestureHolder.ForEach(g => g.SetActive(false));
        }

        /// <summary>
        /// Make sure to destroy all detached objects
        /// </summary>
        public void DestroyDependencies()
        {
            //Debug.Log("Destroy player dependencies: " + UserId, this);

            if (xrPointerLeft)
                Destroy(xrPointerLeft.gameObject);

            if (xrPointerRight)
                Destroy(xrPointerRight.gameObject);

            if (xrHandLeft)
                Destroy(xrHandLeft.gameObject);

            if (xrHandRight)
                Destroy(xrHandRight.gameObject);
        }
    }
}