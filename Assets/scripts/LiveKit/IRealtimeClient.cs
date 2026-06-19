using System;
using System.Collections;
using LiveKit;

public interface IRealtimeClient : IDisposable
{
    ConnectionDetails ConnectionDetails { get; }
    Room ManagedRoom { get; }
    bool IsConnected { get; }
    event Action<string> AgentStateChanged;
    event Action<float> AgentAudioLevelChanged;

    IEnumerator Connect(TokenSourceComponent tokenSourceComponent, string roomName, string participantName, string participantIdentity);
    bool SetMicrophoneCaptureEnabled(bool enabled);
    void PumpEvents();
}
