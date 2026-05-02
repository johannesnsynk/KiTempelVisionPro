using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using NSYNK.HyperSlides.Runtime;
using System.Linq;

namespace NSYNK.HyperSlides.Network
{

    /// <summary>
    /// This class is used to sync a component or value over the network.
    /// A network synced component can sync a transform or a value and handles the interaction with the local user and updates the component or value accordingly.
    /// It is used to synchronize the state of the component or value across all users in the network.
    /// </summary>
    public class NetworkSynced : MonoBehaviour
    {
        /// <summary>
        /// The type of synchronization to use for the component.
        /// This can either be a transform (position, rotation, scale) or a value.
        /// </summary>
        public enum SyncType
        {
            Transform = 0,
            Value = 1
        }

        /// <summary>
        /// Unique identifier for the network synced component.
        /// </summary>
        public string guid;
        /// <summary>
        /// The role of the user that is allowed to interact with this component.
        /// </summary>
        public XRPlayer.Role restrictAccessTo = XRPlayer.Role.Moderator;
        /// <summary>
        /// The type of synchronization to use for the component.
        /// </summary>
        public SyncType syncType;
        /// <summary>
        /// The component that is synced over the network.
        /// This can be a Unity UI Slider or a custom UI component that implements the same interface.
        /// </summary>
        public Component syncedComponent;
        /// <summary>
        /// The synced transform that is used to synchronize the state of the component over the network.
        /// </summary>
        public XRNetworkObjects.NetworkSyncedTransform syncedTransform;
        /// <summary>
        /// The synced value that is used to synchronize the state of the component over the network.
        /// </summary>
        public XRNetworkObjects.NetworkSyncedValue syncedValue;
        /// <summary>
        /// Event that is triggered when the synced transform is updated over the network.
        /// </summary>
        public UnityEvent<XRNetworkObjects.NetworkSyncedTransform> syncedTransformEvent;
        /// <summary>
        /// Event that is triggered when the synced value is updated over the network.
        /// </summary>
        public UnityEvent<float> syncedValueEvent;

        /// <summary>
        /// Flag to indicate if this is the first time the component is being called.
        /// </summary>
        private bool firstTimeCall = true;
        /// <summary>
        /// This variable is used to smooth the value updates over time.
        /// </summary>
        private float smoothedValue = 0;
        /// <summary>
        /// This variable is used to smooth the position updates over time.
        /// </summary>
        private Vector3 smoothedPosition = Vector3.zero;
        /// <summary>
        /// This variable is used to smooth the rotation updates over time.
        /// </summary>
        private Quaternion smoothedRotation = Quaternion.identity;
        /// <summary>
        /// This variable is used to smooth the scale updates over time.
        /// </summary>
        private Vector3 smoothedScale = Vector3.one;
        /// <summary>
        /// Kalman filter used to smooth the position updates over time.
        /// </summary>
        private KalmanFilter positionKalmanFilter = new KalmanFilter(0.1f, 0.1f, Vector3.zero, Vector3.one);
        /// <summary>
        /// Kalman filter used to smooth the rotation updates over time.
        /// </summary>
        private QuaternionKalmanFilter quaternionKalmanFilter = new QuaternionKalmanFilter(0.1f, 0.1f, Quaternion.identity, 1.0f);
        /// <summary>
        /// Kalman filter used to smooth the scale updates over time.
        /// </summary>
        private KalmanFilter scaleKalmanFilter = new KalmanFilter(0.1f, 0.1f, Vector3.one, Vector3.one);

        /// <summary>
        /// Transform type to be saved in the transformBuffer
        /// </summary>
        private class BufferedTransform
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public double timeStamp;
            public Vector3 velocity; // for extrapolation
        }
        /// <summary>
        /// Queue to be used for network transform smoothing
        /// </summary>
        private readonly Queue<BufferedTransform> transformBuffer = new();
        /// <summary>
        /// age of transforms to be used for interpolation
        /// </summary>
        private const float interpolationBackTime = 0.1f;

        /// <summary>
        /// This method initializes the GUID if it is not set and registers to the network events.
        /// </summary>
        public virtual void OnEnable()
        {
            XRNetworkManager.onUserConnected += ToggleInteraction;
            XRNetworkManager.onMatchJoined += ToggleInteraction;

            if (syncType == SyncType.Transform)
                XRNetworkManager.onNetworkTransformUpdate += NetworkUpdateTransform;
            if (syncType == SyncType.Value)
                XRNetworkManager.onNetworkdValueUpdate += NetworkUpdateValue;

            firstTimeCall = true;
            RegisterToComponentCallbacks(true);
            ToggleInteraction();
        }

        /// <summary>
        /// This method unregisters from the network events and component callbacks.
        /// </summary>
        public virtual void OnDisable()
        {
            XRNetworkManager.onUserConnected -= ToggleInteraction;
            XRNetworkManager.onMatchJoined -= ToggleInteraction;

            if (syncType == SyncType.Transform)
                XRNetworkManager.onNetworkTransformUpdate -= NetworkUpdateTransform;
            if (syncType == SyncType.Value)
                XRNetworkManager.onNetworkdValueUpdate -= NetworkUpdateValue;

            RegisterToComponentCallbacks(false);
        }

        /// <summary>
        /// Register to the component set in inspector if supported
        /// </summary>
        /// <param name="register"></param>
        private void RegisterToComponentCallbacks(bool register = true)
        {
            if (syncedComponent)
            {
                switch (syncedComponent.GetType().ToString())
                {
                    case "UnityEngine.UI.Slider":
                        if (register)
                            ((UnityEngine.UI.Slider)syncedComponent).onValueChanged.AddListener(UpdateValue);
                        else
                            ((UnityEngine.UI.Slider)syncedComponent).onValueChanged.RemoveListener(UpdateValue);
                        break;
                    case "NSYNK.HyperSlides.UI.XRUISlider":
                        if (register)
                            ((UI.XRUISlider)syncedComponent).onValueChanged.AddListener(UpdateValue);
                        else
                            ((UI.XRUISlider)syncedComponent).onValueChanged.RemoveListener(UpdateValue);
                        break;
                }
            }
        }

        /// <summary>
        /// Autogenerate a GUID to have it static over the course of creation
        /// </summary>
        public virtual void OnValidate()
        {
            //List<NetworkSynced> allNetworkSyncs = FindObjectsByType<NetworkSynced>(FindObjectsSortMode.None).ToList();

            //if(allNetworkSyncs.Find(nsync => nsync.guid == guid))
            //    guid = Guid.NewGuid().ToString();

            if (string.IsNullOrEmpty(guid))
                guid = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Update the value and send to network
        /// </summary>
        /// <param name="value"></param>
        public void UpdateValue(float value)
        {
            ToggleInteraction();

            if (XRNetworkManager.localPlayer && DeviceInfo.Role >= restrictAccessTo)
            {
                if (syncedValue &&
                    !string.IsNullOrEmpty(syncedValue.owner) &&
                    syncedValue.owner != XRNetworkManager.localPlayer.UserId)
                    return;

                XRNetworkObjects.NetworkSyncedValue newSyncedValue = new(guid, XRNetworkManager.localPlayer.UserId, value);
                XRNetworkManager.localPlayer.AddOrUpdateSyncedItem(newSyncedValue);
            }
        }

        /// <summary>
        /// Update the transform and send to network
        /// </summary>
        public void UpdateTransform() => UpdateTransform(transform.position, transform.rotation, transform.localScale);

        /// <summary>
        /// Update the transform and send to network
        /// </summary>
        public void UpdateTransform(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            ToggleInteraction();

            if (XRNetworkManager.localPlayer && DeviceInfo.Role >= restrictAccessTo)
            {
                if (syncedTransform &&
                    !string.IsNullOrEmpty(syncedTransform.owner) &&
                    syncedTransform.owner != XRNetworkManager.localPlayer.UserId)
                    return;

                syncedTransform.guid = guid;
                syncedTransform.owner = XRNetworkManager.localPlayer.UserId;
                syncedTransform.localPosition = pos;
                syncedTransform.localRotation = rot.eulerAngles;
                syncedTransform.localScale = scale;

                XRNetworkManager.localPlayer.AddOrUpdateSyncedItem(syncedTransform);
            }
        }

        /// <summary>
        /// Update local value from Update() or first call
        /// </summary>
        /// <param name="instant"></param>
        private void UpdateLocalValue(bool instant = false)
        {
            if (!syncedComponent)
                return;

            firstTimeCall = false;

            switch (syncedComponent.GetType().ToString())
            {
                case "UnityEngine.UI.Slider":
                    smoothedValue = Mathf.Lerp(((UnityEngine.UI.Slider)syncedComponent).value, syncedValue.value, Time.deltaTime * RuntimeHandler.Settings.syncedValueEasing);
                    ((UnityEngine.UI.Slider)syncedComponent).SetValueWithoutNotify(instant ? syncedValue.value : smoothedValue);
                    break;
                case "NSYNK.HyperSlides.UI.XRUISlider":
                    ((UI.XRUISlider)syncedComponent).UpdateSlider(syncedValue.value);
                    break;
            }
        }

        /// <summary>
        /// Update local transform from Update() or first call
        /// </summary>
        private void UpdateLocalTransform(bool instant = false)
        {
            if (instant)
            {
                transform.SetLocalPositionAndRotation(syncedTransform.localPosition, Quaternion.Euler(syncedTransform.localRotation));
                transform.localScale = syncedTransform.localScale;
            }

            firstTimeCall = false;

            if (transformBuffer.Count < 2)
                return;

            double interpolationTime = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds - interpolationBackTime;

            // Remove old states
            while (transformBuffer.Count >= 2 && transformBuffer.ElementAt(1).timeStamp <= interpolationTime)
            {
                transformBuffer.Dequeue();
            }

            var states = transformBuffer.ToArray();
            if (states.Length >= 2)
            {
                var prev = states[0];
                var next = states[1];
                float t = 0f;
                double span = next.timeStamp - prev.timeStamp;
                if (span > 0.0001)
                    t = (float)((interpolationTime - prev.timeStamp) / span);

                transform.localPosition = positionKalmanFilter.Update(Vector3.Lerp(prev.position, next.position, t));
                transform.localRotation = quaternionKalmanFilter.Update(Quaternion.LerpUnclamped(prev.rotation, next.rotation, t));
                transform.localScale = scaleKalmanFilter.Update(Vector3.Lerp(prev.scale, next.scale, t));
            }
            else if (states.Length == 1)
            {
                var lastState = transformBuffer.Last();
                double timeSinceLastState = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds - lastState.timeStamp;

                // Only extrapolate for a reasonable amount of time (e.g., 0.5 seconds)
                if (timeSinceLastState < 0.5f)
                {
                    // Extrapolate position based on last known velocity
                    Vector3 extrapolatedPosition = lastState.position + (lastState.velocity * (float)timeSinceLastState);
                    transform.localPosition = positionKalmanFilter.Update(extrapolatedPosition);
                }
                else
                {
                    transform.localPosition = positionKalmanFilter.Update(lastState.position);
                }

                transform.localRotation = quaternionKalmanFilter.Update(lastState.rotation);
                transform.localScale = scaleKalmanFilter.Update(lastState.scale);
            }


            // smoothedPosition = positionKalmanFilter.Update(syncedTransform.localPosition);
            // smoothedRotation = quaternionKalmanFilter.Update(Quaternion.Euler(syncedTransform.localRotation));
            // smoothedScale = scaleKalmanFilter.Update(syncedTransform.localScale);

            // transform.localPosition = Vector3.Lerp(transform.localPosition, smoothedPosition, Time.deltaTime * RuntimeHandler.Settings.syncedTransformEasing);
            // transform.localRotation = Quaternion.LerpUnclamped(transform.localRotation, smoothedRotation, Time.deltaTime * RuntimeHandler.Settings.syncedTransformEasing);
            // transform.localScale = Vector3.Lerp(transform.localScale, smoothedScale, Time.deltaTime * RuntimeHandler.Settings.syncedTransformEasing);
        }

        /// <summary>
        /// Update the value from network
        /// </summary>
        /// <param name="value"></param>
        public void NetworkUpdateValue(XRNetworkObjects.NetworkSyncedValue networkSyncedValue)
        {
            ToggleInteraction();

            if (networkSyncedValue.guid == guid)
            {
                syncedValue = networkSyncedValue;
                syncedValueEvent?.Invoke(syncedValue.value);

                if (firstTimeCall)
                    UpdateLocalValue(true);
            }
        }

        /// <summary>
        /// Update the transform from network by adding it to the buffer
        /// </summary>
        /// <param name="p">Position</param>
        /// <param name="r">Rotation</param>
        /// <param name="s">Scale</param>
        public void NetworkUpdateTransform(XRNetworkObjects.NetworkSyncedTransform networkSyncedTransform)
        {
            ToggleInteraction();

            if (networkSyncedTransform.guid == guid)
            {
                syncedTransform = networkSyncedTransform;

                Vector3 velocity = Vector3.zero;
                if (transformBuffer.Count > 0)
                {
                    var lastState = transformBuffer.Last();
                    double deltaTime = networkSyncedTransform.timeStamp.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds - lastState.timeStamp;
                    if (deltaTime > 0)
                    {
                        velocity = (networkSyncedTransform.localPosition - lastState.position) / (float)deltaTime;
                    }
                }

                transformBuffer.Enqueue(new BufferedTransform
                {
                    position = networkSyncedTransform.localPosition,
                    rotation = Quaternion.Euler(networkSyncedTransform.localRotation),
                    scale = networkSyncedTransform.localScale,
                    timeStamp = networkSyncedTransform.timeStamp.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalSeconds,
                    velocity = velocity  // Store the calculated velocity
                });

                // Keep buffer site reasonable
                while (transformBuffer.Count > 20)
                    transformBuffer.Dequeue();

                if (firstTimeCall)
                    UpdateLocalTransform(true);
            }
        }

        public virtual void Update()
        {
            if (!XRNetworkManager.localPlayer)
                return;

            if (syncType == SyncType.Transform)
                UpdateLocalTransform();

            if (syncType == SyncType.Value)
                UpdateLocalValue();
        }

        /// <summary>
        /// Enable or disable interaction from local user
        /// </summary>
        private void ToggleInteraction()
        {
            bool allowedToInteract = XRNetworkManager.localPlayer ? DeviceInfo.Role >= restrictAccessTo : false;

            if (syncType == SyncType.Transform && TryGetComponent(out Collider col))
                col.enabled = allowedToInteract;

            if (!syncedComponent)
                return;

            switch (syncedComponent.GetType().ToString())
            {
                case "UnityEngine.UI.Slider":
                    ((UnityEngine.UI.Slider)syncedComponent).interactable = allowedToInteract;
                    break;
                case "NSYNK.HyperSlides.UI.XRUISlider":
                    ((UI.XRUISlider)syncedComponent).interactable = allowedToInteract;
                    break;
            }
        }
    }
}