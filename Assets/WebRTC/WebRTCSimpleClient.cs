//using UnityEngine;
//using Unity.WebRTC;
//using System;
//using System.Net.WebSockets;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Collections;

//public class WebRTCSimpleClient : MonoBehaviour
//{
//    private Dictionary<string, RTCPeerConnection> peerConnections = new Dictionary<string, RTCPeerConnection>();
//    private Dictionary<string, RTCDataChannel> dataChannels = new Dictionary<string, RTCDataChannel>();
//    private ClientWebSocket webSocket;
//    private string serverUrl = "ws://127.0.0.1:8080";
//    private CancellationTokenSource cancellationTokenSource;

//    void Start()
//    {
//        ConnectToSignalingServer();
//    }

//    void OnDestroy()
//    {
//        foreach (var peerConnection in peerConnections.Values)
//        {
//            peerConnection.Close();
//        }

//        //WebRTC.Dispose();

//        if (webSocket != null && webSocket.State == WebSocketState.Open)
//        {
//            webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None);
//        }
//        cancellationTokenSource.Cancel();
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

//    private void HandleSignalingMessage(SignalingMessage message)
//    {
//        if (!peerConnections.ContainsKey(message.id))
//        {
//            CreatePeerConnection(message.id);
//        }

//        var peerConnection = peerConnections[message.id];
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

//        peerConnections[id] = peerConnection;
//        dataChannels[id] = dataChannel;
//    }

//    private void HandleIceCandidate(string id, RTCIceCandidate candidate)
//    {
//        var message = new { type = "candidate", id = id, candidate = candidate.ToString() };
//        var jsonMessage = JsonUtility.ToJson(message);
//        StartCoroutine(SendWebSocketMessage(jsonMessage));
//        Debug.Log("New ICE candidate from " + id + ": " + candidate.ToString());
//    }

//    private void HandleDataChannel(string id, RTCDataChannel channel)
//    {
//        channel.OnMessage = message => Debug.Log("Received message from " + id + ": " + message);
//    }

//    public void CreateOffer(string id)
//    {
//        if (!peerConnections.ContainsKey(id))
//        {
//            CreatePeerConnection(id);
//        }

//        var peerConnection = peerConnections[id];
//        var offerOptions = new RTCOfferAnswerOptions();
//        StartCoroutine(CreateOfferCoroutine(id, offerOptions));
//    }

//    private IEnumerator CreateOfferCoroutine(string id, RTCOfferAnswerOptions offerOptions)
//    {
//        var peerConnection = peerConnections[id];
//        var createOfferTask = peerConnection.CreateOffer(ref offerOptions);
//        yield return new WaitUntil(() => createOfferTask.IsDone);

//        var desc = createOfferTask.Desc;
//        var setLocalDescTask = peerConnection.SetLocalDescription(ref desc);
//        yield return new WaitUntil(() => setLocalDescTask.IsDone);

//        var message = new { type = "offer", id = id, sdp = desc.sdp };
//        var jsonMessage = JsonUtility.ToJson(message);
//        StartCoroutine(SendWebSocketMessage(jsonMessage));

//        Debug.Log("Created offer for " + id + ": " + desc.sdp);
//    }

//    private IEnumerator HandleOffer(string id, string sdp)
//    {
//        var peerConnection = peerConnections[id];
//        var desc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
//        var setRemoteDescTask = peerConnection.SetRemoteDescription(ref desc);
//        yield return new WaitUntil(() => setRemoteDescTask.IsDone);

//        var answerOptions = new RTCOfferAnswerOptions();
//        var createAnswerTask = peerConnection.CreateAnswer(ref answerOptions);
//        yield return new WaitUntil(() => createAnswerTask.IsDone);

//        var answerDesc = createAnswerTask.Desc;
//        var setLocalDescTask = peerConnection.SetLocalDescription(ref answerDesc);
//        yield return new WaitUntil(() => setLocalDescTask.IsDone);

//        var message = new { type = "answer", id = id, sdp = answerDesc.sdp };
//        var jsonMessage = JsonUtility.ToJson(message);
//        StartCoroutine(SendWebSocketMessage(jsonMessage));

//        Debug.Log("Created answer for " + id + ": " + answerDesc.sdp);
//    }

//    private IEnumerator HandleAnswer(string id, string sdp)
//    {
//        var peerConnection = peerConnections[id];
//        var desc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
//        var setRemoteDescTask = peerConnection.SetRemoteDescription(ref desc);
//        yield return new WaitUntil(() => setRemoteDescTask.IsDone);
//    }

//    public void HandleNewCandidate(string id, string candidate)
//    {
//        var peerConnection = peerConnections[id];
//        var candidateInit = new RTCIceCandidateInit
//        {
//            candidate = candidate,
//            sdpMid = "",
//            sdpMLineIndex = 0
//        };
//        var iceCandidate = new RTCIceCandidate(candidateInit);

//        peerConnection.AddIceCandidate(iceCandidate);
//    }

//    private IEnumerator SendWebSocketMessage(string message)
//    {
//        if (webSocket.State == WebSocketState.Open)
//        {
//            var bytes = Encoding.UTF8.GetBytes(message);
//            var sendTask = webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
//            yield return new WaitUntil(() => sendTask.IsCompleted);
//        }
//    }

//    private class SignalingMessage
//    {
//        public string type;
//        public string id;
//        public string sdp;
//        public string candidate;
//    }
//}
