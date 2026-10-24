using System.Text;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourLab.Azure.Infrastructure.Persistence;
namespace TourLab.Azure.Infrastructure.Messaging;

public interface IMessageTransport
{
    Task SendAsync(ClaimedDelivery delivery, CancellationToken ct);
}
public sealed class ServiceBusTransport : IMessageTransport, IAsyncDisposable
{
    private readonly ServiceBusClient client;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<ServiceBusSender>> senders = new(StringComparer.Ordinal);
    public ServiceBusClient Client => client;
    public ServiceBusTransport(IOptions<MessagingOptions> options, IHostEnvironment environment)
    {
        var config = options.Value;
        // This task authorizes local transport only. Cloud identity/configuration is a separate task.
        if (!config.Enabled || !environment.IsDevelopment()) throw new InvalidOperationException("Local messaging requires explicit Development enablement.");
        var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);
        if (properties.Endpoint.Host is not ("localhost" or "127.0.0.1") || properties.Endpoint.Port != 5673 ||
            !config.ConnectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only this lab's loopback Service Bus emulator is permitted.");
        client = new(config.ConnectionString, new ServiceBusClientOptions {
            TransportType = ServiceBusTransportType.AmqpTcp,
            RetryOptions = new ServiceBusRetryOptions { MaxRetries = 2, TryTimeout = TimeSpan.FromSeconds(5),
                Delay = TimeSpan.FromMilliseconds(200), MaxDelay = TimeSpan.FromSeconds(1) }
        });
    }
    public Task SendAsync(ClaimedDelivery delivery, CancellationToken ct)
    {
        var envelope = MessageCodec.Parse(delivery.EnvelopeJson);
        if (envelope.MessageId != delivery.MessageId || !MessageRoutes.Destinations(envelope.Type).Contains(delivery.Destination))
            throw new PoisonMessageException("OutboxRouteConflict");
        var sender = senders.GetOrAdd(delivery.Destination, name => new Lazy<ServiceBusSender>(() => client.CreateSender(name))).Value;
        var message = new ServiceBusMessage(new BinaryData(Encoding.UTF8.GetBytes(delivery.EnvelopeJson))) {
            MessageId = delivery.MessageId.ToString("D"), CorrelationId = envelope.CorrelationId.ToString("D"),
            Subject = envelope.Type, ContentType = "application/json", TimeToLive = TimeSpan.FromHours(1)
        };
        message.ApplicationProperties["type"] = envelope.Type;
        message.ApplicationProperties["schemaVersion"] = envelope.Version;
        return sender.SendMessageAsync(message, ct);
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var sender in senders.Values) await sender.Value.DisposeAsync();
        await client.DisposeAsync();
    }
}
public sealed class OutboxDispatcher(SqlOutboxLeases leases, IMessageTransport transport)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct, Func<CancellationToken,Task>? afterSend = null)
    {
        var owner = Guid.NewGuid();
        var batch = await leases.ClaimAsync(owner, 1, TimeSpan.FromSeconds(60), null, ct);
        if (batch.Count == 0) return false;
        var delivery = batch.Single();
        await transport.SendAsync(delivery, ct); // SQL claim committed; no SQL transaction spans send.
        if (afterSend is not null) await afterSend(ct); // private deterministic crash barrier
        await leases.MarkPublishedAsync(delivery.DeliveryId, owner, ct);
        return true;
    }
}
public sealed class OutboxWorker(IServiceScopeFactory scopes, IOptions<MessagingOptions> options, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!options.Value.Enabled) return;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stop);
                budget.CancelAfter(TimeSpan.FromSeconds(25));
                if (await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOneAsync(budget.Token)) continue;
            }
            catch (Exception error) when (!stop.IsCancellationRequested) { logger.LogWarning("Outbox intent retained: {FailureType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromMilliseconds(300), stop);
        }
    }
}
public sealed class InboxWorker(IServiceScopeFactory scopes, IOptions<MessagingOptions> options, ServiceBusTransport transport,
    string schema, ILogger<InboxWorker> logger)
{
    public async Task RunAsync(CancellationToken stop)
    {
        if (!options.Value.Enabled) return;
        var settings = new ServiceBusProcessorOptions {
            AutoCompleteMessages = false, ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentCalls = 4, PrefetchCount = 0, MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(2)
        };
        await using var commands = transport.Client.CreateProcessor(BusTopology.CommandQueue(schema), settings);
        await using var events = transport.Client.CreateProcessor(MessageRoutes.Topic, schema + "-events", settings);
        async Task Handle(ProcessMessageEventArgs args, bool eventPath)
        {
            try
            {
                if (args.Message.Body.ToMemory().Length > 256 * 1024) throw new PoisonMessageException("EnvelopeTooLarge");
                var envelope = MessageCodec.Parse(args.Message.Body.ToString());
                if (!BusTopology.Accepts(schema, eventPath, envelope.Type)) throw new PoisonMessageException("WrongEntityRoute");
                if (args.Message.Subject != envelope.Type || !args.Message.ApplicationProperties.TryGetValue("type", out var type) ||
                    !string.Equals(type?.ToString(), envelope.Type, StringComparison.Ordinal))
                    throw new PoisonMessageException("TransportTypeConflict");
                await using var scope = scopes.CreateAsyncScope();
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(args.CancellationToken);
                budget.CancelAfter(TimeSpan.FromSeconds(40));
                await scope.ServiceProvider.GetRequiredService<IInboxConsumer>().ConsumeAsync(envelope, budget.Token);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken); // only after the Inbox transaction commits
            }
            catch (PoisonMessageException poison)
            {
                await args.DeadLetterMessageAsync(args.Message, poison.Message, "Unsupported or contradictory lab envelope.", args.CancellationToken);
            }
            // Other failures escape: SDK abandons/retries under MaxDeliveryCount. No settlement before commit.
        }
        Task Error(ProcessErrorEventArgs args) { logger.LogWarning("Service Bus processing deferred: {FailureType}", args.Exception.GetType().Name); return Task.CompletedTask; }
        commands.ProcessMessageAsync += args => Handle(args, false);
        events.ProcessMessageAsync += args => Handle(args, true);
        commands.ProcessErrorAsync += Error; events.ProcessErrorAsync += Error;
        await commands.StartProcessingAsync(stop); await events.StartProcessingAsync(stop);
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stop); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { await commands.StopProcessingAsync(CancellationToken.None); await events.StopProcessingAsync(CancellationToken.None); }
    }
}
public static class MessagingRegistration
{
    public static void AddLabMessaging(this IServiceCollection services, string schema)
    {
        services.Configure<MessagingOptions>(options => { }); // bind in service host; disabled by default
        services.AddSingleton<ServiceBusTransport>();
        services.AddSingleton<IMessageTransport>(sp => sp.GetRequiredService<ServiceBusTransport>());
        services.AddScoped(sp => new SqlOutboxLeases(sp.GetRequiredService<IOptions<RuntimeDatabase>>().Value.ConnectionString, schema, sp.GetRequiredService<TimeProvider>()));
        services.AddScoped<OutboxDispatcher>();
        services.AddHostedService<OutboxWorker>();
        // Avoid constructing the transport while the explicitly disabled foundation profile is running.
        services.AddHostedService(sp => new ConditionalInboxWorker(sp, schema));
    }
    private sealed class ConditionalInboxWorker(IServiceProvider services, string schema) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            if (!services.GetRequiredService<IOptions<MessagingOptions>>().Value.Enabled) return;
            var inner = new InboxWorker(services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IOptions<MessagingOptions>>(), services.GetRequiredService<ServiceBusTransport>(),
                schema, services.GetRequiredService<ILogger<InboxWorker>>());
            await inner.RunAsync(stop);
        }
    }
}