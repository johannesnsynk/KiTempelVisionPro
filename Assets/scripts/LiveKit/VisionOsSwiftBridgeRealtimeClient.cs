using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using LiveKit;
using UnityEngine;

public sealed class VisionOsSwiftBridgeRealtimeClient : IRealtimeClient
{
    private delegate void NativeStringCallback(IntPtr messagePtr);

#if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void lk_visionos_set_log_callback(NativeStringCallback callback);

    [DllImport("__Internal")]
    private static extern void lk_visionos_set_event_callback(NativeStringCallback callback);

    [DllImport("__Internal")]
    private static extern int lk_visionos_connect(string serverUrl, string participantToken);

    [DllImport("__Internal")]
    private static extern int lk_visionos_set_microphone_enabled(int enabled);

    [DllImport("__Internal")]
    private static extern void lk_visionos_disconnect();
#endif

    private static readonly Queue<string> EventQueue = new Queue<string>();
    private static bool _callbacksRegistered;

    private bool _connectCompleted;
    private bool _connectFailed;
    private string _connectError;
    private bool _hasPendingMicrophoneState;
    private bool _pendingMicrophoneEnabled;

    public ConnectionDetails ConnectionDetails { get; private set; }
    public Room ManagedRoom => null;
    public bool IsConnected { get; private set; }
    public event Action<string> AgentStateChanged;

    public IEnumerator Connect(TokenSourceComponent tokenSourceComponent, string roomName, string participantName, string participantIdentity)
    {
        TaskYieldInstruction<ConnectionDetails> connectionDetailTask = tokenSourceComponent.FetchConnectionDetails(new TokenSourceFetchOptions
        {
            RoomName = roomName,
            ParticipantName = participantName,
            ParticipantIdentity = participantIdentity
        });
        yield return connectionDetailTask;

        if (connectionDetailTask.IsError)
        {
            Debug.LogError("Failed to fetch connection details: " + connectionDetailTask.Result);
            yield break;
        }

        ConnectionDetails = connectionDetailTask.Result;

        if (string.IsNullOrWhiteSpace(ConnectionDetails?.ServerUrl) || string.IsNullOrWhiteSpace(ConnectionDetails?.ParticipantToken))
        {
            Debug.LogError("Token endpoint returned empty connection details.");
            yield break;
        }

#if UNITY_VISIONOS && !UNITY_EDITOR
        EnsureCallbacksRegistered();

        _connectCompleted = false;
        _connectFailed = false;
        _connectError = null;

        int connectStartResult = lk_visionos_connect(ConnectionDetails.ServerUrl, ConnectionDetails.ParticipantToken);
        if (connectStartResult != 0)
        {
            Debug.LogError("Native visionOS connect failed to start. Code: " + connectStartResult);
            yield break;
        }

        float timeoutSeconds = 60f;
        float elapsed = 0f;
        while (!_connectCompleted && !_connectFailed && elapsed < timeoutSeconds)
        {
            PumpEvents();
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_connectFailed)
        {
            Debug.LogError("visionOS bridge connect failed: " + _connectError);
            yield break;
        }

        if (!_connectCompleted)
        {
            Debug.LogError("visionOS bridge connect timed out.");
            yield break;
        }

        IsConnected = true;
#else
        Debug.LogWarning("VisionOsSwiftBridgeRealtimeClient is active outside visionOS player; no native connection created.");
        IsConnected = true;
#endif
    }

    public void PumpEvents()
    {
        List<string> pending = null;

        lock (EventQueue)
        {
            if (EventQueue.Count == 0)
                return;

            pending = new List<string>(EventQueue.Count);
            while (EventQueue.Count > 0)
                pending.Add(EventQueue.Dequeue());
        }

        for (int i = 0; i < pending.Count; i++)
            HandleEvent(pending[i]);
    }

    public bool SetMicrophoneCaptureEnabled(bool enabled)
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        if (!IsConnected)
        {
            _pendingMicrophoneEnabled = enabled;
            _hasPendingMicrophoneState = true;
            return true;
        }

        int result = lk_visionos_set_microphone_enabled(enabled ? 1 : 0);
        if (result != 0)
            Debug.LogWarning("visionOS microphone toggle failed to start. Code: " + result + ", enabled=" + enabled);

        return result == 0;
#else
        return false;
#endif
    }

    public void Dispose()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        lk_visionos_disconnect();
#endif
        _hasPendingMicrophoneState = false;
        IsConnected = false;
    }

#if UNITY_VISIONOS && !UNITY_EDITOR
    private static void EnsureCallbacksRegistered()
    {
        if (_callbacksRegistered)
            return;

        lk_visionos_set_log_callback(OnNativeLogMessage);
        lk_visionos_set_event_callback(OnNativeEvent);
        _callbacksRegistered = true;
    }

    [MonoPInvokeCallback(typeof(NativeStringCallback))]
    private static void OnNativeLogMessage(IntPtr messagePtr)
    {
        string message = PtrToString(messagePtr);
        if (string.IsNullOrWhiteSpace(message))
            return;

        Debug.Log("[VisionOSBridge] " + message);
    }

    [MonoPInvokeCallback(typeof(NativeStringCallback))]
    private static void OnNativeEvent(IntPtr messagePtr)
    {
        string message = PtrToString(messagePtr);
        if (string.IsNullOrWhiteSpace(message))
            return;

        lock (EventQueue)
        {
            EventQueue.Enqueue(message);
        }
    }

    private static string PtrToString(IntPtr messagePtr)
    {
        if (messagePtr == IntPtr.Zero)
            return string.Empty;

        return Marshal.PtrToStringAnsi(messagePtr) ?? string.Empty;
    }
#endif

    private void HandleEvent(string eventMessage)
    {
        if (string.Equals(eventMessage, "connected", StringComparison.Ordinal))
        {
            _connectCompleted = true;
            IsConnected = true;

#if UNITY_VISIONOS && !UNITY_EDITOR
            if (_hasPendingMicrophoneState)
            {
                int result = lk_visionos_set_microphone_enabled(_pendingMicrophoneEnabled ? 1 : 0);
                if (result == 0)
                    _hasPendingMicrophoneState = false;
                else
                    Debug.LogWarning("visionOS deferred microphone toggle failed to start. Code: " + result + ", enabled=" + _pendingMicrophoneEnabled);
            }
#endif

            return;
        }

        if (eventMessage.StartsWith("connect-error:", StringComparison.Ordinal))
        {
            _connectFailed = true;
            _connectError = eventMessage.Substring("connect-error:".Length).Trim();
            IsConnected = false;
            return;
        }

        if (eventMessage.StartsWith("microphone-error:", StringComparison.Ordinal))
        {
            Debug.LogWarning("visionOS microphone setup failed: " + eventMessage);
            return;
        }

        if (string.Equals(eventMessage, "microphone-disabled", StringComparison.Ordinal))
            return;

        if (string.Equals(eventMessage, "microphone-enabled", StringComparison.Ordinal))
            return;

        if (eventMessage.StartsWith("agent-state:", StringComparison.Ordinal))
        {
            string state = eventMessage.Substring("agent-state:".Length).Trim();
            if (!string.IsNullOrWhiteSpace(state))
                AgentStateChanged?.Invoke(state);

            return;
        }

        if (string.Equals(eventMessage, "disconnected", StringComparison.Ordinal))
        {
            IsConnected = false;
            return;
        }

        Debug.Log("[VisionOSBridge] Event: " + eventMessage);
    }
}
