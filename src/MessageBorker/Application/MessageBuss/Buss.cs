﻿using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Net.Sockets;
using MessageBuss.Messages;
using Serialization;
using Serialization.Serializers;
using Serialization.WireProtocols;
using Transport.Connectors.Tcp;
using Transport.Events;
using ConnectionState = Transport.Events.ConnectionState;

namespace MessageBuss
{
    public class Buss
    {
        private static Buss _instance;
        public static Buss Instance => _instance ?? (_instance = new Buss());
        private readonly TcpConnector _tcpConnector;
        private readonly Queue<Message> _messagesToSend;
        private readonly IWireProtocol _wireProtocol;


        private Buss()
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //TODO read ip and port from settings
            socket.Connect(IPAddress.Parse("127.0.0.1"), 9000);
            _tcpConnector = new TcpConnector(socket, new DefaultWireProtocol());
            _tcpConnector.StateChanged += OnStateChange;
            _tcpConnector.StartAsync();
            _messagesToSend = new Queue<Message>();
            _wireProtocol = new DefaultWireProtocol();
        }

        private void OnStateChange(object sender, ConnectorStateChangeEventArgs args)
        {
            if (args.NewState == ConnectionState.Connected)
            {
                while (_messagesToSend.Count > 0)
                {
                    _tcpConnector.SendMessage(_messagesToSend.Dequeue());
                }
            }
        }

        public void Send(Message payload)
        {
            var message = CreateDefaultMessage(payload);
            if (_tcpConnector.ConnectionState == ConnectionState.Connected)
            {
                _tcpConnector.SendMessage(message);
            }
            else
            {
                _messagesToSend.Enqueue(message);
            }
        }

        private Message CreateDefaultMessage(Message payload)
        {
            var defaultMessage = new DefaultMessage();
            _wireProtocol.WriteMessage(new DefaultSerializer(defaultMessage.MemoryStream), payload);
            defaultMessage.ExchangeName = "test";
            defaultMessage.IsDurable = false;
            defaultMessage.RoutingKey = "test";
            return defaultMessage;
        }
    }
}