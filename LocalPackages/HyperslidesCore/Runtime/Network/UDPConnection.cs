using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using NSYNK.HyperSlides.Runtime;
using UnityEngine;

namespace NSYNK.HyperSlides.Network
{
    public class UDPConnection : MonoBehaviour
    {
        public UdpClient udpClient;
        public bool connected = false;
        public XRNetworkObjects.MatchUpdate lastMatchUpdate, lastPlayerUpdate = null;

        private XRNetworkObjects.MatchUpdate checkMessage = null;
        private Thread listenThread;

        /// <summary>
        /// Connects to the match using UDP
        /// </summary>
        /// <param name="orkanIP"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool ConnectToMatch(string orkanIP, string port)
        {
            if (udpClient != null)
                udpClient.Close();

            connected = false;

            try
            {
                udpClient = new UdpClient();
                udpClient.Client.ReceiveBufferSize = 65536;
                udpClient.Connect(orkanIP, port != "" ? int.Parse(port) : 7777);

                CreateThreadForUDPReceiving();

                connected = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error creating UDP client: " + e.StackTrace, XRNetworkManager.Instance);
                connected = false;
            }

            return connected;
        }

        /// <summary>
        /// Creates a new thread to listen for incoming UDP messages from the server. 
        /// If there is already an active listening thread, it aborts the existing thread before starting a new one. 
        /// The new thread runs the ListenForMessages method, which continuously checks for incoming messages and updates the latestMessageBuffer and lastMessage variables accordingly. 
        /// The thread is set as a background thread and given the highest priority to ensure timely processing of incoming messages.
        /// </summary>
        private void CreateThreadForUDPReceiving()
        {
            if (listenThread != null && listenThread.IsAlive)
            {
                listenThread.Abort();
                listenThread = null;
            }

            listenThread = new Thread(ListenForMessages)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.Highest
            };

            listenThread.Start();
        }

        /// <summary>
        /// Leaves the match and closes the UDP connection
        /// </summary>
        public void LeaveMatch()
        {
            try
            {
                XRNetworkObjects.networkPlayer.Status = XRNetworkObjects.XRPlayer.MessageType.LEAVE;
                XRNetworkObjects.MatchUpdate messageObject = new()
                {
                    Status = XRNetworkObjects.MatchUpdate.MessageType.PLAYER_UPDATE,
                    Data = XRNetworkObjects.networkPlayer
                };

                byte[] data = MessagePackSerializer.Serialize(messageObject);
                udpClient.Send(data, data.Length);

                Disconnect();
            }
            catch (Exception e)
            {
                if (udpClient != null)
                    Debug.LogWarning("Error leaving match: " + e.StackTrace, XRNetworkManager.Instance);
            }
        }

        public void OnDisable() => Disconnect();
        public void OnApplicationQuit() => Disconnect();

        /// <summary>
        /// Closes the UDP connection and cleans up resources
        /// </summary>
        public void Disconnect()
        {
            if (udpClient != null)
                udpClient.Close();

            connected = false;
        }

        /// <summary>
        /// Sends a simple string message to the server. This can be used for connection checks or other simple commands. 
        /// The message is converted to a byte array using ASCII encoding before being sent over UDP.
        /// </summary>
        /// <param name="message"></param>
        private void SendData(string message)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            udpClient.Send(data, data.Length);
        }

        /// <summary>
        /// Listens for incoming messages from the server in an asynchronous loop. 
        /// When a message is received, it updates the latestMessageBuffer and lastMessage variables. 
        /// If there is an error while receiving messages, it logs a warning and continues listening.
        /// </summary>
        private void ListenForMessages()
        {
            while (connected)
            {
                try
                {
                    if (!CheckConnection())
                    {
                        _ = Debug.LogQueue("UDP connection lost!", XRNetworkManager.Instance);
                        connected = false;
                        break;
                    }

                    var result = udpClient.Receive(ref remoteEndPoint);

                    if (!ValidateMessageBuffer(result))
                        continue;

                    XRNetworkObjects.MatchUpdate incomingUpdate = MessagePackSerializer.Deserialize<XRNetworkObjects.MatchUpdate>(result);

                    switch (incomingUpdate.Status)
                    {
                        case XRNetworkObjects.MatchUpdate.MessageType.MATCH_UPDATE:
                            lastMatchUpdate = incomingUpdate;
                            break;
                        case XRNetworkObjects.MatchUpdate.MessageType.PLAYER_UPDATE:
                            lastPlayerUpdate = incomingUpdate;
                            break;
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        /// <summary>
        /// Validates the structure of the incoming message buffer to ensure it can be deserialized into a MatchUpdate object.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private bool ValidateMessageBuffer(byte[] buffer)
        {
            try
            {
                if (buffer == null || buffer.Length == 0)
                    return false;

                MessagePackReader reader = new MessagePackReader(buffer);
                return reader.TryReadArrayHeader(out int arrayLength) && arrayLength == 2;
            }
            catch
            {
                return false;
            }
        }

        DateTime lastCheckTime = DateTime.MinValue;
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// Checks the connection to the server by sending a connection check message at regular intervals.
        /// </summary>
        /// <returns></returns>
        bool CheckConnection()
        {
            if (DateTime.UtcNow - lastCheckTime < TimeSpan.FromSeconds(5))
                return true;

            lastCheckTime = DateTime.UtcNow;

            if (checkMessage == null)
                checkMessage = new XRNetworkObjects.MatchUpdate
                {
                    Status = XRNetworkObjects.MatchUpdate.MessageType.CONNECTION_CHECK,
                    Data = new XRNetworkObjects.ConnectionCheck()
                };

            try
            {
                byte[] data = MessagePackSerializer.Serialize(checkMessage);

                udpClient.Send(data, data.Length);
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}