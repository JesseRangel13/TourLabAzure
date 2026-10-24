using System.Text.Json;
using TourLab.Azure.Catalog.Application;
using TourLab.Azure.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure.Messaging;

namespace TourLab.Azure.Catalog.Messaging;

public sealed class CatalogConsumer(CatalogDb db, InventoryHandlers inventory) : IInboxConsumer
{
    public async Task ConsumeAsync(IntegrationEnvelope<JsonElement> envelope, CancellationToken ct)
    {
        if (envelope.Type is not ("HoldSeats" or "ConfirmSeats" or "ReleaseSeats"))
        {
            throw new PoisonMessageException("CatalogHandlerNotImplemented");
        }

        await InboxTransaction.ProcessAsync(db, "catalog", "CatalogInventory", envelope, async token =>
        {
            var context = new CatalogMessageContext(envelope.MessageId, envelope.SagaId!.Value, envelope.CorrelationId);
            try
            {
                switch (envelope.Type)
                {
                    case "HoldSeats":
                        await inventory.HoldAsync(MessageCodec.Payload<HoldSeats>(envelope), context, token);
                        break;

                    case "ConfirmSeats":
                        await inventory.ConfirmAsync(MessageCodec.Payload<ConfirmSeats>(envelope), context, token);
                        break;

                    case "ReleaseSeats":
                        await inventory.ReleaseAsync(MessageCodec.Payload<ReleaseSeats>(envelope), context, token);
                        break;
                }
            }
            catch (CatalogProblem error)
            {
                throw new PoisonMessageException(error.Code);
            }
        }, ct);
    }
}

