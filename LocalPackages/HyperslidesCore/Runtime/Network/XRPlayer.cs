using Nakama;

using NSYNK.HyperSlides.Runtime;
using NSYNK.HyperSlides.XR;

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
        public enum Role {
            Inherit = -1,
            Broadcast = 0,
            Simulation = 1,
            Participant = 2,
            Moderator = 3,
            Admin = 4
        };

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

        private void OnEnable()
        {
            XRNetworkManager.onNetworkPlayerUpdate += UpdatePlayer;
            XRNetworkManager.onNetworkModeratorUpdate += UpdateModerator;
            RuntimeHandler.tick += OnEditMode;
        }

        private void OnDisable()
        {
            XRNetworkManager.onNetworkPlayerUpdate -= UpdatePlayer;
            XRNetworkManager.onNetworkModeratorUpdate -= UpdateModerator;
            RuntimeHandler.tick -= OnEditMode;
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
            if(nameTag)
                nameTag.gameObject.SetActive(RuntimeHandler.Settings.showNameTags);

            if (xrPlayer.UserId != UserId || role == Role.Simulation || role == Role.Broadcast)
                return;

            if (nameTag)
                nameTag.SetText(Username);

            localPosition = xrPlayer.position;
            localRotation = Quaternion.Euler(xrPlayer.rotation);
        }

        /// <summary>
        /// Update the moderator including pointer
        /// </summary>
        /// <param name="xrModerator"></param>
        public void UpdateModerator(XRNetworkObjects.XRModerator xrModerator)
        {
            UpdatePlayer(xrModerator);

            if (xrModerator.leftPointer != null)
                UpdatePointerPosition(xrModerator.leftPointer);

            if (xrModerator.rightPointer != null)
                UpdatePointerPosition(xrModerator.rightPointer);
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

            xrHandLeft.transform.SetParent(transform.parent, XRContentRoot.Instance);
            xrHandRight.transform.SetParent(transform.parent, XRContentRoot.Instance);

            xrPointerLeft.transform.SetParent(XRContentRoot.Instance.transform);
            xrPointerRight.transform.SetParent(XRContentRoot.Instance.transform);

            if (localPlayer)
            {
                if (role == Role.Moderator)
                    EnableHandSystem();
                else
                    DisableHandSystem();
            }

            //Show the avatar prefab, if the role is equal or above the settings role
            if (!localPlayer
                && RuntimeHandler.Settings.avatarPrefab
                && role >= RuntimeHandler.Settings.avatarVisibilityRole)
                Instantiate(RuntimeHandler.Settings.avatarPrefab, head);
        }

        /// <summary>
        /// Update the pointer position and rotation baesd on the networkobject
        /// </summary>
        /// <param name="networkObject">The networkobject with all relevant values</param>
        private void UpdatePointerPosition(XRNetworkObjects.XRPointer networkObject)
        {
            if(UserId == networkObject.UserId && !localPlayer)
            {
                if (role == Role.Moderator && RuntimeHandler.Settings.ShowModeratorPointer)
                {
                    if (networkObject.handedness == Handedness.Left)
                    {
                        xrPointerLeft.gameObject.SetActive(networkObject.visible);
                        xrPointerLeftPos = networkObject.position;
                        xrPointerLeftRot = Quaternion.Euler(networkObject.rotation);
                    }
                    if (networkObject.handedness == Handedness.Right)
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

            transform.localPosition = Vector3.Lerp(transform.localPosition, localPosition, Time.deltaTime * RuntimeHandler.Settings.playerPositionEasing);
            transform.localRotation = Quaternion.LerpUnclamped(transform.localRotation, Quaternion.Euler(0, localRotation.eulerAngles.y, 0), Time.deltaTime * RuntimeHandler.Settings.playerRotationEasing);

            if (head)
                head.localRotation = Quaternion.LerpUnclamped(head.localRotation, Quaternion.Euler(localRotation.eulerAngles.x, 0, 0), Time.deltaTime * RuntimeHandler.Settings.playerHeadRotationEasing);
        }

        private void HidePointerAfterTime()
        {
            pointerHideTimer = RuntimeHandler.Settings.pointerHideTimer;
        }

        /// <summary>
        /// Update all pointer or position if spectator
        /// </summary>
        private void Update()
        {
            //Use spectator specific position and rotation code without any pointer logic
            if(role == Role.Simulation || role == Role.Broadcast)
                return;

            //Only process if not spectator and all inspector values have been set
            if (!localPlayer && RuntimeHandler.Settings.ShowModeratorPointer)
            {
                if(xrPointerLeft.gameObject.activeInHierarchy)
                    LerpLocalPointerPosition(xrPointerLeft.transform, xrPointerLeftPos, xrPointerLeftRot, RuntimeHandler.Settings.pointerEasing);

                if (xrPointerRight.gameObject.activeInHierarchy)
                    LerpLocalPointerPosition(xrPointerRight.transform, xrPointerRightPos, xrPointerRightRot, RuntimeHandler.Settings.pointerEasing);

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
                if (role != Role.Moderator || !RuntimeHandler.Settings.ShowModeratorPointer)
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
                    Time.deltaTime * RuntimeHandler.Settings.localPointerEasing +
                    Time.deltaTime * RuntimeHandler.Settings.localPointerEasing *
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
            target.localPosition = Vector3.Lerp(target.localPosition, pos, Time.deltaTime * speed);
            target.localRotation = Quaternion.Lerp(target.localRotation, rot, Time.deltaTime * speed);
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
                handProcessor.leftHandSmoothingFactor = RuntimeHandler.Settings.handSmoothing;
                handProcessor.rightHandSmoothingFactor = RuntimeHandler.Settings.handSmoothing;
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

            if(xrPointerLeft)
                Destroy(xrPointerLeft.gameObject);

            if(xrPointerRight)
                Destroy(xrPointerRight.gameObject);

            if(xrHandLeft)
                Destroy(xrHandLeft.gameObject);

            if(xrHandRight)
                Destroy(xrHandRight.gameObject);
        }

        private void OnDestroy()
        {
            //Debug.Log("Destroy player: " + UserId, this);
        }

        public void OnEditMode()
        {
            if (!head)
                return;

            if (RuntimeHandler.Settings.editModeAvatarPrefab)
            {
                if (RuntimeHandler.editModeEnabled && head.childCount == 0)
                    Instantiate(RuntimeHandler.Settings.editModeAvatarPrefab, head, false);
                else if (!RuntimeHandler.editModeEnabled)
                {
                    foreach (Transform child in head)
                        Destroy(child.gameObject);
                }
            }
        }
    }
}