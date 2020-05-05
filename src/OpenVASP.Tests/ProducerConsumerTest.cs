using System.Threading;
using System.Threading.Tasks;
using OpenVASP.CSharpClient.Sessions;
using OpenVASP.Messaging;
using OpenVASP.Messaging.Messages;
using OpenVASP.Messaging.Messages.Entities;
using OpenVASP.Messaging.MessagingEngine;
using Xunit;
using Xunit.Abstractions;

namespace OpenVASP.Tests
{
    public class ProducerConsumerTest
    {
        private readonly ITestOutputHelper testOutputHelper;

        public ProducerConsumerTest(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void ProducerConsumerSampleTest()
        {
            var messageResolverBuilder = new MessageHandlerResolverBuilder();
            var eventHandle = new CountdownEvent(5);
            messageResolverBuilder.AddHandler(typeof(SessionRequestMessage), new SessionRequestMessageHandler(
                (message, token) =>
                {
                    eventHandle.Signal();
                    return Task.CompletedTask;
                }));
            var cancellationTokenSource = new CancellationTokenSource();
            using (var producerConsumerQueue =
                new ProducerConsumerQueue(messageResolverBuilder.Build(), cancellationTokenSource.Token))
            {
                var sessionRequestMessage = SessionRequestMessage.Create(
                    "123",
                    new HandShakeRequest("1", "1"),
                    new VaspInformation("1", "1", "1", null, null, null, null, ""));

                producerConsumerQueue.EnqueueAsync(sessionRequestMessage);
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage);
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage);
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage);
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage);

                eventHandle.Wait(1_000);
            }
        }
    }
}
