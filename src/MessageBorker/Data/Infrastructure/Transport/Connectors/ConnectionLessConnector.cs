﻿using System.Net;
using log4net;
using Serialization;

namespace Transport.Connectors
{
    public abstract class ConnectionLessConnector : Connector, IConnectionLessConnector
    {
        private readonly ILog _logger;
        private readonly object _sendLock;
        protected bool IsAlive; 

        protected ConnectionLessConnector(long connectorId) : base(connectorId)
        {
            _logger = LogManager.GetLogger(GetType());
            _sendLock = new object();
            IsAlive = false;
        }
        
        #region Control IRun Methods

        public override void Start()
        {
            _logger.Info("Starting connection less comunication");
            lock (_sendLock)
            {
                if (IsAlive)
                {
                    _logger.Error("Connection less communication is already started");
                    return;
                }
                StartCommunication();
                IsAlive = true;
            }
        }

        public override void Stop()
        {
            _logger.Info("Connector is going to be disconnected"); 
            lock (_sendLock)
            {
                if (!IsAlive)
                {
                    _logger.Error("Connectiop less comunication is already stoped");
                    return;
                }
                StopCommunication();
                IsAlive = false;
            }
        }

        #endregion

        protected abstract void StartCommunication();

        protected abstract void StopCommunication();
        
        public void SendMessage(Message message, EndPoint endPoint)
        {
            lock (_sendLock)
            {
                SendMessageInternal(message, endPoint);
            }
        }
        
        protected abstract void SendMessageInternal(Message message, EndPoint endPoint);
    }
}