//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.WebSockets;
//using System.Text;
//using System.Threading;
//using Unity.WebRTC;
//using UnityEngine;
//using UnityEngine.Experimental.Rendering;
//using UnityEngine.UI;

//public class WebRTCClient : MonoBehaviour
//{
//    [Serializable]
//    public class RTCPeerConnectionPair
//    {
//        public string id;
//        public RTCPeerConnection connection;

//        public RTCPeerConnectionPair(string _id, RTCPeerConnection _connection)
//        {
//            id = _id;
//            connection = _connection;
//        }
//    }
//    [Serializable]
//    public class RTCDataChannelPair
//    {
//        public string id;
//        public RTCDataChannel channel;

//        public RTCDataChannelPair(string _id, RTCDataChannel _channel)
//        {
//            id = _id;
//            channel = _channel;
//        }
//    }

//    public List<RTCPeerConnectionPair> peerConnections = new List<RTCPeerConnectionPair>();
//    private List<RTCDataChannelPair> dataChannels = new List<RTCDataChannelPair>();

//    private CancellationTokenSource cancellationTokenSource;

//    [SerializeField] private RawImage sourceImage;
//    [SerializeField] private RawImage receiveImage;

//    private ClientWebSocket webSocket;
//    private string serverUrl = "ws://127.0.0.1:8080";

//    private RTCDataChannel dataChannel;
//    private List<RTCRtpSender> senders;

//    private VideoStreamTrack videoStreamTrack;
//    private MediaStream receiveVideoStream;

//    private DelegateOnIceConnectionChange onIceConnectionChange;
//    private DelegateOnIceCandidate onIceCandidate;
//    private DelegateOnDataChannel onDataChannel;
//    private DelegateOnTrack onTrack;
//    private DelegateOnNegotiationNeeded onNegotiationNeeded;

//    private WebCamTexture webCamTexture;
//    private Texture2D webcamCopyTexture;
//    private Coroutine coroutineConvertFrame;

//    private void Start()
//    {
//        ConnectToSignalingServer();

//        senders = new List<RTCRtpSender>();

//        //onNegotiationNeeded = () => { StartCoroutine(PeerNegotiationNeeded(connection)); };

//        StartCoroutine(WebRTC.Update());
//    }

//    private void ConnectToSignalingServer()
//    {
//        webSocket = new ClientWebSocket();
//        cancellationTokenSource = new CancellationTokenSource();
//        StartCoroutine(WebSocketConnectCoroutine());
//    }

//    private IEnumerator WebSocketConnectCoroutine()
//    {
//        var connectTask = webSocket.ConnectAsync(new Uri(serverUrl), cancellationTokenSource.Token);
//        yield return new WaitUntil(() => connectTask.IsCompleted);

//        if (connectTask.IsCompletedSuccessfully)
//        {
//            Debug.Log("Connected to signaling server");
//            StartCoroutine(ReceiveWebSocketMessages());

//            CreateOffer(Guid.NewGuid().ToString());
//        }
//        else
//        {
//            Debug.LogError("WebSocket connection failed");
//        }
//    }

//    private IEnumerator ReceiveWebSocketMessages()
//    {
//        var buffer = new byte[1024];
//        while (webSocket.State == WebSocketState.Open)
//        {
//            var receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
//            yield return new WaitUntil(() => receiveTask.IsCompleted);

//            var message = Encoding.UTF8.GetString(buffer, 0, receiveTask.Result.Count);
//            Debug.Log("message: " + message);
//            var signalingMessage = JsonUtility.FromJson<SignalingMessage>(message);
//            HandleSignalingMessage(signalingMessage);
//        }
//    }

//    private void OnDestroy()
//    {
//        if (webCamTexture != null)
//        {
//            webCamTexture.Stop();
//            webCamTexture = null;
//        }
//    }

//    private void AddTracks(RTCPeerConnection connection)
//    {
//        var videoSender = connection.AddTrack(videoStreamTrack);
//        senders.Add(videoSender);

//        if (WebRTCSettings.UseVideoCodec != null)
//        {
//            var codecs = new[] { WebRTCSettings.UseVideoCodec };
//            var transceiver = connection.GetTransceivers().First(t => t.Sender == videoSender);
//            transceiver.SetCodecPreferences(codecs);
//        }
//    }

//    private void RemoveTracks(RTCPeerConnection connection)
//    {
//        var transceivers = connection.GetTransceivers();
//        foreach (var transceiver in transceivers)
//        {
//            if (transceiver.Sender != null)
//            {
//                transceiver.Stop();
//                connection.RemoveTrack(transceiver.Sender);
//            }
//        }

//        senders.Clear();
//    }

//    private IEnumerator CaptureVideoStart(RTCPeerConnection connection)
//    {
//        if (WebCamTexture.devices.Length == 0)
//        {
//            Debug.LogFormat("WebCam device not found");
//            yield break;
//        }

//        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
//        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
//        {
//            Debug.LogFormat("authorization for using the device is denied");
//            yield break;
//        }

//        int width = WebRTCSettings.StreamSize.x;
//        int height = WebRTCSettings.StreamSize.y;
//        const int fps = 30;
//        WebCamDevice userCameraDevice = WebCamTexture.devices[0];
//        webCamTexture = new WebCamTexture(userCameraDevice.name, height, height, fps);
//        webCamTexture.Play();
//        yield return new WaitUntil(() => webCamTexture.didUpdateThisFrame);

//        /// Convert texture if the graphicsFormat is not supported.
//        /// Since Unity 2022.1, WebCamTexture.graphicsFormat returns R8G8B8A8_SRGB on Android Vulkan.
//        /// WebRTC doesn't support the graphics format when using Vulkan, and throw exception.
//        var supportedFormat = WebRTC.GetSupportedGraphicsFormat(SystemInfo.graphicsDeviceType);
//        if (webCamTexture.graphicsFormat != supportedFormat)
//        {
//            webcamCopyTexture = new Texture2D(width, height, supportedFormat, TextureCreationFlags.None);
//            videoStreamTrack = new VideoStreamTrack(webcamCopyTexture);
//            coroutineConvertFrame = StartCoroutine(ConvertFrame());
//        }
//        else
//        {
//            videoStreamTrack = new VideoStreamTrack(webCamTexture);
//        }

//        sourceImage.texture = webCamTexture;

//        AddTracks(connection);
//    }

//    IEnumerator ConvertFrame()
//    {
//        while (true)
//        {
//            yield return new WaitForEndOfFrame();
//            Graphics.ConvertTexture(webCamTexture, webcamCopyTexture);
//        }
//    }

//    private void HangUp()
//    {
//        if (webCamTexture != null)
//        {
//            webCamTexture.Stop();
//            webCamTexture = null;
//        }

//        if (coroutineConvertFrame != null)
//        {
//            StopCoroutine(coroutineConvertFrame);
//            coroutineConvertFrame = null;
//        }

//        receiveVideoStream?.Dispose();
//        receiveVideoStream = null;

//        videoStreamTrack?.Dispose();
//        videoStreamTrack = null;

//        sourceImage.texture = null;
//        receiveImage.texture = null;
//    }

//    private void CreatePeerConnection(string id)
//    {
//        var config = new RTCConfiguration
//        {
//            iceServers = new[] {
//                new RTCIceServer {
//                    urls = new[] { "stun:stun.l.google.com:19302" }
//                }
//            }
//        };

//        var peerConnection = new RTCPeerConnection(ref config);
//        var dataChannel = peerConnection.CreateDataChannel("dataChannel");

//        peerConnection.OnIceCandidate = candidate => HandleIceCandidate(id, candidate);
//        peerConnection.OnDataChannel = channel => HandleDataChannel(id, channel);
//        peerConnection.OnTrack = e =>
//        {
//            Debug.Log("ONTRACK: " + peerConnection + " => " + e);

//            if (e.Track is VideoStreamTrack video)
//            {
//                video.OnVideoReceived += tex =>
//                {
//                    receiveImage.texture = tex;
//                };
//            }
//        };

//        peerConnections.Add(new(id, peerConnection));
//        dataChannels.Add(new(id, dataChannel));

//        Debug.Log("Created peer connection: " + peerConnection);

//        StartCoroutine(CaptureVideoStart(peerConnection));
//    }

//    private void HandleSignalingMessage(SignalingMessage message)
//    {
//        if (peerConnections.Find(connection => connection.id == message.id) == null)
//        {
//            CreatePeerConnection(message.id);
//        }

//        Debug.Log("Message received: " + JsonUtility.ToJson(message));

//        var peerConnection = peerConnections.Find(connection => connection.id == message.id);
//        switch (message.type)
//        {
//            case "offer":
//                StartCoroutine(HandleOffer(message.id, message.sdp));
//                break;
//            case "answer":
//                StartCoroutine(HandleAnswer(message.id, message.sdp));
//                break;
//            case "candidate":
//                HandleNewCandidate(message.id, message.candidate);
//                break;
//        }
//    }

//    private void HandleIceCandidate(string id, RTCIceCandidate candidate)
//    {
//        SignalingMessage message = new SignalingMessage { type = "candidate", id = id, candidate = candidate.ToString() };
//        var jsonMessage = JsonUtility.ToJson(message);
//        StartCoroutine(SendWebSocketMessage(jsonMessage));
//        //Debug.Log("New ICE candidate from " + id + ": " + candidate.ToString());
//    }

//    private void HandleDataChannel(string id, RTCDataChannel channel)
//    {
//        channel.OnMessage = message => Debug.Log("Received message from " + id + ": " + message);
//    }

//    public void CreateOffer(string id)
//    {
//        if (peerConnections.Find(connection => connection.id == id) == null)
//        {
//            CreatePeerConnection(id);
//        }

//        var peerConnection = peerConnections.Find(connection => connection.id == id);
//        var offerOptions = new RTCOfferAnswerOptions();
//        StartCoroutine(CreateOfferCoroutine(id, offerOptions));
//    }

//    private IEnumerator CreateOfferCoroutine(string id, RTCOfferAnswerOptions offerOptions)
//    {
//        var peerConnection = peerConnections.Find(connection => connection.id == id);
//        var createOfferTask = peerConnection.connection.CreateOffer(ref offerOptions);
//        yield return new WaitUntil(() => createOfferTask.IsDone);

//        var desc = createOfferTask.Desc;
//        var setLocalDescTask = peerConnection.connection.SetLocalDescription(ref desc);
//        yield return new WaitUntil(() => setLocalDescTask.IsDone);

//        SignalingMessage message = new SignalingMessage { type = "offer", id = id, sdp = desc.sdp };
//        var jsonMessage = JsonUtility.ToJson(message);
//        StartCoroutine(SendWebSocketMessage(jsonMessage));

//        Debug.Log("Created offer for " + id + ": " + desc.sdp);
//    }

//    private IEnumerator HandleOffer(string id, string sdp)
//    {
//        var peerConnection = peerConnections.Find(connection => connection.id == id);
//        var desc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
//        var setRemoteDescTask = peerConnection.connection.SetRemoteDescription(ref desc);
//        yield return new WaitUntil(() => setRemoteDescTask.IsDone);

//        var answerOptions = new RTCOfferAnswerOptions();
//        var createAnswerTask = peerConnection.connection.CreateAnswer(ref answerOptions);
//        yield return new WaitUntil(() => createAnswerTask.IsDone);

//        var answerDesc = createAnswerTask.Desc;
//        var setLocalDescTask = peerConnection.connection.SetLocalDescription(ref answerDesc);
//        yield return new WaitUntil(() => setLocalDescTask.IsDone);

//        SignalingMessage message = new SignalingMessage { type = "answer", id = id, sdp = answerDesc.sdp };
//        var jsonMessage = JsonUtility.ToJson(message);
//        StartCoroutine(SendWebSocketMessage(jsonMessage));

//        Debug.Log("Created answer for " + id + ": " + answerDesc.sdp);
//    }

//    private IEnumerator HandleAnswer(string id, string sdp)
//    {
//        var peerConnection = peerConnections.Find(connection => connection.id == id);
//        var desc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
//        var setRemoteDescTask = peerConnection.connection.SetRemoteDescription(ref desc);
//        yield return new WaitUntil(() => setRemoteDescTask.IsDone);
//    }

//    public void HandleNewCandidate(string id, string candidate)
//    {
//        var peerConnection = peerConnections.Find(connection => connection.id == id);
//        var candidateInit = new RTCIceCandidateInit
//        {
//            candidate = candidate,
//            sdpMid = "",
//            sdpMLineIndex = 0
//        };
//        var iceCandidate = new RTCIceCandidate(candidateInit);

//        peerConnection.connection.AddIceCandidate(iceCandidate);

//        Debug.Log("Added candidate to peerconnection:" + peerConnection);
//    }

//    private IEnumerator SendWebSocketMessage(string message)
//    {
//        if (webSocket.State == WebSocketState.Open)
//        {
//            //Debug.Log("sending message: " + message);

//            var bytes = Encoding.UTF8.GetBytes(message);
//            var sendTask = webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
//            yield return new WaitUntil(() => sendTask.IsCompleted);
//        }
//    }

//    [Serializable]
//    private class SignalingMessage
//    {
//        public string type;
//        public string id;
//        public string sdp;
//        public string candidate;
//    }
//}

//internal static class WebRTCSettings
//{
//    public const int DefaultStreamWidth = 1280;
//    public const int DefaultStreamHeight = 720;

//    private static Vector2Int s_StreamSize = new Vector2Int(DefaultStreamWidth, DefaultStreamHeight);
//    private static RTCRtpCodecCapability s_useVideoCodec = null;

//    public static Vector2Int StreamSize
//    {
//        get { return s_StreamSize; }
//        set { s_StreamSize = value; }
//    }

//    public static RTCRtpCodecCapability UseVideoCodec
//    {
//        get { return s_useVideoCodec; }
//        set { s_useVideoCodec = value; }
//    }
//}