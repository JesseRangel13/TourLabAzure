using Azure.Messaging.ServiceBus;
namespace TourLab.Azure.Tests;

// Keep unrelated PeekLocks only for this bounded test, then abandon them. Never
// complete, dead-letter, purge or apply another profile's traffic just to find a probe.
internal sealed class BrokerSelection(ServiceBusReceiver receiver) : IAsyncDisposable
{
    private readonly List<ServiceBusReceivedMessage> retained = [];
    public async Task<ServiceBusReceivedMessage?> ReceiveAsync(string logicalId, TimeSpan wait, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow + wait;
        while (DateTimeOffset.UtcNow < until)
        {
            if (retained.Count >= 64) throw new InvalidOperationException("Retained traffic exceeds the bounded test selection; no message was discarded.");
            var remaining = until - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) break;
            var message = await receiver.ReceiveMessageAsync(remaining, ct);
            if (message is null) return null;
            if (message.MessageId == logicalId ||
                message.ApplicationProperties.TryGetValue("originalLogicalMessageId", out var original) && original?.ToString() == logicalId)
                return message;
            retained.Add(message);
        }
        return null;
    }
    public async ValueTask DisposeAsync()
    {
        using var release = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var message in retained)
        {
            try { await receiver.AbandonMessageAsync(message, cancellationToken: release.Token); }
            catch (ServiceBusException error) when (error.Reason == ServiceBusFailureReason.MessageLockLost) { }
        }
    }
}
