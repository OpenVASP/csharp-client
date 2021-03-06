﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using Xunit;

namespace OpenVASP.Tests
{
    public class ProducerConsumerTest
    {
        [Fact]
        public void ProducerConsumerSampleTest()
        {
            var messageResolverBuilder = new MessageHandlerResolverBuilder();
            var eventHandle = new CountdownEvent(5);
            messageResolverBuilder.AddHandler<SessionRequestMessage>(
                (message, token) =>
                {
                    eventHandle.Signal();
                    return Task.CompletedTask;
                });
            var cancellationTokenSource = new CancellationTokenSource();
            using (var producerConsumerQueue = new ProducerConsumerQueue(
                messageResolverBuilder.Build(),
                cancellationTokenSource.Token,
                new NullLogger<ProducerConsumerQueue>()))
            {
                var sessionRequestMessage = SessionRequestMessage.Create(
                    "123",
                    new HandShakeRequest("1", "1"),
                    new VaspInformation("1", "1", "1", null, null, null, null, ""));

                producerConsumerQueue.Enqueue(sessionRequestMessage);
                producerConsumerQueue.Enqueue(sessionRequestMessage);
                producerConsumerQueue.Enqueue(sessionRequestMessage);
                producerConsumerQueue.Enqueue(sessionRequestMessage);
                producerConsumerQueue.Enqueue(sessionRequestMessage);

                eventHandle.Wait(1_000);
            }
        }
    }
}
