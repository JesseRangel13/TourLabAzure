using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Notifications.Receipts;
namespace TourLab.Azure.Receipts;

public sealed class GenerateReceipt(ReceiptProcessor processor, ILogger<GenerateReceipt> logger)
{
    [Function("GenerateBookingReceipt")]
    public async Task Run(
        [ServiceBusTrigger("tourlab-azure-receipt-jobs", Connection = "ReceiptBus", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message, ServiceBusMessageActions actions, CancellationToken ct)
    {
        try
        {
            if (message.Body.ToMemory().Length > 65536) throw new PoisonMessageException("ReceiptEnvelopeTooLarge");
            var envelope = MessageCodec.Parse(message.Body.ToString());
            // A crashed invocation may retain its SQL lease for 60s. Wait within the renewed broker lock
            // rather than rapidly burning MaxDeliveryCount on a healthy competing invocation.
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(90));
            while (true)
            {
                try { await processor.ProcessAsync(envelope, budget.Token); break; }
                catch (ReceiptLeaseBusy) { await Task.Delay(TimeSpan.FromSeconds(2), budget.Token); }
            }
            await actions.CompleteMessageAsync(message, ct);
            logger.LogInformation("Receipt command {MessageId} completed after durable Ready", envelope.MessageId);
        }
        catch (PoisonMessageException error)
        {
            await actions.DeadLetterMessageAsync(message, deadLetterReason: error.Message,
                deadLetterErrorDescription: "Receipt command rejected; inspect owned durable audit.", cancellationToken: ct);
            logger.LogWarning("Receipt command rejected with {Code}", error.Message);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Receipt work retained for retry; {FailureType}", error.GetType().Name);
            // Do not expose SDK/SQL payloads or connection details through host exception logs.
            throw new ReceiptRetryRequired();
        }
    }
}
public sealed class ReceiptRetryRequired : Exception;