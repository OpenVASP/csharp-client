using System.Threading;
using System.Threading.Tasks;
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
            using (var producerConsumerQueue =
                new ProducerConsumerQueue(messageResolverBuilder.Build(), cancellationTokenSource.Token))
            {
                var sessionRequestMessage = SessionRequestMessage.Create(
                    "123",
                    new HandShakeRequest("1", "1"),
                    new VaspInformation("1", "1", "1", null, null, null, null, ""));

                producerConsumerQueue.EnqueueAsync(sessionRequestMessage).GetAwaiter().GetResult();
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage).GetAwaiter().GetResult();
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage).GetAwaiter().GetResult();
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage).GetAwaiter().GetResult();
                producerConsumerQueue.EnqueueAsync(sessionRequestMessage).GetAwaiter().GetResult();

                eventHandle.Wait(1_000);
            }
        }
    }
}
