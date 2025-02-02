﻿/*
 * Copyright (c) 2021-2023, Norsk Helsenett SF and contributors
 * See the file CONTRIBUTORS for details.
 *
 * This file is licensed under the MIT license
 * available at https://raw.githubusercontent.com/helsenorge/Helsenorge.Messaging/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Helsenorge.Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Helsenorge.Messaging.Amqp
{
    /// <summary>
    /// A factory for creating pooled Receiver and Sender links.
    /// </summary>
    public class LinkFactoryPool : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly AmqpSettings _settings;
        private readonly IDictionary<string, object> _applicationProperties;
        private readonly AmqpFactoryPool _factoryPool;
        private readonly AmqpReceiverPool _receiverPool;
        private readonly AmqpSenderPool _senderPool;

        /// <summary>Initializes a new instance of the <see cref="LinkFactoryPool" /> class with a <see cref="AmqpSettings"/> and a <see cref="ILogger"/>.</summary>
        /// <param name="settings">A <see cref="AmqpSettings"/> instance that contains the settings.</param>
        /// <param name="logger">An <see cref="ILogger"/> instance which will be used to log errors and information.</param>
        /// <param name="applicationProperties">A Dictionary with additional application properties which will be added to <see cref="Message"/>.</param>
        public LinkFactoryPool(ILogger logger, AmqpSettings settings, IDictionary<string, object> applicationProperties = null)
        {
            _logger = logger;
            _settings = settings;
            _applicationProperties = applicationProperties ?? new Dictionary<string, object>();

            _factoryPool = new AmqpFactoryPool(_settings, _applicationProperties);
            _receiverPool = new AmqpReceiverPool(_settings, _factoryPool);
            _senderPool = new AmqpSenderPool(_settings, _factoryPool);
        }

        /// <summary>Creates a pooled receiver link of type <see cref="IAmqpReceiver"/>.</summary>
        /// <param name="queue">The path to the queue you want to receive messages from.</param>
        /// <returns>A <see cref="IAmqpReceiver"/></returns>
        public Task<IAmqpReceiver> CreateCachedMessageReceiverAsync(string queue)
            => _receiverPool.CreateCachedMessageReceiverAsync(_logger, queue);

        /// <summary>Release a pooled receiver link tied to the 'queue'.</summary>
        /// <param name="queue">The queue address for the link we want to release</param>
        public Task ReleaseCachedMessageReceiverAsync(string queue)
            => _receiverPool.ReleaseCachedMessageReceiverAsync(_logger, queue);

        /// <summary>Creates a pooled sender link of type <see cref="IAmqpSender"/>.</summary>
        /// <param name="queue">The path to the queue you want to send messages to.</param>
        /// <returns>A <see cref="IAmqpSender"/></returns>
        public Task<IAmqpSender> CreateCachedMessageSenderAsync(string queue)
            => _senderPool.CreateCachedMessageSenderAsync(_logger, queue);

        /// <summary>Release a pooled sender link tied to the 'queue'.</summary>
        /// <param name="queue">The queue address for the link we want to release</param>
        public Task ReleaseCachedMessageSenderAsync(string queue)
            => _senderPool.ReleaseCachedMessageSenderAsync(_logger, queue);

        /// <summary>Creates a <see cref="IAmqpMessage"/>.</summary>
        /// <param name="fromHerId">The HER-id which is the receipient of the message</param>
        /// <param name="message">The outgoing message as an <see cref="OutgoingMessage"/>.</param>
        /// <param name="payload">The payload as a <see cref="Stream"/> object.</param>
        /// <returns>Returns a <see cref="IAmqpMessage"/>.</returns>
        public async Task<IAmqpMessage> CreateMessageAsync(int fromHerId, OutgoingMessage message, Stream payload)
        {
            using var payloadMemoryStream = new MemoryStream();
            await payload.CopyToAsync(payloadMemoryStream);

            var innerMessage = new Message
            {
                BodySection = new Data
                {
                    Binary = payloadMemoryStream.ToArray(),
                }
            };

            return new AmqpMessage(innerMessage)
            {
                MessageId = message.MessageId,
                MessageFunction = message.MessageFunction,
                ToHerId = message.ToHerId,
                FromHerId = fromHerId,
            };
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await _receiverPool.ShutdownAsync(_logger).ConfigureAwait(false);
            await _senderPool.ShutdownAsync(_logger).ConfigureAwait(false);
            await _factoryPool.ShutdownAsync(_logger).ConfigureAwait(false);
        }
    }
}
