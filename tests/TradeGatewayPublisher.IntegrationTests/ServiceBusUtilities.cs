using Azure.Messaging.ServiceBus;

namespace TradeGatewayPublisher.IntegrationTests
{
    public static class ServiceBusUtilities
    {
        public static async Task<bool> ServiceBusQueueContainsExpectedAsync(
            ServiceBusReceiver receiver,
            string expectedId,
            ITestOutputHelper testOutputHelper,
            CancellationToken cancellationToken
        )
        {
            try
            {


                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: 10,
                    maxWaitTime: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken
                );

                if (messages == null || messages.Count == 0)
                    return false;

                foreach (var msg in messages)
                {
                    var body = msg.Body.ToString();
                    try
                    {
                        await receiver.CompleteMessageAsync(msg, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // best-effort complete; ignore if emulator behaves differently
                        testOutputHelper.WriteLine(ex.Message);
                    }

                    if (body.Contains(expectedId, StringComparison.Ordinal))
                        return true;
                }
            }
            catch (Exception ex)
            {
                testOutputHelper.WriteLine(ex.Message);
                // If the emulator isn't reachable or the receiver fails, treat as not received for retry loop
                return false;
            }

            return false;
        }
    }
}
