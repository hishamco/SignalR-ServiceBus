// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.ServiceBus;

namespace Microsoft.AspNet.SignalR.ServiceBus
{
    /// <summary>
    /// Settings for the Service Bus scale-out message bus implementation.
    /// </summary>
    public class ServiceBusScaleoutConfiguration : ScaleoutConfiguration
    {
        private int _topicCount;

        public ServiceBusScaleoutConfiguration()
        {
            IdleSubscriptionTimeout = TimeSpan.FromHours(1);

            TopicCount = 5;
            BackoffTime = TimeSpan.FromSeconds(20);
            TimeToLive = TimeSpan.FromMinutes(1);
            MaximumMessageSize = 256 * 1024;
            OperationTimeout = null;
        }

        public ServiceBusScaleoutConfiguration(string connectionString, string topicPrefix)
            : this()
        {
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            if (String.IsNullOrEmpty(topicPrefix))
            {
                throw new ArgumentNullException(nameof(topixPrefix));
            }

            ConnectionString = connectionString;
            TopicPrefix = topicPrefix;
        }

        /// <summary>
        /// The Service Bus connection string to use.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The topic prefix to use. Typically represents the app name.
        /// This must be consistent between all nodes in the web farm.
        /// </summary>
        public string TopicPrefix { get; set; }

        /// <summary>
        /// The number of topics to send messages over. Using more topics reduces contention and may increase throughput.
        /// This must be consistent between all nodes in the web farm.
        /// Defaults to 5.
        /// </summary>
        public int TopicCount
        {
            get
            {
                return _topicCount;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _topicCount = value;
            }
        }

        /// <summary>
        /// Gets or sets the message�s time to live value. This is the duration after
        /// which the message expires, starting from when the message is sent to the
        /// Service Bus. Messages older than their TimeToLive value will expire and no
        /// longer be retained in the message store. Subscribers will be unable to receive
        /// expired messages.
        /// </summary>
        public TimeSpan TimeToLive { get; set; }

        /// <summary>
        /// Specifies the time duration after which an idle subscription is deleted
        /// </summary>
        public TimeSpan IdleSubscriptionTimeout { get; set; }

        /// <summary>
        /// Specifies the delay before we try again after an error
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Backoff")]
        public TimeSpan BackoffTime { get; set; }

        /// <summary>
        /// Gets or Sets the operation timeout for all Service Bus operations 
        /// </summary>
        public TimeSpan? OperationTimeout { get; set; }

        /// <summary>
        /// Gets or Sets the maximum message size (in bytes) that can be sent or received
        /// Default value is set to 256KB which is the maximum recommended size for Service Bus operations
        /// </summary>
        public int MaximumMessageSize { get; set; }

        /// <summary>
        /// Returns Service Bus connection string to use.
        /// </summary>
        public string BuildConnectionString()
        {
            if (OperationTimeout != null)
            {
                if (String.IsNullOrEmpty(ConnectionString))
                {
                    throw new InvalidOperationException("ConnectionString is invalid");
                }
                var connectionStringBuilder = new ServiceBusConnectionStringBuilder(ConnectionString);
                connectionStringBuilder.OperationTimeout = OperationTimeout.Value;
                return connectionStringBuilder.ToString();
            }

            return ConnectionString;
        }

        /// <summary>
        /// Gets or sets the retry policy for service bus
        /// Default value is RetryExponential.Default
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; }
    }
}

