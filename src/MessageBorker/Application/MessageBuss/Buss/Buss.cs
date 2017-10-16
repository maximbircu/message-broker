﻿using System;
using System.IO;
using MessageBuss.Brocker;
using MessageBuss.Brocker.Events;
using MessageBuss.Buss.Events;
using Messages;
using Messages.Payload;
using Messages.ServerInfo;
using Messages.Subscribe;
using Serialization;
using Serialization.Deserializer;
using Serialization.Serializer;

namespace MessageBuss.Buss
{
    public class Buss
    {
        private readonly BrockerClient _brocker;
        public event MessageReceivedHandler MessageReceived;

        public Buss(BrockerClient brocker)
        {
            _brocker = brocker;
            _brocker.MessageReceivedFromBrockerHandler += OnMessageReceivedFromBrocker;
        }

        #region Buss features

        public void Ping()
        {
            _brocker.Ping();
        }

        public void RequestServerInfo()
        {
            _brocker.SendOrEnqueue(new ServerGerneralInfoRequest());
        }

        public void Request(string queueName)
        {
            _brocker.SendOrEnqueue(new PayloadRequestMessage {QueueName = queueName});
        }

        public void Publish(string exchangeName, string routingKey, Message payload, bool isDurable = false)
        {
            _brocker.SendOrEnqueue(CreateRouteMessage(exchangeName, routingKey, payload, isDurable));
        }

        public void Fanout(Message payload, bool isDurable = false)
        {
            Publish(GetExchangeNameForType("Fanout"), "", payload, isDurable);
        }

        public void Direct(Message payload, string routingKey, bool isDurable = false)
        {
            Publish(GetExchangeNameForType("Direct"), routingKey, payload, isDurable);
        }

        public void Topic(Message payload, string routingKey, bool isDurable = false)
        {
            Publish(GetExchangeNameForType("Topic"), routingKey, payload, isDurable);
        }

        public void Subscribe(string queueName)
        {
           _brocker.Subscribe(queueName);
        }
        
        public void Unsubscribe()
        {
            //TODO implement unsubscribe feature.
        }

        public void Dispose()
        {
            _brocker.MessageReceivedFromBrockerHandler -= OnMessageReceivedFromBrocker;
            _brocker.Stop();
        }

        #endregion

        private void OnMessageReceivedFromBrocker(object sender, BrockerClientMessageReceivedEventArgs args)
        {
            var messageType = args.Message.MessageTypeName;
            if (messageType == typeof(PayloadMessage).Name)
            {
                var payloadMessage = args.Message as PayloadMessage;
                Message payload = null;
                if (payloadMessage != null)
                {
                    var deserializer = new DefaultDeserializer(new MemoryStream(payloadMessage.Payload));
                    payload = _brocker.WireProtocol.ReadMessage(deserializer);
                }
                MessageReceived?.Invoke(this, new MessegeReceviedEventArgs(payload));
            }
            else
            {
                MessageReceived?.Invoke(this, new MessegeReceviedEventArgs(args.Message));
            }
        }

        private string GetExchangeNameForType(string exchangeType)
        {
            string exchangeName;
            _brocker.DefautlExchanges.TryGetValue(exchangeType, out exchangeName);
            if (exchangeName == null)
            {
                throw new Exception($"Default exchange for {exchangeType} was not set !!!");
            }
            return exchangeName;
        }

        private Message CreateRouteMessage(string exchangeName, string routingKey, Message payload,
            bool isDurable = false)
        {
            var defaultMessage = new PayloadRouteMessage
            {
                IsDurable = isDurable,
                RoutingKey = routingKey,
                ExchangeName = exchangeName
            };
            _brocker.WireProtocol.WriteMessage(new DefaultSerializer(defaultMessage.MemoryStream), payload);
            return defaultMessage;
        }
    }
}