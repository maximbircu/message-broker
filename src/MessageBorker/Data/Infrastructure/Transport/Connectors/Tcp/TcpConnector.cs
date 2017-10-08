﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using log4net;
using Serialization;
using Serialization.Deserializers;
using Serialization.Serializers;
using Transport.Events;

namespace Transport.Connectors.Tcp
{
    public class TcpConnector : ConnectionOrientedConnector
    {
        private const int DefaultMessageLength = 52428800;
        private readonly ILog _logger;
        private readonly Socket _socket;
        private readonly NetworkStream _networkStream;
        private readonly IWireProtocol _wireProtocol;
        private readonly int _maxMessageLength;

        public TcpConnector(Socket socket, IWireProtocol wireProtocol, int maxMessageLength = DefaultMessageLength)
        {
            _logger = LogManager.GetLogger(GetType());
            _socket = socket;
            _wireProtocol = wireProtocol;
            _networkStream = new NetworkStream(_socket);
            _maxMessageLength = maxMessageLength;
            Validate();
        }

        protected override void StartCommunication()
        {
            if (!_socket.Connected)
            {
                _logger.Error("Socket is not connected");
                throw new Exception("Tried to start communication with a TCP socket that is not connected.");
            }
            Task.Factory.StartNew(StartReceivingMessages);
        }

        protected override void StopCommunication()
        {
            if (!_socket.Connected) return;
            _socket.Shutdown(SocketShutdown.Send);
            _socket.Close();
            _socket.Dispose();
        }

        protected override void SendMessageInternal(Message message)
        {
            if (ConnectionState != ConnectionState.Connected)
            {
                _logger.Error("Socket is not connected");
                throw new Exception("Communicator's state is not connected. It can not send message.");
            }

            SendMessageToSocket(message);
        }

        private void StartReceivingMessages()
        {
            _logger.Debug("Started receiving messages");
            while (ConnectionState == ConnectionState.Connected || ConnectionState == ConnectionState.Connecting)
            {
                try
                {
                    var message = _wireProtocol.ReadMessage(new DefaultDeserializer(_networkStream));
                    OnMessageReceived(message);
                }
                catch (Exception)
                {
                    _logger.Error("Receiving message failed");
                    break;
                }
            }
            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to stop the connector");
                Console.WriteLine(ex);
                throw;
            }
        }

        private void SendMessageToSocket(Message message)
        {
            _logger.Info("Message is preparing to be sent to communicator");
            var memoryStream = new MemoryStream();
            //TODO add default serializers to settings
            _wireProtocol.WriteMessage(new DefaultSerializer(memoryStream), message);

            if (memoryStream.Length > _maxMessageLength)
            {
                _logger.Error("Message is too big to send.");
                throw new Exception("Message is too big to send.");
            }

            var sendBuffer = memoryStream.ToArray();
            var length = sendBuffer.Length;
            var totalSent = 0;
            while (totalSent < length)
            {
                var sent = _socket.Send(sendBuffer, totalSent, length - totalSent, SocketFlags.None);
                if (sent <= 0)
                {
                    _logger.Error("Message can not be sent via TCP socket. Only " + totalSent + " bytes of " +
                                  length + " bytes are sent.");
                }

                totalSent += sent;
            }
            _logger.Info("Message was sent");
        }

        private void Validate()
        {
            if (_socket == null || _wireProtocol == null || _networkStream == null)
            {
                _logger.Error("Entity vaildation error");
                throw new NullReferenceException();
            }
        }
    }
}