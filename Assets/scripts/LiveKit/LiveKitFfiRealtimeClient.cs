using System;
using System.Collections;
using LiveKit;
using UnityEngine;

public sealed class LiveKitFfiRealtimeClient : IRealtimeClient
{
    public ConnectionDetails ConnectionDetails { get; private set; }
    public Room ManagedRoom { get; private set; }
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

        // Fallback: Falls Werte leer sind (camelCase nicht gemappt wurde), versuche manuelle Konvertierung
        if (string.IsNullOrWhiteSpace(ConnectionDetails.ServerUrl) || string.IsNullOrWhiteSpace(ConnectionDetails.ParticipantToken))
        {
            Debug.LogError("Token endpoint returned empty connection details. Supported JSON keys are server_url/participant_token and serverUrl/participantToken.");
            yield break;
        }

        ManagedRoom = new Room();
        ConnectInstruction connect = ManagedRoom.Connect(ConnectionDetails.ServerUrl, ConnectionDetails.ParticipantToken, new RoomOptions());
        yield return connect;

        if (connect.IsError)
        {
            bool fallbackTried = false;
            string fallbackUrl = null;

            if (TryBuildLocalWsFallbackUrl(ConnectionDetails.ServerUrl, out fallbackUrl))
            {
                fallbackTried = true;
                ManagedRoom.Disconnect();
                ManagedRoom = new Room();

                ConnectInstruction fallbackConnect = ManagedRoom.Connect(fallbackUrl, ConnectionDetails.ParticipantToken, new RoomOptions());
                yield return fallbackConnect;

                if (!fallbackConnect.IsError)
                {
                    IsConnected = true;
                    yield break;
                }
            }

            Debug.LogError($"Failed to connect to room. primaryUrl='{ConnectionDetails.ServerUrl}', fallbackTried={fallbackTried}, fallbackUrl='{fallbackUrl}'");
            yield break;
        }

        IsConnected = true;
    }

    public void PumpEvents()
    {
    }

    public bool SetMicrophoneCaptureEnabled(bool enabled)
    {
        return false;
    }

    private static bool TryBuildLocalWsFallbackUrl(string serverUrl, out string fallbackUrl)
    {
        fallbackUrl = null;
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            return false;

        bool isIpHost = System.Net.IPAddress.TryParse(uri.Host, out _);
        bool isSecureWs = string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
        bool isDefaultTlsPort = uri.IsDefaultPort || uri.Port == 443;

        if (!isIpHost || !isSecureWs || !isDefaultTlsPort)
            return false;

        var builder = new UriBuilder(uri)
        {
            Scheme = "ws",
            Port = 7880
        };

        fallbackUrl = builder.Uri.ToString();
        return true;
    }

    public void Dispose()
    {
        if (ManagedRoom != null)
            ManagedRoom.Disconnect();

        IsConnected = false;
        ManagedRoom = null;
    }
}
