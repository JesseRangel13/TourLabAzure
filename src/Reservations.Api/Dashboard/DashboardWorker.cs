using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using TourLab.Azure.Contracts;
using TourLab.Azure.Infrastructure;
using TourLab.Azure.Infrastructure.Messaging;
using TourLab.Azure.Infrastructure.Operations;

namespace TourLab.Azure.Reservations.Dashboard;

// No SQL Inbox precedes Cosmos: durable version + ETag guards make redelivery safe.
public sealed class DashboardConsumer(IBookingProjection store)
{
    public async Task ConsumeAsync(IntegrationEnvelope<JsonElement> envelope,CancellationToken ct)
    {
        if(envelope.Type!="BookingSnapshotChanged")throw new PoisonMessageException("DashboardRouteConflict");
        await store.ApplyAsync(MessageCodec.Payload<BookingSnapshotChanged>(envelope),ct);
    }
}
public sealed class DashboardWorker(IServiceScopeFactory scopes,IOptions<DashboardOptions> options,
    IOptions<MessagingOptions> messaging,IServiceProvider services,WorkerStatus status,ILogger<DashboardWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var enabled=options.Value.Enabled&&messaging.Value.Enabled;
        status.Update("dashboard",enabled,enabled?"Starting":"Disabled");
        if(!enabled)return;
        var transport=services.GetRequiredService<ServiceBusTransport>();
        await using var processor=transport.Client.CreateProcessor(MessageRoutes.Topic,"dashboard-events",new ServiceBusProcessorOptions{
            AutoCompleteMessages=false,ReceiveMode=ServiceBusReceiveMode.PeekLock,MaxConcurrentCalls=2,PrefetchCount=0,MaxAutoLockRenewalDuration=TimeSpan.FromMinutes(2)
        });
        processor.ProcessMessageAsync+=async args=>{
            try
            {
                if(args.Message.Body.ToMemory().Length>256*1024)throw new PoisonMessageException("EnvelopeTooLarge");
                var envelope=MessageCodec.Parse(args.Message.Body.ToString());
                if(envelope.Type!="BookingSnapshotChanged"||args.Message.Subject!=envelope.Type||
                    !args.Message.ApplicationProperties.TryGetValue("type",out var type)||type?.ToString()!=envelope.Type)
                    throw new PoisonMessageException("DashboardRouteConflict");
                await using var scope=scopes.CreateAsyncScope();
                using var budget=CancellationTokenSource.CreateLinkedTokenSource(args.CancellationToken);budget.CancelAfter(TimeSpan.FromSeconds(25));
                using var activity=LabTelemetry.Start("dashboard.project",ActivityKind.Consumer,envelope.Traceparent,envelope.Tracestate);
                await scope.ServiceProvider.GetRequiredService<DashboardConsumer>().ConsumeAsync(envelope,budget.Token);
                await args.CompleteMessageAsync(args.Message,args.CancellationToken);
                status.Update("dashboard",true,"Running");
            }
            catch(PoisonMessageException error){await args.DeadLetterMessageAsync(args.Message,error.Message,"Invalid or contradictory booking snapshot.",args.CancellationToken);}
            // Transient failures escape and abandon/redeliver. Never acknowledge a failed Cosmos write.
        };
        processor.ProcessErrorAsync+=args=>{
            status.Update("dashboard",true,"Failure",args.Exception.GetType().Name);
            logger.LogWarning("Dashboard projection deferred; {FailureType}",args.Exception.GetType().Name);return Task.CompletedTask;
        };
        await processor.StartProcessingAsync(stop);status.Update("dashboard",true,"Running");
        try{await Task.Delay(Timeout.InfiniteTimeSpan,stop);}
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        finally{using var shutdown=new CancellationTokenSource(TimeSpan.FromSeconds(45));await processor.StopProcessingAsync(shutdown.Token);status.Update("dashboard",true,"Stopped");}
    }
}
