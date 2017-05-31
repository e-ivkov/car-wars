using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System;
using System.IO;

namespace CarWars
{
    public class EnvironmentCommunicator : MonoBehaviour
    {

        public int port = 2851;
        public const int maxImageSize = 256;

        public Vector2[] Sensors = new Vector2[3];
        private CarController Car;

        private Socket listener;
        private Client client;

        private const byte REQUEST_READ_SENSORS = 1;
        private const byte REQUEST_WRITE_ACTION = 2;
        private const byte REQUEST_RESTART = 3;

        private class Client
        {

            public Client(Socket socket)
            {
                this.socket = socket;
            }

            public void BeginReceive(AsyncCallback callback)
            {
                socket.BeginReceive(buffer, 0, BufferSize, 0, callback, this);
            }

            public Socket socket;
            public const int BufferSize = 1024;
            public int bytesLeft = 0;
            public byte[] buffer = new byte[BufferSize];
            public volatile bool requestPending;
            public const int ResponseBufferSize = maxImageSize * maxImageSize + 16;
            public byte[] responseBuffer = new byte[ResponseBufferSize];
        }

        private int DeterminePort()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--port" && i + 1 < args.Length)
                    return Convert.ToInt32(args[i + 1]);
            }
            // or return default port
            return port;
        }

        void Start()
        {
            Car = GetComponent<CarController>();

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, DeterminePort());

            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(1);

                BeginAccept();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void BeginAccept()
        {
            listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            Debug.Log("Accepted new client");

            client = new Client(handler);
            client.BeginReceive(new AsyncCallback(ReadCallback));
        }

        private void ReadCallback(IAsyncResult ar)
        {
            int bytesRead = client.socket.EndReceive(ar);

            if (bytesRead > 0)
            {
                client.bytesLeft = bytesRead;
                client.requestPending = true;
            }
        }

        private void SendSensorData()
        {
            int responseSize = 4 * 8;
            BinaryWriter writer = new BinaryWriter(new MemoryStream(client.responseBuffer));
            // We can't transfer floating point values, so let's transmit velocity * 2^16
            writer.Write(IPAddress.HostToNetworkOrder((int)(Car.CurrentReward * 0xffff)));
            writer.Write(IPAddress.HostToNetworkOrder((int)((float)Car.CurrentSpeed * 0xffff)));
            writer.Write(IPAddress.HostToNetworkOrder((int)(transform.position.x * 0xffff)));
            writer.Write(IPAddress.HostToNetworkOrder((int)(transform.position.y * 0xffff)));
            writer.Write(IPAddress.HostToNetworkOrder((int)(transform.rotation.z * 0xffff)));
            Car.SetCollidersEnabled(false);
            foreach (var sensor in Sensors)
            {
                var sensorRay = Physics2D.Raycast(transform.position, sensor);
                writer.Write(IPAddress.HostToNetworkOrder((int)(sensorRay.distance * 0xffff)));
            }
            Car.SetCollidersEnabled(true);
            client.socket.BeginSend(client.responseBuffer, 0, responseSize, 0, new AsyncCallback(SendCallback), client);
        }

        private void ApplyAction(int action)
        {
            switch (action)
            {
                case 1:
                    Car.PhaseUpdate(0, 30, 0, false, 0, -1);
                    break;
                case 2:
                    Car.PhaseUpdate(0, -30, 0, false, 0, -1);
                    break;
                case 3:
                    Car.PhaseUpdate(-1, 0, 0, true, 10, -1);
                    break;
                case 4:
                    Car.PhaseUpdate(-1, 0, 0, true, -10, -1);
                    break;
                default:
                    Car.PhaseUpdate(-1, 0, 0, false, 0, -1);
                    break;
            }
        }

        void Update()
        {
            if (client != null && client.requestPending)
            {
                client.requestPending = false;
                BinaryReader reader = new BinaryReader(new MemoryStream(client.buffer));
                while (client.bytesLeft > 0)
                {
                    byte instruction = reader.ReadByte();
                    client.bytesLeft--;
                    switch (instruction)
                    {
                        case REQUEST_READ_SENSORS:
                            Debug.Log("Read message request");
                            SendSensorData();
                            break;
                        case REQUEST_WRITE_ACTION:
                            int action = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                            client.bytesLeft -= 4;
                            Debug.Log("Write message request");
                            ApplyAction(action);
                            break;
                        case REQUEST_RESTART:
                            Car.Reset();
                            Debug.Log("Restart request");
                            break;
                    }
                }
                // Listen for next message
                client.BeginReceive(new AsyncCallback(ReadCallback));
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            client.socket.EndSend(ar);
        }

        private void WriteCameraImage(BinaryWriter writer, Texture2D image)
        {
            for (int y = 0; y < image.height; y++)
            {
                for (int x = 0; x < image.width; x++)
                {
                    Color pixel = image.GetPixel(x, y);
                    byte grayscale = (byte)(pixel.grayscale * 255);
                    writer.Write(grayscale);
                }
            }
        }
    }
}