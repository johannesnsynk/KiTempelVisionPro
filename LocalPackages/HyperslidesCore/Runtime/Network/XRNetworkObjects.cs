using MessagePack;
using Nakama;

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Hands;

namespace NSYNK.HyperSlides.Network
{
    /// <summary>
    /// A collection of simplified classes based on runtime values to reduce network message data
    /// </summary>
    [Serializable]
    public abstract class XRNetworkObjects
    {
        /// <summary>
        /// The network player data, that is going to be sent to other clients
        /// </summary>
        public static XRPlayer networkPlayer = new XRPlayer();
        public static XRPlayers networkPlayers = new XRPlayers();
        /// <summary>
        /// The network current slide data to update all clients to the same presentation and id
        /// </summary>
        public static XRSessionState networkSessionState = new XRSessionState();

        public enum MsgType
        {
            PRESENTATION_UPDATE = 10, // Presentation Update - e.g. slide change
            SLIDE_UPDATE = 11, // Notifies client about slide update during a session - e.g. moderator notes
            ROLE_UPDATE = 12, // Notifies client update Role change during a session
            SPAWN_PANEL = 13, // Notifies client about spawning a panel during a session
            NEXT_SLIDE = 14,
            PREV_SLIDE = 15,
            RESET_ANCHORS = 16, // Notifies client update metadata

            UNIFIED_TRANSFORM_UPDATE = 20, // Updates the transforms of a client - e.g. position, rotation, scale
            TRANSFORM_UPDATE = 21, // Incoming transform update from a client
            SESSION_TRANSFORM_OVERRIDE = 25, // Incoming transform update from session overrides,
            SETTING_OVERRIDE = 30, // Incoming setting override for active clients

            STOP_SESSION = 90,
            UPDATE_REJECTED = 99,
            DEBUG_STATE = 100,
        };

        [Serializable]
        public class XRSessionState
        {
            public string presentationId;
            public string presentationName;
            public int presentationContentIndex;
            public float tickRate;
            public bool showNameTags = false;
            public bool editMode = false;
        }

        [Serializable]
        public class UDPLogging
        {
            public UDPLogging(string timeStamp, MatchUpdate.MessageType status)
            {
                readableTimeStamp = timeStamp;
                Status = status;
            }

            public string readableTimeStamp = DateTime.UtcNow.ToString("o");
            public MatchUpdate.MessageType Status = MatchUpdate.MessageType.PLAYER_UPDATE;
        }

        [MessagePackObject]
        [Union(0, typeof(XRPlayer))]
        [Union(1, typeof(MatchState))]
        [Union(2, typeof(ConnectionCheck))]
        [Union(3, typeof(PlayerStates))]
        public abstract class UpdateData
        {
        }

        [Serializable, MessagePackObject]
        public class ConnectionCheck : UpdateData
        {
        }

        [Serializable, MessagePackObject]
        public class MatchUpdate
        {
            public enum MessageType
            {
                CONNECTION_CHECK,
                PLAYER_UPDATE,
                MATCH_UPDATE
            }

            [Key(0)]
            public MessageType Status = MessageType.PLAYER_UPDATE;
            [Key(1)]
            public UpdateData Data;
        }

        [Serializable, MessagePackObject]
        public class MatchState : UpdateData
        {
            [Key(0)]
            public DateTime timeStamp = DateTime.UtcNow;
            [Key(1)]
            public int SlideIndex = 0;
            [Key(2)]
            public NetworkSyncedTransform[] syncedTransforms = new NetworkSyncedTransform[0];
            [Key(3)]
            public NetworkSyncedValue[] syncedValues = new NetworkSyncedValue[0];
        }

        [Serializable, MessagePackObject]
        public class PlayerStates : UpdateData
        {
            [Key(0)]
            public DateTime timeStamp = DateTime.UtcNow;
            [Key(1)]
            public XRPlayer[] playerStates = new XRPlayer[0];
        }

        [Serializable, MessagePackObject]
        public class XRPlayer : UpdateData
        {
            public enum MessageType
            {
                JOIN,
                UPDATE,
                LEAVE
            }

            [Key(0)]
            public DateTime timeStamp = DateTime.UtcNow;
            [Key(1)]
            public string Username = "DefaultUser";
            [Key(2)]
            public string UserId = "DefaultUserId";
            [Key(3)]
            public string role = "Participant";
            [Key(4)]
            public string deviceType = "DebugUser";
            [Key(5)]
            public SerializedVector position = new(Vector3.one);
            [Key(6)]
            public SerializedVector rotation = new(Vector3.one);
            [Key(7)]
            public XRPointer LeftPointer = new();
            [Key(8)]
            public XRPointer RightPointer = new();
            [Key(9)]
            public MessageType Status = MessageType.UPDATE;
            [IgnoreMember]
            public NetworkSyncedTransform[] syncedTransforms = new NetworkSyncedTransform[0];
            [IgnoreMember]
            public NetworkSyncedValue[] syncedValues = new NetworkSyncedValue[0];
        }

        [Serializable, MessagePackObject]
        public class XRPointer
        {
            [Key(0)]
            public string UserId = "DefaultUserId";
            [Key(1)]
            public string handedness = "";
            [Key(2)]
            public SerializedVector position = new(Vector3.one);
            [Key(3)]
            public SerializedVector rotation = new(Vector3.one);
            [Key(4)]
            public bool visible;

            public XRPointer() { }
            public XRPointer(string u, Handedness h, GameObject g)
            {
                UserId = u;
                handedness = h.ToString();
                position = g.transform.localPosition;
                rotation = g.transform.localRotation.eulerAngles;
                visible = g.activeInHierarchy;
            }
        }

        [Serializable]
        public class XRPlayerMetadata
        {
            public string userId;
            public Metadata metadata;
        }

        [Serializable]
        public class Metadata
        {
            public string ip = "";
            public string role = Network.XRPlayer.Role.Participant.ToString();
            public string sessionCode = "000-000";
            public string autojoinSession = "";
            public string deviceType = "";
            public int battery = 100;
            public Vector3 location;
            public string voiceChatId = "";
            public string voiceChatLocation = "";
        }

        [Serializable]
        public class XRPlayers
        {
            public List<XRPlayer> transforms = new();
        }

        public class NakamaUserPresence : IUserPresence
        {
            public bool Persistence { get; set; }
            public string SessionId { get; set; }
            public string Status { get; set; }
            public string Username { get; set; }
            public string UserId { get; set; }
        }

        [Serializable]
        public class MatchLabel
        {
            public string name;
            public string presentationId;
            public string presentationName;
            public DateTime createdAt;
            public string sessionCode;
            // public bool persistSession;
            // public string nodeId;
            // public bool udpEnabled;
            // public string udpServer;
            // public bool voiceChat;
            public List<SessionTransformOverride> sessionTransformOverrides;
        }

        [Serializable]
        public class SessionTransformOverride
        {
            public string guid;
            public SerializedVector localPosition = Vector3.one;
            public SerializedVector localRotation = Vector3.one;
            public SerializedVector localScale = Vector3.one;

            public void UpdateOverride(SessionTransformOverride newOverride)
            {
                guid = newOverride.guid;
                localPosition = newOverride.localPosition;
                localRotation = newOverride.localRotation;
                localScale = newOverride.localScale;
            }
        }

        [Serializable, MessagePackObject]
        public class NetworkSyncedValue
        {
            public enum NetworkState
            {
                OCCUPIED,
                RELEASING,
                AVAILABLE
            }

            [Key(0)]
            public NetworkState state = NetworkState.AVAILABLE;
            [Key(1)]
            public string guid;
            [Key(2)]
            public string owner;
            [Key(3)]
            public float value;
            [Key(4)]
            public DateTime timeStamp;

            public static implicit operator bool(NetworkSyncedValue syncedValue) => syncedValue != null;

            public NetworkSyncedValue() { }
            public NetworkSyncedValue(string g, string o, float v)
            {
                guid = g;
                owner = o;
                value = v;
                timeStamp = DateTime.UtcNow;
            }

            public void UpdateValue(NetworkSyncedValue v)
            {
                state = v.state;
                owner = v.owner;
                value = v.value;
                timeStamp = v.timeStamp;
            }
        }

        [Serializable, MessagePackObject]
        public class NetworkSyncedTransform
        {
            public enum NetworkState
            {
                OCCUPIED,
                RELEASING,
                AVAILABLE
            }

            [Key(0)]
            public NetworkState state = NetworkState.AVAILABLE;
            [Key(1)]
            public string guid = "";
            [Key(2)]
            public string owner = "";
            [Key(3)]
            public SerializedVector localPosition = new();
            [Key(4)]
            public SerializedVector localRotation = new();
            [Key(5)]
            public SerializedVector localScale = new();
            [Key(6)]
            public DateTime timeStamp = DateTime.UtcNow;

            public static implicit operator bool(NetworkSyncedTransform syncedTransform) => syncedTransform != null;

            public NetworkSyncedTransform() { }
            public NetworkSyncedTransform(string g, string o, Transform t) => UpdateTransform(g, o, t);
            public NetworkSyncedTransform(string g, string o, Vector3 p, Quaternion r, Vector3 s) => UpdateTransform(g, o, p, r, s);

            public void UpdateTransform(NetworkSyncedTransform t)
            {
                state = t.state;
                owner = t.owner;
                localPosition = t.localPosition;
                localRotation = t.localRotation;
                localScale = t.localScale;
                timeStamp = t.timeStamp;
            }

            public void UpdateTransform(string g, string o, Transform t) => UpdateTransform(g, o, t.localPosition, t.localRotation, t.localScale);

            public void UpdateTransform(string g, string o, Vector3 p, Quaternion r, Vector3 s)
            {
                guid = g;
                owner = o;
                localPosition = p;
                localRotation = r.eulerAngles;
                localScale = s;
            }
        }

        [Serializable, MessagePackObject]
        public class SerializedVector
        {
            [Key(0)]
            public float x = 1;
            [Key(1)]
            public float y = 1;
            [Key(2)]
            public float z = 1;

            public static implicit operator Vector3(SerializedVector vector)
            {
                return new Vector3(vector.x, vector.y, vector.z);
            }

            public static implicit operator SerializedVector(Vector3 vector)
            {
                return new(vector);
            }

            public SerializedVector()
            {
                x = 1;
                y = 1;
                z = 1;
            }

            public SerializedVector(Vector3 vector)
            {
                x = vector.x;
                y = vector.y;
                z = vector.z;
            }

            public override string ToString() => $"({x}, {y}, {z})";
        }
    }
}