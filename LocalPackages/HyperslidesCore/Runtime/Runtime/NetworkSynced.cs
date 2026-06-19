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

        public enum FilterMethod
        {
            None = 0,
            Lerp = 1,
            Kalman = 2,
            KalmanQueue = 3,
            OneEuro = 4
        }

        public FilterMethod filterMethod = FilterMethod.Kalman;

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
        public XRNetworkObjects.NetworkSyncedTransform tempSyncedTransform;
        /// <summary>
        /// The synced value that is used to synchronize the state of the component over the network.
        /// </summary>
        public XRNetworkObjects.NetworkSyncedValue syncedValue;
        public XRNetworkObjects.NetworkSyncedValue tempSyncedValue;
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
        /// One Euro filter used to smooth the position updates over time.
        /// </summary>
        private OneEuroFilter positionOneEuroFilter = new OneEuroFilter(60f, 1.0f, 0.0f, 1.0f);
        /// <summary>
        /// One Euro filter used to smooth the rotation updates over time.
        /// </summary>
        private OneEuroFilterQuaternion rotationOneEuroFilter = new OneEuroFilterQuaternion(60f, 1.0f, 0.0f, 1.0f);
        /// <summary>
        /// One Euro filter used to smooth the scale updates over time.
        /// </summary>
        private OneEuroFilter scaleOneEuroFilter = new OneEuroFilter(60f, 1.0f, 0.0f, 1.0f);

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
            XRNetworkManager.Instance.OnUserConnected += ToggleInteraction;
            XRNetworkManager.Instance.OnMatchJoined += ToggleInteraction;

            if (syncType == SyncType.Transform)
                XRNetworkManager.Instance.OnNetworkTransformUpdate += NetworkUpdateTransform;
            if (syncType == SyncType.Value)
                XRNetworkManager.Instance.OnNetworkValueUpdate += NetworkUpdateValue;

            firstTimeCall = true;
            RegisterToComponentCallbacks(true);
            ToggleInteraction();
        }

        /// <summary>
        /// This method unregisters from the network events and component callbacks.
        /// </summary>
        public virtual void OnDisable()
        {
            if (XRNetworkManager.Instance == null)
                return;

            XRNetworkManager.Instance.OnUserConnected -= ToggleInteraction;
            XRNetworkManager.Instance.OnMatchJoined -= ToggleInteraction;

            if (syncType == SyncType.Transform)
                XRNetworkManager.Instance.OnNetworkTransformUpdate -= NetworkUpdateTransform;
            if (syncType == SyncType.Value)
                XRNetworkManager.Instance.OnNetworkValueUpdate -= NetworkUpdateValue;

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
                switch (syncedComponent)
                {
                    case UnityEngine.UI.Slider slider:
                        if (register)
                            slider.onValueChanged.AddListener(UpdateValue);
                        else
                            slider.onValueChanged.RemoveListener(UpdateValue);
                        break;
                    case UI.XRUISlider xrUiSlider:
                        if (register)
                        {
                            xrUiSlider.onValueChanged.AddListener(UpdateValue);
                            xrUiSlider.onTouchPhaseChanged.AddListener(UpdateValue);
                        }
                        else
                        {
                            xrUiSlider.onValueChanged.RemoveListener(UpdateValue);
                            xrUiSlider.onTouchPhaseChanged.RemoveListener(UpdateValue);
                        }
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

            if (XRNetworkManager.Instance.LocalPlayer && DeviceInfo.Instance.Role >= restrictAccessTo)
            {
                if (syncedValue &&
                    !string.IsNullOrEmpty(syncedValue.owner) &&
                    syncedValue.owner != XRNetworkManager.Instance.LocalPlayer.UserId)
                    return;

                tempSyncedValue.state = XRNetworkObjects.NetworkSyncedValue.NetworkState.OCCUPIED;
                tempSyncedValue.guid = guid;
                tempSyncedValue.owner = XRNetworkManager.Instance.LocalPlayer.UserId;
                tempSyncedValue.value = value;
                tempSyncedValue.timeStamp = DateTime.UtcNow;

                XRNetworkManager.Instance.LocalPlayer.AddOrUpdateSyncedItem(tempSyncedValue);
            }
        }

        /// <summary>
        /// Release the value based on touch phase ended
        /// </summary>
        /// <param name="touchPhase">Touch phase of the input</param>
        public void UpdateValue(UnityEngine.InputSystem.TouchPhase touchPhase)
        {
            if (touchPhase == UnityEngine.InputSystem.TouchPhase.Ended || touchPhase == UnityEngine.InputSystem.TouchPhase.Canceled)
                ReleaseValue(syncedValue.value);
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

            if (XRNetworkManager.Instance.LocalPlayer && DeviceInfo.Instance.Role >= restrictAccessTo)
            {
                if (syncedTransform &&
                    !string.IsNullOrEmpty(syncedTransform.owner) &&
                    syncedTransform.owner != XRNetworkManager.Instance.LocalPlayer.UserId)
                    return;

                tempSyncedTransform.state = XRNetworkObjects.NetworkSyncedTransform.NetworkState.OCCUPIED;
                tempSyncedTransform.guid = guid;
                tempSyncedTransform.owner = XRNetworkManager.Instance.LocalPlayer.UserId;
                tempSyncedTransform.localPosition = pos;
                tempSyncedTransform.localRotation = rot.eulerAngles;
                tempSyncedTransform.localScale = scale;
                tempSyncedTransform.timeStamp = DateTime.UtcNow;

                XRNetworkManager.Instance.LocalPlayer.AddOrUpdateSyncedItem(tempSyncedTransform);
            }
        }

        /// <summary>
        /// Release the transform so other users can interact with it
        /// </summary>
        public void ReleaseTransform()
        {
            if (XRNetworkManager.Instance.LocalPlayer)
            {
                if ((syncedTransform && string.IsNullOrEmpty(syncedTransform.owner)) ||
                    syncedTransform.owner != XRNetworkManager.Instance.LocalPlayer.UserId)
                    return;

                syncedTransform.state = XRNetworkObjects.NetworkSyncedTransform.NetworkState.RELEASING;
                syncedTransform.timeStamp = DateTime.UtcNow;

                XRNetworkManager.Instance.LocalPlayer.AddOrUpdateSyncedItem(syncedTransform);
            }
        }

        /// <summary>
        /// Release the value so other users can interact with it
        /// </summary>
        public void ReleaseValue(float finalValue)
        {
            if (XRNetworkManager.Instance.LocalPlayer)
            {
                if ((syncedValue && string.IsNullOrEmpty(syncedValue.owner)) ||
                    syncedValue.owner != XRNetworkManager.Instance.LocalPlayer.UserId)
                    return;

                syncedValue.state = XRNetworkObjects.NetworkSyncedValue.NetworkState.RELEASING;
                syncedValue.value = finalValue;
                syncedValue.timeStamp = DateTime.UtcNow;

                XRNetworkManager.Instance.LocalPlayer.AddOrUpdateSyncedItem(syncedValue);
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

            switch (syncedComponent)
            {
                case UnityEngine.UI.Slider slider:
                    smoothedValue = Mathf.Lerp(instant ? slider.value : syncedValue.value, syncedValue.value, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedValueEasing);
                    slider.SetValueWithoutNotify(instant ? syncedValue.value : smoothedValue);
                    break;
                case UI.XRUISlider xrUiSlider:
                    xrUiSlider.UpdateSlider(syncedValue.value);
                    break;
            }
        }

        /// <summary>
        /// Update local transform from Update() or first call
        /// </summary>
        private void UpdateLocalTransform(bool instant = false)
        {
            if (!syncedTransform)
                return;

            if (instant)
            {
                transform.SetLocalPositionAndRotation(syncedTransform.localPosition, Quaternion.Euler(syncedTransform.localRotation));
                transform.localScale = syncedTransform.localScale;
            }

            if (transformBuffer.Count < 2)
                return;

            double interpolationTime = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds - interpolationBackTime;

            // Remove old states
            while (transformBuffer.Count >= 2 && transformBuffer.ElementAt(1).timeStamp <= interpolationTime)
            {
                transformBuffer.Dequeue();
            }

            switch (filterMethod)
            {
                case FilterMethod.None:
                    // No filtering, directly set the transform
                    transform.localPosition = syncedTransform.localPosition;
                    transform.localRotation = Quaternion.Euler(syncedTransform.localRotation);
                    transform.localScale = syncedTransform.localScale;
                    break;
                case FilterMethod.Lerp:
                    // Linear interpolation between the two closest states
                    transform.localPosition = Vector3.Lerp(transform.localPosition, syncedTransform.localPosition, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedTransformEasing);
                    transform.localRotation = Quaternion.LerpUnclamped(transform.localRotation, Quaternion.Euler(syncedTransform.localRotation), Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedTransformEasing);
                    transform.localScale = Vector3.Lerp(transform.localScale, syncedTransform.localScale, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedTransformEasing);
                    break;
                case FilterMethod.Kalman:
                    // Kalman filter will
                    smoothedPosition = positionKalmanFilter.Update(syncedTransform.localPosition);
                    smoothedRotation = quaternionKalmanFilter.Update(Quaternion.Euler(syncedTransform.localRotation));
                    smoothedScale = scaleKalmanFilter.Update(syncedTransform.localScale);
                    transform.localPosition = Vector3.Lerp(transform.localPosition, smoothedPosition, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedTransformEasing);
                    transform.localRotation = Quaternion.LerpUnclamped(transform.localRotation, smoothedRotation, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedTransformEasing);
                    transform.localScale = Vector3.Lerp(transform.localScale, smoothedScale, Time.deltaTime * HyperSlidesStateManager.Instance.Settings.syncedTransformEasing);
                    break;
                case FilterMethod.KalmanQueue:
                    // Kalman filter with queue will
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
                    break;
                case FilterMethod.OneEuro:
                    // One Euro filter - responsive with jitter reduction
                    // Update filter parameters based on tickrate
                    positionOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
                    rotationOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
                    scaleOneEuroFilter.UpdateParams(HyperSlidesStateManager.Instance.Settings.updateRate, 1.0f, 0.0f, 1.0f);
                    
                    // Apply One Euro filtering directly to the latest network position
                    transform.localPosition = positionOneEuroFilter.Update(syncedTransform.localPosition);
                    transform.localRotation = rotationOneEuroFilter.Update(Quaternion.Euler(syncedTransform.localRotation));
                    transform.localScale = scaleOneEuroFilter.Update(syncedTransform.localScale);
                    break;
            }
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
                {
                    UpdateLocalValue(true);
                    firstTimeCall = false;
                }
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
                syncedTransformEvent?.Invoke(syncedTransform);

                if (firstTimeCall)
                {
                    UpdateLocalTransform(true);
                    firstTimeCall = false;
                }

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
            }
        }

        public virtual void Update()
        {
            if (!XRNetworkManager.Instance.LocalPlayer)
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
            if (!XRNetworkManager.Instance)
                return;

            bool allowedToInteract = XRNetworkManager.Instance.LocalPlayer ? DeviceInfo.Instance.Role >= restrictAccessTo : false;

            if (syncType == SyncType.Transform && TryGetComponent(out Collider col))
                col.enabled = allowedToInteract;

            if (!syncedComponent)
                return;

            switch (syncedComponent)
            {
                case UnityEngine.UI.Slider slider:
                    slider.interactable = allowedToInteract;
                    break;
                case UI.XRUISlider xrUiSlider:
                    xrUiSlider.interactable = allowedToInteract;
                    break;
            }
        }
    }
}