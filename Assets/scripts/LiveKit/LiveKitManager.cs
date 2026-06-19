using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CrazyMinnow.SALSA;
using LiveKit;
using LiveKit.Proto;
using NSYNK.LiveKitIntegration;
using UnityEngine;
using UnityEngine.InputSystem;

public class LiveKitManager : MonoBehaviour
{
    [SerializeField]
    private Salsa salsaLipSync;

    [Header("Connection Overrides")]
    [SerializeField]
    private string _roomName = "local-ai-room";
    [SerializeField]
    private string _participantName = "unity-client";
    [SerializeField]
    private string _participantIdentity = "unity-client";
    [SerializeField]
    private bool _useUniqueIdentityPerConnectInEditor = false;
    [SerializeField]
    private bool _useStableUniqueIdentityPerInstall = true;
    [SerializeField]
    private string _transcriptionTopic = "lk.transcription";
    [SerializeField]
    private LocalVoice_Input _inputActions;
    [SerializeField]
    private bool _enablePushToTalk = true;

    [Header("Chat & Avatar")]
    // Reference to the chat message prefab used to display the send message of the local user (for now)
    [SerializeField]
    private ChatMessage chatMessage;
    // Reference to the chat message prefab used to display the bot messages, we might need more prefabs later depending on the role of the participant.
    [SerializeField]
    private ChatMessage botChatMessage;
    // Reference to the content transform under which chat message instances will be instantiated.
    [SerializeField]
    private Transform chatContent;
    // Reference to the avatar clips controller, used to update the agent avatar state based on participant attributes.
    [SerializeField]

    // The TokenSourceComponent is responsible for fetching the connection details (server URL and participant token) needed to connect to the LiveKit room as an asset.
    private TokenSourceComponent _tokenSourceComponent;
    // The connection details object holds the server URL and participant token needed to connect to the LiveKit room.
    private LiveKit.ConnectionDetails _connectionDetails;
    // The realtime client allows switching between LiveKit FFI and the visionOS Swift bridge.
    private IRealtimeClient _realtimeClient;
    // The room object represents the connection to the LiveKit server and provides access to participants, tracks, and events in the room.
    private Room _room;
    // The audio objects and streams dictionaries are used to manage the lifecycle of audio tracks subscribed in the room. When a new audio track is subscribed, an AudioSource is created in the scene to play the incoming audio.
    private readonly Dictionary<string, GameObject> _audioObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, AudioStream> _audioStreams = new Dictionary<string, AudioStream>();
    private const string LocalAudioSourceObjectName = "my-audio-source";
    private const string LocalAudioTrackName = "my-audio-track";
    private const string InstallIdentityKey = "livekit.install.identity";
    private LocalAudioTrack _localAudioTrack;
    private RtcAudioSource _localRtcAudioSource;
    private InputAction _pushToTalkAction;
    private bool _isPushToTalkPressed;
    private bool _isMicrophoneCaptureActive;

    // We keep track of the last delivered message from each owner to avoid showing duplicates in the chat log
    private readonly Dictionary<string, string> _lastDeliveredMessageByOwner = new Dictionary<string, string>();
    // Chat history lines are final messages that have been delivered and should remain in the order they were received.
    private readonly List<ChatLineEntry> _chatHistoryLines = new List<ChatLineEntry>();
    // Active lines are interim transcription results that are still being updated. They are stored separately from the history so they can be updated in place without affecting the order of final messages in the history.
    private readonly Dictionary<string, ChatLineEntry> _activeStreamLines = new Dictionary<string, ChatLineEntry>();
    // Registered text topics are tracked so we can clean up handlers when the object is destroyed.
    private readonly HashSet<string> _registeredTextTopics = new HashSet<string>();
    // The agent participant reference is used to track the participant in the room that represents the AI agent.
    private RemoteParticipant _agentParticipant;
    // The chat sequence is a simple counter to ensure a consistent ordering of chat messages in the log when multiple messages have the same timestamp.
    private long _chatSequence;
    private Coroutine _connectRoutine;

    private void OnEnable()
    {
        salsaLipSync = FindAnyObjectByType<Salsa>(FindObjectsInactive.Include);

        // SetupPushToTalk();
        StopAllCoroutines();
        StartCoroutine(InitLiveKit());
    }

    private IEnumerator InitLiveKit()
    {
        Debug.Log("LiveKitManager starting up and connecting to room '" + _roomName + "' as participant '" + _participantName + "' with identity '" + _participantIdentity + "'.");

        _tokenSourceComponent = GetComponent<TokenSourceComponent>();
        _connectRoutine = StartCoroutine(ConnectRealtime("startup"));
        yield return _connectRoutine;
    }

    private void Update()
    {
        _realtimeClient?.PumpEvents();
    }

    private IEnumerator ConnectRealtime(string reason)
    {
        if (_tokenSourceComponent == null)
        {
            Debug.LogError("TokenSourceComponent missing; cannot connect realtime client.");
            _connectRoutine = null;
            yield break;
        }

        TearDownRealtimeClient();

        _realtimeClient = RealtimeClientFactory.Create();
        if (_realtimeClient == null)
        {
            Debug.LogError("No realtime client available.");
            _connectRoutine = null;
            yield break;
        }
        else
        {
            Debug.Log("Realtime client created using " + _realtimeClient.GetType().Name + " (" + reason + ").");
        }

        string connectIdentity = ResolveConnectParticipantIdentity();
        _realtimeClient.AgentStateChanged += OnRealtimeAgentStateChanged;
        yield return _realtimeClient.Connect(_tokenSourceComponent, _roomName, _participantName, connectIdentity);

        _connectionDetails = _realtimeClient.ConnectionDetails;
        _room = _realtimeClient.ManagedRoom;

        if (!_realtimeClient.IsConnected)
        {
            Debug.LogError("Failed to connect realtime client (" + reason + ").");
            _connectRoutine = null;
            yield break;
        }

        if (_room == null)
        {
            Debug.Log("Connected via native bridge backend (no managed Room available in C#). Reason=" + reason);
            ApplyPushToTalkState();
            _connectRoutine = null;
            yield break;
        }
        else
        {
            Debug.Log("Connected to room '" + _room.Name + "' with SID '" + _room.Sid + "' (" + reason + ").");
        }

        _room.TrackSubscribed += TrackSubscribed;
        _room.ParticipantConnected += OnParticipantConnected;
        _room.ParticipantDisconnected += OnParticipantDisconnected;
        _room.ParticipantAttributesChanged += OnParticipantAttributesChanged;
        _room.DataReceived += OnDataReceived;
        RegisterTextHandlers(_room);

        Debug.Log("Connected to " + _room.Name + " (" + reason + ").");
        StartCoroutine(PublishMicrophone(_room));
        _connectRoutine = null;
    }

    private void TearDownRealtimeClient()
    {
        Debug.Log("Tearing down realtime client and cleaning up resources.");

        CleanupLocalMicrophone();

        if (_realtimeClient != null)
            _realtimeClient.AgentStateChanged -= OnRealtimeAgentStateChanged;

        if (_room != null)
        {
            _room.TrackSubscribed -= TrackSubscribed;
            _room.ParticipantConnected -= OnParticipantConnected;
            _room.ParticipantDisconnected -= OnParticipantDisconnected;
            _room.ParticipantAttributesChanged -= OnParticipantAttributesChanged;
            _room.DataReceived -= OnDataReceived;

            foreach (string topic in _registeredTextTopics)
                _room.UnregisterTextStreamHandler(topic);
        }

        _registeredTextTopics.Clear();

        foreach (AudioStream stream in _audioStreams.Values)
            SafeDisposeAudioStream(stream);
        _audioStreams.Clear();

        foreach (KeyValuePair<string, GameObject> entry in _audioObjects)
        {
            if (entry.Value == null)
                continue;

            SafeStopAudioSource(entry.Value);
            Destroy(entry.Value);
        }
        _audioObjects.Clear();

        _realtimeClient?.Dispose();
        _room = null;
        _realtimeClient = null;
    }

    private void CleanupLocalMicrophone()
    {
        if (_room != null && _localAudioTrack != null)
        {
            try
            {
                _room.LocalParticipant.UnpublishTrack(_localAudioTrack, false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to unpublish local microphone track during cleanup: " + ex.Message);
            }
        }

        DisposeSource(ref _localRtcAudioSource);
        _isMicrophoneCaptureActive = false;
        _localAudioTrack = null;

        if (_audioObjects.TryGetValue(LocalAudioSourceObjectName, out GameObject localAudioObject) && localAudioObject != null)
        {
            SafeStopAudioSource(localAudioObject);
            Destroy(localAudioObject);
            _audioObjects.Remove(LocalAudioSourceObjectName);
        }
    }

    private static void SafeDisposeAudioStream(AudioStream stream)
    {
        if (stream == null)
            return;

        try
        {
            stream.Dispose();
        }
        catch (MissingReferenceException)
        {
            // Unity may already have destroyed the underlying AudioSource during teardown.
        }
    }

    private static void SafeStopAudioSource(GameObject audioObject)
    {
        if (audioObject == null)
            return;

        try
        {
            AudioSource source = audioObject.GetComponent<AudioSource>();
            if (source != null)
                source.Stop();
        }
        catch (MissingReferenceException)
        {
            // Object/component was already destroyed by Unity lifecycle teardown.
        }
    }

    private static void DisposeSource<T>(ref T source) where T : class, IDisposable
    {
        if (source is RtcAudioSource audio)
            audio?.Stop();

        source?.Dispose();
        source = null;
    }

    private void SetupPushToTalk()
    {
        if (!_enablePushToTalk)
            return;

        _inputActions = new LocalVoice_Input();

        _pushToTalkAction = _inputActions.Voice.PTT;

        _pushToTalkAction.performed += OnPushToTalkPerformed;
        _pushToTalkAction.canceled += OnPushToTalkCanceled;

        _pushToTalkAction.Enable();

        _isPushToTalkPressed = _pushToTalkAction.IsPressed();
    }

    private void TeardownPushToTalk()
    {
        if (_pushToTalkAction == null)
            return;

        _pushToTalkAction.performed -= OnPushToTalkPerformed;
        _pushToTalkAction.canceled -= OnPushToTalkCanceled;

        _pushToTalkAction.Disable();
        _pushToTalkAction = null;
        _isPushToTalkPressed = false;
    }

    private void OnPushToTalkPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("Push-to-talk or trigger pressed.");
        _isPushToTalkPressed = true;
        ApplyPushToTalkState();
    }

    private void OnPushToTalkCanceled(InputAction.CallbackContext context)
    {
        Debug.Log("Push-to-talk or trigger released.");
        _isPushToTalkPressed = false;
        ApplyPushToTalkState();
    }

    private void ApplyPushToTalkState()
    {
        bool shouldCapture = !_enablePushToTalk || _isPushToTalkPressed;
        SetMicrophoneCaptureEnabled(shouldCapture);
    }

    private void SetMicrophoneCaptureEnabled(bool enabled)
    {
        if (_isMicrophoneCaptureActive == enabled)
            return;

        if (_localRtcAudioSource != null)
        {
            if (enabled)
                _localRtcAudioSource.Start();
            else
                _localRtcAudioSource.Stop();

            _isMicrophoneCaptureActive = enabled;
            return;
        }

        if (_realtimeClient == null)
            return;

        if (!_realtimeClient.IsConnected)
            return;

        if (_realtimeClient.SetMicrophoneCaptureEnabled(enabled))
            _isMicrophoneCaptureActive = enabled;
    }

    /// <summary>
    /// Event fired when a participant joins the created room and if its an agent, assign the agent reference to it. This might be altered later, when we do not use livekit as agent connection or becomes optional.
    /// </summary>
    /// <param name="participant"></param>
    private void OnParticipantConnected(Participant participant)
    {
        //Ignore local participant join messages
        if (participant is not RemoteParticipant remoteParticipant)
            return;

        if (LooksLikeAgent(remoteParticipant))
            _agentParticipant = remoteParticipant;
    }

    /// <summary>
    /// Event fired when a participant leaves the room and if its the agent, clear the agent reference.
    /// </summary>
    /// <param name="participant"></param>
    private void OnParticipantDisconnected(Participant participant)
    {
        if (_agentParticipant != null && participant.Identity == _agentParticipant.Identity)
            _agentParticipant = null;
    }

    /// <summary>
    /// Event fired when a participant attributes change and if its the agent, check for the agent state attribute and update the avatar accordingly.
    /// </summary>
    /// <param name="participant">The participant whose attributes have changed.</param>
    private void OnParticipantAttributesChanged(Participant participant)
    {
        if (participant is not RemoteParticipant remoteParticipant)
            return;

        if (!remoteParticipant.Attributes.TryGetValue("bridge.audio.status", out string agentState))
            return;

        _agentParticipant = remoteParticipant;
        ApplyAgentState(agentState);
    }

    private void OnRealtimeAgentStateChanged(string agentState)
    {
        ApplyAgentState(agentState);
    }

    private void ApplyAgentState(string agentState)
    {
        if (string.IsNullOrWhiteSpace(agentState))
            return;

        switch (agentState.ToLowerInvariant())
        {
            case "thinking":
                // avatarController?.SetClip(AvatarClipsController.AvatarClipName.Checking);
                break;
            case "speaking":
                // avatarController?.SetClip(AvatarClipsController.AvatarClipName.Talk1);
                break;
            case "listening":
                // avatarController?.PlayClip(AvatarClipsController.AvatarClipName.Wave);
                break;
        }
    }

    /// <summary>
    /// Event fired when a data message is received in the room. This can be used for custom data channels, but in this example we just log the content and add it to the chat as a new message.
    /// </summary>
    /// <param name="data">The data received in the message.</param>
    /// <param name="participant">The participant who sent the message.</param>
    /// <param name="kind">The kind of data packet received.</param>
    /// <param name="topic">The topic of the data message.</param>
    private void OnDataReceived(byte[] data, Participant participant, DataPacketKind kind, string topic)
    {
        Debug.Log("Data received from " + participant?.Identity + " on topic '" + topic + "': " + (data != null ? Encoding.UTF8.GetString(data) : "null"));

        if (data == null || data.Length == 0)
            return;

        // Transcription text for this topic is handled via RegisterTextStreamHandler.
        // Avoid rendering the same agent sentence once from data channel and once from text stream.
        if (string.Equals(topic, _transcriptionTopic, StringComparison.Ordinal))
            return;

        string identity = participant?.Identity ?? "unknown";
        try
        {
            string text = Encoding.UTF8.GetString(data);
            TryAppendChatMessage(identity, text, NextTimestamp());
        }
        catch
        {
            // Ignore non-UTF8 payloads for chat rendering.
        }
    }

    /// <summary>
    /// Clean up event handlers and references when the object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (_connectRoutine != null)
        {
            StopCoroutine(_connectRoutine);
            _connectRoutine = null;
        }

        TearDownRealtimeClient();
        TeardownPushToTalk();

        _lastDeliveredMessageByOwner.Clear();
        _chatHistoryLines.Clear();
        _activeStreamLines.Clear();
        _agentParticipant = null;
    }

#if UNITY_EDITOR
    public void EditorForceDisconnect()
    {
        if (_connectRoutine != null)
        {
            StopCoroutine(_connectRoutine);
            _connectRoutine = null;
        }

        TearDownRealtimeClient();
    }
#endif

    private string ResolveConnectParticipantIdentity()
    {
        string baseIdentity = string.IsNullOrWhiteSpace(_participantIdentity) ? "unity-client" : _participantIdentity.Trim();

        if (_useStableUniqueIdentityPerInstall)
        {
            string installSuffix = GetOrCreateInstallIdentitySuffix();
            if (!string.IsNullOrWhiteSpace(installSuffix))
            {
                string stableIdentity = baseIdentity + "-" + installSuffix;
                baseIdentity = stableIdentity;
            }
        }

#if UNITY_EDITOR
        if (_useUniqueIdentityPerConnectInEditor)
        {
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            string uniqueIdentity = baseIdentity + "-e-" + suffix;
            return uniqueIdentity;
        }
#endif

        return baseIdentity;
    }

    private static string GetOrCreateInstallIdentitySuffix()
    {
        string stored = PlayerPrefs.GetString(InstallIdentityKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(stored))
            return stored;

        string generated = Guid.NewGuid().ToString("N").Substring(0, 8);
        PlayerPrefs.SetString(InstallIdentityKey, generated);
        PlayerPrefs.Save();
        return generated;
    }

    /// <summary>
    /// Registers handlers for text streams in the room, specifically for transcription results. 
    /// </summary>
    /// <param name="room">The room in which to register the text stream handlers.</param>
    private void RegisterTextHandlers(Room room)
    {
        if (room == null || string.IsNullOrWhiteSpace(_transcriptionTopic))
            return;

        room.RegisterTextStreamHandler(_transcriptionTopic, OnTranscriptionStreamOpened);
        _registeredTextTopics.Add(_transcriptionTopic);
    }

    /// <summary>
    /// Event fired when a new text stream is opened in the room for the registered transcription topic. This handler reads the stream content and updates the chat log with interim and final transcription results.
    /// </summary>
    /// <param name="reader">The text stream reader for the opened stream.</param>
    /// <param name="participantIdentity">The identity of the participant who opened the text stream.</param>
    private void OnTranscriptionStreamOpened(TextStreamReader reader, string participantIdentity) => StartCoroutine(ReadTranscriptionStream(reader, participantIdentity));

    /// <summary>
    /// Reads the content of a transcription text stream incrementally, updating the chat log in real-time as chunks arrive. The interim text is shown immediately while speaking; once the stream ends the entry is promoted to a final history message.
    /// </summary>
    /// <param name="reader">The text stream reader for the opened stream.</param>
    /// <param name="participantIdentity">The identity of the participant who opened the text stream.</param>
    private IEnumerator ReadTranscriptionStream(TextStreamReader reader, string participantIdentity)
    {
        string owner = NormalizeIdentity(participantIdentity);
        string segmentId = BuildTranscriptionStreamId(owner, reader);
        ulong timestamp = ResolveStreamTimestamp(reader);

        TextStreamReader.ReadIncrementalInstruction incremental = reader.ReadIncremental();
        StringBuilder accumulated = new StringBuilder();

        while (!incremental.IsEos)
        {
            incremental.Reset();
            yield return incremental;

            if (incremental.IsError)
            {
                Debug.LogWarning("Error reading incremental transcription stream from " + owner + ": " + incremental.Error?.Message);
                break;
            }

            string chunk = incremental.Text;
            if (string.IsNullOrEmpty(chunk))
                continue;

            accumulated.Append(chunk);
            string currentText = NormalizeMessageText(accumulated.ToString());
            if (string.IsNullOrEmpty(currentText))
                continue;

            _activeStreamLines[segmentId] = new ChatLineEntry
            {
                Owner = owner,
                Text = currentText,
                Timestamp = timestamp,
                Sequence = NextSequence()
            };
            RefreshChatLog();
        }

        string finalMessage = NormalizeMessageText(accumulated.ToString());
        _activeStreamLines.Remove(segmentId);

        if (!string.IsNullOrEmpty(finalMessage))
        {
            if (_lastDeliveredMessageByOwner.TryGetValue(owner, out string lastText) && string.Equals(lastText, finalMessage, StringComparison.Ordinal))
            {
                RefreshChatLog();
                reader.Dispose();
                yield break;
            }

            // Promote the active line directly to history so it stays in place
            _lastDeliveredMessageByOwner[owner] = finalMessage;
            string key = "final:" + owner + ":" + NextSequence();
            _chatHistoryLines.Add(new ChatLineEntry
            {
                Owner = owner,
                Key = key,
                Text = finalMessage,
                Timestamp = timestamp,
                Sequence = NextSequence()
            });
        }

        RefreshChatLog();

        reader.Dispose();
    }

    /// <summary>
    /// Normalizes a participant identity string for display purposes. If the identity is null, empty, or whitespace, it returns "unknown".
    /// </summary>
    /// <param name="participantIdentity">The identity of the participant to normalize.</param>
    /// <returns>A normalized participant identity string.</returns>
    private string NormalizeIdentity(string participantIdentity) => string.IsNullOrWhiteSpace(participantIdentity) ? "unknown" : participantIdentity;

    /// <summary>
    /// Appends a new line to the chat history with the specified owner, key, text, and timestamp. This is used for adding final transcription results or data messages to the chat log. 
    /// </summary>
    /// <param name="owner">The owner of the chat line.</param>
    /// <param name="key">The key associated with the chat line.</param>
    /// <param name="line">The text content of the chat line.</param>
    /// <param name="timestamp">The timestamp of the chat line.</param>
    private void AppendHistoryLine(string owner, string key, string line, ulong timestamp)
    {
        _chatHistoryLines.Add(new ChatLineEntry
        {
            Owner = owner,
            Key = key,
            Text = line,
            Timestamp = timestamp,
            Sequence = NextSequence()
        });
        RefreshChatLog();
    }

    /// <summary>
    /// Tries to append a chat message to the chat log if it is not a duplicate of the last delivered message from the same owner.
    /// </summary>
    /// <param name="owner">The owner of the chat message.</param>
    /// <param name="text">The text content of the chat message.</param>
    /// <param name="timestamp">The timestamp of the chat message.</param>
    private void TryAppendChatMessage(string owner, string text, ulong timestamp)
    {
        string normalizedOwner = NormalizeIdentity(owner);
        string normalizedText = NormalizeMessageText(text);
        if (string.IsNullOrEmpty(normalizedText))
            return;

        if (_lastDeliveredMessageByOwner.TryGetValue(normalizedOwner, out string lastText) && string.Equals(lastText, normalizedText, StringComparison.Ordinal))
            return;

        ClearMatchingActiveLines(normalizedOwner, normalizedText);
        _lastDeliveredMessageByOwner[normalizedOwner] = normalizedText;

        string key = "final:" + normalizedOwner + ":" + NextSequence();
        AppendHistoryLine(normalizedOwner, key, normalizedOwner + ": " + normalizedText, timestamp);
    }

    /// <summary>
    /// Normalizes a message text by trimming whitespace and ensuring it is not null or empty. 
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>A normalized message text.</returns>
    private string NormalizeMessageText(string text) => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    /// <summary>
    /// Builds a unique stream ID for a transcription stream based on the owner and available metadata from the text stream reader. 
    /// </summary>
    /// <param name="owner">The owner of the transcription stream.</param>
    /// <param name="reader">The text stream reader containing metadata.</param>
    /// <returns>A unique stream ID for the transcription stream.</returns>
    private string BuildTranscriptionStreamId(string owner, TextStreamReader reader)
    {
        if (reader?.Info?.Attributes != null && reader.Info.Attributes.TryGetValue("lk.segment_id", out string segmentId) && !string.IsNullOrWhiteSpace(segmentId))
            return "transcription:" + owner + ":" + segmentId;

        if (!string.IsNullOrWhiteSpace(reader?.Info?.Id))
            return "transcription:" + owner + ":" + reader.Info.Id;

        return "transcription:" + owner + ":" + reader?.Info?.Timestamp;
    }

    /// <summary>
    /// Determines if a given text stream reader corresponds to a final transcription result by checking for a specific attribute in the stream's metadata.
    /// </summary>
    /// <param name="reader">The text stream reader containing metadata.</param>
    /// <returns>True if the transcription is final.</returns>
    private bool IsFinalTranscription(TextStreamReader reader)
    {
        if (reader?.Info?.Attributes == null)
            return false;

        if (!reader.Info.Attributes.TryGetValue("lk.transcription_final", out string finalValue))
            return false;

        return string.Equals(finalValue, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Removes any active transcription lines from the chat log that match the specified owner and text. This is used to clear interim transcription results when a final result is received or when a duplicate message is detected.
    /// </summary>
    /// <param name="owner">The owner of the transcription lines to clear.</param>
    /// <param name="text">The text of the transcription lines to clear.</param>
    private void ClearMatchingActiveLines(string owner, string text)
    {
        List<string> streamIdsToRemove = new List<string>();
        foreach (KeyValuePair<string, ChatLineEntry> entry in _activeStreamLines)
        {
            string line = entry.Value.Text;
            string prefix = owner + ": ";

            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (string.Equals(NormalizeMessageText(line.Substring(prefix.Length)), text, StringComparison.Ordinal))
                streamIdsToRemove.Add(entry.Key);
        }

        if (streamIdsToRemove.Count == 0)
            return;

        foreach (string streamId in streamIdsToRemove)
            _activeStreamLines.Remove(streamId);

        RefreshChatLog();
    }

    /// <summary>
    /// Refreshes the chat log UI by combining and sorting the chat history lines and active transcription lines, then instantiating chat message UI elements for each entry.
    /// We might need more prefabs depending on the role of the participant later.
    /// </summary>
    private void RefreshChatLog()
    {
        if (chatMessage == null || chatContent == null)
            return;

        List<ChatLineEntry> entries = new List<ChatLineEntry>(_chatHistoryLines.Count + _activeStreamLines.Count);
        entries.AddRange(_chatHistoryLines);
        entries.AddRange(_activeStreamLines.Values);
        entries.Sort((left, right) =>
        {
            int timestampCompare = left.Timestamp.CompareTo(right.Timestamp);
            if (timestampCompare != 0)
                return timestampCompare;

            return left.Sequence.CompareTo(right.Sequence);
        });

        for (int i = chatContent.childCount - 1; i >= 0; i--)
            Destroy(chatContent.GetChild(i).gameObject);

        foreach (ChatLineEntry entry in entries)
        {
            if (entry.Owner.StartsWith("agent", StringComparison.OrdinalIgnoreCase))
            {
                ChatMessage botMessageItem = Instantiate(botChatMessage, chatContent);
                botMessageItem.SetMessage(entry.Text);
                continue;
            }

            ChatMessage messageItem = Instantiate(chatMessage, chatContent);
            messageItem.SetMessage(entry.Text);
        }
    }

    /// <summary>
    /// Resolves a timestamp for a given text stream reader by checking for a valid timestamp in the stream's metadata.
    /// </summary>
    /// <param name="reader">The text stream reader to resolve the timestamp for.</param>
    /// <returns>The resolved timestamp in milliseconds since the Unix epoch.</returns>
    private ulong ResolveStreamTimestamp(TextStreamReader reader)
    {
        if (reader != null && reader.Info != null && reader.Info.Timestamp != default)
            return (ulong)new DateTimeOffset(reader.Info.Timestamp.ToUniversalTime()).ToUnixTimeMilliseconds();

        return NextTimestamp();
    }

    /// <summary>
    /// Generates the next timestamp in milliseconds since the Unix epoch. This is used for chat messages that do not have an associated timestamp from a text stream, such as data messages or messages from unknown sources.
    /// </summary>
    /// <returns>The next timestamp in milliseconds since the Unix epoch.</returns>
    private ulong NextTimestamp() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Generates the next sequence number for chat messages. This is used to ensure a consistent ordering of messages in the chat log when multiple messages have the same timestamp.
    /// </summary>
    /// <returns>The next sequence number for chat messages.</returns>
    private long NextSequence()
    {
        _chatSequence += 1;
        return _chatSequence;
    }

    /// <summary>
    /// Compare the attributes including livekit agent state scheme
    /// </summary>
    /// <param name="participant">The participant connecting to the room</param> <returns>Return true if its an agent</returns>
    private bool LooksLikeAgent(RemoteParticipant participant)
    {
        if (participant == null)
            return false;

        if (participant.Attributes.TryGetValue("lk.agent.state", out _))
            return true;

        return participant.Identity.IndexOf("assistant", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Event fired when a new track is subscribed to in the room. This handler checks if the track is an audio track and, if so, creates an AudioSource in the scene to play the incoming audio.
    /// </summary>
    /// <param name="track">The track that was subscribed to.</param>
    /// <param name="publication">The publication associated with the track.</param>
    /// <param name="participant">The participant who published the track.</param>
    private void TrackSubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
    {
        if (track is RemoteVideoTrack videoTrack)
        {
            //If we ever use video data in the room
        }
        else if (track is RemoteAudioTrack audioTrack)
        {
            GameObject audObject = new GameObject(audioTrack.Sid);
            AudioSource source = audObject.AddComponent<AudioSource>();
            AudioStream stream = new AudioStream(audioTrack, source);
            _audioObjects[audioTrack.Sid] = audObject;
            _audioStreams[audioTrack.Sid] = stream;

            if (audioTrack.Name == "bridge-audio")
            {
                Debug.Log("Assigning audio source for agent participant " + participant.Identity);

                if (salsaLipSync == null)
                {
                    Debug.LogWarning("SALSA reference is missing; cannot assign bridge-audio analysis source.");
                    return;
                }

                SalsaExternalAnalysisBridge analysisBridge = audObject.GetComponent<SalsaExternalAnalysisBridge>();
                if (analysisBridge == null)
                    analysisBridge = audObject.AddComponent<SalsaExternalAnalysisBridge>();

                // LiveKit delivers PCM through the audio filter path; drive SALSA from the same signal.
                salsaLipSync.audioSrc = source;
                salsaLipSync.useExternalAnalysis = true;
                salsaLipSync.getExternalAnalysis = analysisBridge.GetAnalysisValue;
            }
        }
    }

    /// <summary>
    /// Publishes the local microphone audio track to the room. This method checks for available microphone devices, creates a LocalAudioTrack using the first available microphone, and publishes it to the room with specified encoding options.
    /// We might need to change this later to support push-to-talk.
    /// </summary>
    /// <param name="room">The room to which the local microphone audio track will be published.</param>
    private IEnumerator PublishMicrophone(Room room)
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone devices found. Connected to LiveKit without publishing local mic.");
            yield break;
        }

        CleanupLocalMicrophone();

        string localSid = LocalAudioSourceObjectName;

        GameObject audObject = new GameObject(localSid);
        _audioObjects[localSid] = audObject;

        MicrophoneSource rtcSource = new MicrophoneSource(Microphone.devices[0], _audioObjects[localSid]);
        LocalAudioTrack track = LocalAudioTrack.CreateAudioTrack(LocalAudioTrackName, rtcSource, room);
        TrackPublishOptions options = new TrackPublishOptions
        {
            AudioEncoding = new AudioEncoding
            {
                MaxBitrate = 64000
            },
            Source = TrackSource.SourceMicrophone
        };

        PublishTrackInstruction publish = room.LocalParticipant.PublishTrack(track, options);

        yield return publish;

        if (publish.IsError)
        {
            Debug.LogError("Failed to publish local microphone track: " + publish);
            rtcSource.Stop();
            rtcSource.Dispose();
            Destroy(audObject);
            _audioObjects.Remove(localSid);
            yield break;
        }

        _localAudioTrack = track;
        _localRtcAudioSource = rtcSource;
        Debug.Log("Track published! sid=" + _localAudioTrack.Sid);

        rtcSource.Start();
        _isMicrophoneCaptureActive = true;
        ApplyPushToTalkState();
    }
}
