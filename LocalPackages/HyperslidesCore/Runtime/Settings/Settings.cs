using System;
using NSYNK.HyperSlides.Network;
using UnityEngine;
using UnityEngine.Serialization;

namespace NSYNK.HyperSlides
{
    [CreateAssetMenu(fileName = "GlobalSettings", menuName = "Hyperslides/Global Setting", order = 0)]
    public class Settings : ScriptableObject
    {
        public string backend = "https://next.hyperslides.de";
        public enum TrackingType { Free, Anchors, Image };
        public bool showNameTags = false;

        [Header("Nakama Connection")]
        public NakamaConnectionProfile NakamaProfile = new();

        [Serializable]
        public class NakamaConnectionProfile
        {
            public string IP = "nakama.hyperslides.de";
            public int Port = 443;
            public bool SSL = true;
            public string Protocol => SSL ? "https" : "http";
            public string ServerKey = "ooRB3B*7prh-dAD8Qnz*.UzTY";
        }

        [Header("Nakama Session Config")]
        public float updateRate = 5;
        public float backupUpdateRate = 5;
        public float slowUpdateRate = 0.001f;

        [Header("Local Player")]
        public float handSmoothing = 5;
        public float playerPositionEasing = 5;
        public float playerRotationEasing = 5;
        public float playerHeadRotationEasing = 5;
        public XRPlayer.Role avatarVisibilityRole = XRPlayer.Role.Moderator;
        [FormerlySerializedAs("playerHeadPrefab")]
        public GameObject avatarPrefab;
        [FormerlySerializedAs("editModePlayerPrefab")]
        public GameObject editModeAvatarPrefab;

        [Header("Moderator Pointer")]
        public bool ShowModeratorPointer = true;
        public float localPointerEasing = 20;
        public float pointerEasing = 10;
        public float pointerHideTimer = 2;
        public float pointerTransparency = 0.5f;

        [Header("General UI")]
        public float uiTimeout = 2;
        public float uiFollowEasing = 4;
        public float uiRotateEasing = 4;
        public float uiHeadScale = 0.7f;
        public float uiHeadDistance = 0.5f;

        public Vector3 uiPresenterNotesOffset = new Vector3(1.0f, -0.25f, 1.0f);
        public float uiHandScale = 1f;
        public Vector3 uiHandOffset = new Vector3(.0f, 0.2f, -.15f);

        [Header("Spectator")]
        public float spectatorPositionEasing = 10;
        public float spectatorRotationEasing = 10;

        [Header("Network Synchronisation")]
        public float syncedTransformEasing = 10;
        public float syncedValueEasing = 10;

        [Header("AR Setup")]
        public float findWorldmapTimeout = 3;
        public bool showPersonaMarkersOnLobby = true;
        [SerializeField]
        private TrackingType handheldTrackingType = TrackingType.Image;
        public TrackingType HandheldTrackingType => handheldTrackingType;
        [SerializeField]
        private TrackingType xrTrackingType = TrackingType.Anchors;
        public TrackingType trackingType
        {
#if UNITY_IOS
            get { return handheldTrackingType; }
            set { handheldTrackingType = value; }
#else
            get { return xrTrackingType; }
            set { xrTrackingType = value; }
#endif
        }
    }
}