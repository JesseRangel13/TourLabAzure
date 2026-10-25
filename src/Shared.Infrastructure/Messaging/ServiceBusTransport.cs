using System.Diagnostics;
using TourLab.Azure.Infrastructure.Operations;
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
        if (!config.Enabled) throw new InvalidOperationException("Messaging is disabled.");
        var clientOptions = new ServiceBusClientOptions {
            TransportType = ServiceBusTransportType.AmqpTcp,
            RetryOptions = new ServiceBusRetryOptions { MaxRetries = 2, TryTimeout = TimeSpan.FromSeconds(5),
                Delay = TimeSpan.FromMilliseconds(200), MaxDelay = TimeSpan.FromSeconds(1) }
        };
        if (environment.IsDevelopment()) {
            var properties = ServiceBusConnectionStringProperties.Parse(config.ConnectionString);
            if (properties.Endpoint.Host is not ("localhost" or "127.0.0.1") || properties.Endpoint.Port != 5673 ||
                !string.IsNullOrEmpty(config.FullyQualifiedNamespace) ||
                !config.ConnectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only this lab's loopback emulator is permitted in Development.");
            client = new(config.ConnectionString, clientOptions);
        } else {
            TourLab.Azure.Infrastructure.Security.AzureSecurity.Require(string.IsNullOrEmpty(config.ConnectionString) &&
                TourLab.Azure.Infrastructure.Security.AzureSecurity.Namespace(config.FullyQualifiedNamespace),"UnsafeCloudMessaging");
            client = new(config.FullyQualifiedNamespace,TourLab.Azure.Infrastructure.Security.AzureSecurity.Credential(),clientOptions);
        }
    }
    public Task SendAsync(ClaimedDelivery delivery, CancellationToken ct)
    {
        var envelope = MessageCodec.Parse(delivery.EnvelopeJson);
        if (envelope.MessageId != delivery.MessageId || !MessageRoutes.Destinations(envelope.Type).Contains(delivery.Destination))
            throw new PoisonMessageException("OutboxRouteConflict");
        using var activity = LabTelemetry.Start("outbox.publish", ActivityKind.Producer, envelope.Traceparent, envelope.Tracestate);
        var sender = senders.GetOrAdd(delivery.Destination, name => new Lazy<ServiceBusSender>(() => client.CreateSender(name))).Value;
        var message = new ServiceBusMessage(new BinaryData(Encoding.UTF8.GetBytes(delivery.EnvelopeJson))) {
            MessageId = delivery.MessageId.ToString("D"), CorrelationId = envelope.CorrelationId.ToString("D"),
            Subject = envelope.Type, ContentType = "application/json", TimeToLive = TimeSpan.FromHours(1)
        };
        message.ApplicationProperties["type"] = envelope.Type;
        message.ApplicationProperties["schemaVersion"] = envelope.Version;
        return sender.SendMessageAsync(message, ct);
    }
    public Task SendReplayAsync(string body,string destination,Guid replayId,CancellationToken ct)
    {
        var envelope=MessageCodec.Parse(body);
        if(!MessageRoutes.Destinations(envelope.Type).Contains(destination,StringComparer.Ordinal))throw new PoisonMessageException("ReplayRouteConflict");
        var sender=senders.GetOrAdd(destination,name=>new Lazy<ServiceBusSender>(()=>client.CreateSender(name))).Value;
        var message=new ServiceBusMessage(body){MessageId="replay-"+Guid.NewGuid().ToString("N"),CorrelationId=envelope.CorrelationId.ToString("D"),
            Subject=envelope.Type,ContentType="application/json",TimeToLive=TimeSpan.FromHours(1)};
        message.ApplicationProperties["type"]=envelope.Type;message.ApplicationProperties["schemaVersion"]=envelope.Version;
        message.ApplicationProperties["replayId"]=replayId.ToString("D");message.ApplicationProperties["originalLogicalMessageId"]=envelope.MessageId.ToString("D");
        return sender.SendMessageAsync(message,ct);
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
public sealed class OutboxWorker(IServiceScopeFactory scopes, IOptions<MessagingOptions> options, ILogger<OutboxWorker> logger, WorkerStatus status) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        status.Update("outbox",options.Value.Enabled,options.Value.Enabled?"Starting":"Disabled");
        if (!options.Value.Enabled) return;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stop);
                budget.CancelAfter(TimeSpan.FromSeconds(25));

                if(await scope.ServiceProvider.GetRequiredService<ServiceOperations>().TakeFaultAsync("PauseOutbox",budget.Token)){
                    status.Update("outbox",true,"Paused");
                }else{
                    var dispatched=await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchOneAsync(budget.Token);
                    status.Update("outbox",true,"Running");if(dispatched)continue;
                }
            }
            catch (Exception error) when (!stop.IsCancellationRequested) { status.Update("outbox",true,"Failure",error.GetType().Name); logger.LogWarning("Outbox intent retained: {FailureType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromMilliseconds(300), stop);
        }
    }
}
public sealed class InboxWorker(IServiceScopeFactory scopes, IOptions<MessagingOptions> options, ServiceBusTransport transport,
    string schema, ILogger<InboxWorker> logger, WorkerStatus status)
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
                while(await scope.ServiceProvider.GetRequiredService<ServiceOperations>().TakeFaultAsync("PauseConsumption",budget.Token))
                    await Task.Delay(TimeSpan.FromSeconds(1),budget.Token);
                using var activity=LabTelemetry.Start("inbox.consume",ActivityKind.Consumer,envelope.Traceparent,envelope.Tracestate);
                using var logScope=logger.BeginScope(new Dictionary<string,object?>{["messageId"]=envelope.MessageId,["sagaId"]=envelope.SagaId,
                    ["traceId"]=Activity.Current?.TraceId.ToString()});
                await scope.ServiceProvider.GetRequiredService<IInboxConsumer>().ConsumeAsync(envelope, budget.Token);
                scope.ServiceProvider.GetRequiredService<WorkerStatus>().Update("inbox",true,"Running");
                logger.LogInformation("Committed message {MessageType}",envelope.Type);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken); // only after the Inbox transaction commits
            }
            catch (PoisonMessageException poison)
            {
                await args.DeadLetterMessageAsync(args.Message, poison.Message, "Unsupported or contradictory lab envelope.", args.CancellationToken);
            }
            // Other failures escape: SDK abandons/retries under MaxDeliveryCount. No settlement before commit.
        }
        Task Error(ProcessErrorEventArgs args) { status.Update("inbox",true,"Failure",args.Exception.GetType().Name); logger.LogWarning("Service Bus processing deferred: {FailureType}", args.Exception.GetType().Name); return Task.CompletedTask; }
        commands.ProcessMessageAsync += args => Handle(args, false);
        events.ProcessMessageAsync += args => Handle(args, true);
        commands.ProcessErrorAsync += Error; events.ProcessErrorAsync += Error;
        status.Update("inbox",true,"Starting");
        await commands.StartProcessingAsync(stop); await events.StartProcessingAsync(stop); status.Update("inbox",true,"Running");
        try { await Task.Delay(Timeout.InfiniteTimeSpan, stop); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { using var shutdown=new CancellationTokenSource(TimeSpan.FromSeconds(45)); await commands.StopProcessingAsync(shutdown.Token); await events.StopProcessingAsync(shutdown.Token); status.Update("inbox",true,"Stopped"); }
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
            if (!services.GetRequiredService<IOptions<MessagingOptions>>().Value.Enabled) { services.GetRequiredService<WorkerStatus>().Update("inbox",false,"Disabled"); return; }
            var inner = new InboxWorker(services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IOptions<MessagingOptions>>(), services.GetRequiredService<ServiceBusTransport>(),
                schema, services.GetRequiredService<ILogger<InboxWorker>>(), services.GetRequiredService<WorkerStatus>());
            await inner.RunAsync(stop);
        }
    }
}