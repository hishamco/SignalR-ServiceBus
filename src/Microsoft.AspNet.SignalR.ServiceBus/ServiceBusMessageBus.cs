// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Framework.Logging;
using Microsoft.Framework.OptionsModel;
using Microsoft.ServiceBus.Messaging;

namespace Microsoft.AspNet.SignalR.ServiceBus
{
    /// <summary>
    /// Uses Windows Azure Service Bus topics to scale-out SignalR applications in web farms.
    /// </summary>
    public class ServiceBusMessageBus : ScaleoutMessageBus
    {
        private const string SignalRTopicPrefix = "SIGNALR_TOPIC";

        private ServiceBusConnectionContext _connectionContext;

        private ILogger _logger;

        private readonly ServiceBusConnection _connection;
        private readonly string[] _topics;

        public ServiceBusMessageBus(IStringMinifier stringMinifier,
                                     ILoggerFactory loggerFactory,
                                     IPerformanceCounterManager performanceCounterManager,
                                     IOptions<SignalROptions> optionsAccessor,
                                     IOptions<ServiceBusScaleoutConfiguration> scaleoutConfigurationAccessor)
            : base(stringMinifier, loggerFactory, performanceCounterManager, optionsAccessor, scaleoutConfigurationAccessor)
        {
            var configuration = scaleoutConfigurationAccessor.Options;

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if(String.IsNullOrEmpty(configuration.TopicPrefix))
            {
                throw new InvalidOperationException("TopixPrefix is invalid");
            }

            _logger = loggerFactory.Create<ServiceBusMessageBus>();

            _connection = new ServiceBusConnection(configuration, _logger);

            _topics = Enumerable.Range(0, configuration.TopicCount)
                                .Select(topicIndex => SignalRTopicPrefix + "_" + configuration.TopicPrefix + "_" + topicIndex)
                                .ToArray();

            _connectionContext = new ServiceBusConnectionContext(configuration, _topics, _logger, OnMessage, OnError, Open);

            ThreadPool.QueueUserWorkItem(Subscribe);
        }

        protected override int StreamCount
        {
            get
            {
                return _topics.Length;
            }
        }

        protected override Task Send(int streamIndex, IList<Message> messages)
        {
            var stream = ServiceBusMessage.ToStream(messages);

            TraceMessages(messages, "Sending");

            return _connectionContext.Publish(streamIndex, stream);
        }

        private void OnMessage(int topicIndex, IEnumerable<BrokeredMessage> messages)
        {
            if (!messages.Any())
            {
                // Force the topic to re-open if it was ever closed even if we didn't get any messages
                Open(topicIndex);
                return;
            }

            foreach (var message in messages)
            {
                using (message)
                {
                    ScaleoutMessage scaleoutMessage = ServiceBusMessage.FromBrokeredMessage(message);

                    TraceMessages(scaleoutMessage.Messages, "Receiving");

                    OnReceived(topicIndex, (ulong)message.EnqueuedSequenceNumber, scaleoutMessage);
                }
            }
        }

        private void Subscribe(object state)
        {
            _connection.Subscribe(_connectionContext);
        }

        private void TraceMessages(IList<Message> messages, string messageType)
        {
            if (!_logger.IsEnabled(LogLevel.Verbose))
            {
                return;
            }

            foreach (Message message in messages)
            {
                _logger.WriteVerbose("{0} {1} bytes over Service Bus: {2}", messageType, message.Value.Array.Length, message.GetString());
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (_connectionContext != null)
                {
                    _connectionContext.Dispose();
                }

                if (_connection != null)
                {
                    _connection.Dispose();
                }
            }
        }
    }
}

