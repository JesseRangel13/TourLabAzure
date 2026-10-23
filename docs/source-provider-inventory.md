# PostgreSQL-specific source sites

Observed read-only. Port in AZ-002 onward; fresh SQL Server migrations are required. Source migrations were not copied.

C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\RuntimeDatabaseOptions.cs:1:using Npgsql;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\RuntimeDatabaseOptions.cs:13:            var value = new NpgsqlConnectionStringBuilder(Runtime);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\Persistence\TransportStorage.cs:42:        model.Entity<NotificationOutbox>(b=> {b.ToTable("Outbox"); b.HasKey(x=>x.DeliveryId); b.HasIndex(x=>new{x.Destination,x.EffectKey}).IsUnique(); b.Property(x=>x.EnvelopeJson).HasColumnType("jsonb"); b.HasIndex(x=>new{x.PublishedAtUtc,x.OccurredAtUtc});});
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:13:using Npgsql;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:37:    private async Task<NpgsqlConnection> Open(CancellationToken ct)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:39:        var connection = new NpgsqlConnection(owner.ConnectionString);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:43:    private NpgsqlCommand Sql(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:45:        var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 5 };
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:53:        await using var command = Sql(connection, $"UPDATE {Schema}.\"Faults\" SET \"Remaining\"=\"Remaining\"-1 WHERE \"Id\"=1 AND \"Mode\"=@mode AND \"Remaining\">0 RETURNING 1", ("mode",mode));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:111:                ON CONFLICT ("DeliveryId") DO UPDATE SET "Body"=@body,"Receipt"=@receipt,"ExpiresAtUtc"=@expires,"Finished"=false,"Replaying"=false,"ReplayUntilUtc"=NULL
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:113:                RETURNING "DeliveryId"
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:130:            await using var claim=Sql(connection,$"UPDATE {Schema}.\"DlqInspections\" SET \"Replaying\"=true,\"ReplayUntilUtc\"=CURRENT_TIMESTAMP+interval '60 seconds' WHERE \"DeliveryId\"=@id AND NOT \"Finished\" AND \"ExpiresAtUtc\">CURRENT_TIMESTAMP AND (NOT \"Replaying\" OR \"ReplayUntilUtc\"<CURRENT_TIMESTAMP) RETURNING \"Body\",\"Receipt\"",("id",id));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:163:    private async Task Audit(NpgsqlConnection connection,string actor,string action,Guid? id,CancellationToken ct)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Operations\ServiceOperations.cs:166:        command.Parameters["delivery"].NpgsqlDbType=NpgsqlTypes.NpgsqlDbType.Uuid;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\Persistence\NotificationsDb.cs:49:    public NotificationsDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<NotificationsDb>().UseNpgsql("Host=localhost;Database=tourlab", o=>o.MigrationsHistoryTable("__EFMigrationsHistory","notifications")).Options);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\NotificationEndpoints.cs:14:        services.AddDbContextFactory<NotificationsDb>(o=>o.UseNpgsql(config.GetConnectionString("Runtime"),pg=>pg.MigrationsHistoryTable("__EFMigrationsHistory","notifications")));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\CatalogEndpoints.cs:21:        services.AddDbContext<CatalogDb>(o => o.UseNpgsql(
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Persistence\CatalogDb.cs:188:            b.Property(x => x.EnvelopeJson).HasColumnType("jsonb");
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Persistence\CatalogDb.cs:200:            .UseNpgsql("Host=localhost;Database=tourlab", o => o.MigrationsHistoryTable("__EFMigrationsHistory", "catalog"))
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:13:using Npgsql;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:148:                await using var connection = new NpgsqlConnection(options.Value.Runtime);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:150:                await using var command = new NpgsqlCommand("SELECT 1", connection);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\LabHosting.cs:154:            catch (NpgsqlException)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\LocalTransactions.cs:47:        // ON CONFLICT blocks behind a concurrent uncommitted winner without aborting this transaction.
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\LocalTransactions.cs:52:            ON CONFLICT ("ConsumerName", "MessageId") DO NOTHING
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:2:using Npgsql;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:3:using NpgsqlTypes;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:61:        await using var connection = new NpgsqlConnection(connectionString);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:67:        await using var command = new NpgsqlCommand($"""
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:76:                FOR UPDATE SKIP LOCKED 
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:85:            RETURNING o."DeliveryId", o."MessageId", o."Destination", o."EnvelopeJson"::text
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:91:        command.Parameters.Add(new NpgsqlParameter("selected", NpgsqlDbType.Uuid)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:95:        command.Parameters.Add(new NpgsqlParameter("only", NpgsqlDbType.Text)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:99:        command.Parameters.Add(new NpgsqlParameter("excluded", NpgsqlDbType.Text)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:121:        await using var connection = new NpgsqlConnection(connectionString);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Shared.Infrastructure\Messaging\OutboxDispatch.cs:124:        await using var command = new NpgsqlCommand($"""
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Notifications.Api\Application\NotificationProcessing.cs:192:                FOR UPDATE SKIP LOCKED 
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Images\TourImages.cs:173:              ORDER BY "CreatedAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1)
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Catalog.Api\Images\TourImages.cs:175:              FROM due WHERE i."Id"=due."Id" RETURNING i.*
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Application\ReservationHandlers.cs:5:using Npgsql;
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Application\ReservationHandlers.cs:183:        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Persistence\ReservationsDb.cs:146:            b.HasIndex(x => new { x.PublishedAtUtc, x.OccurredAtUtc }); b.Property(x => x.EnvelopeJson).HasColumnType("jsonb");
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Persistence\ReservationsDb.cs:156:        .UseNpgsql("Host=localhost;Database=tourlab", o => o.MigrationsHistoryTable("__EFMigrationsHistory", "reservations")).Options);
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\ReservationEndpoints.cs:17:        services.AddDbContext<ReservationsDb>(o => o.UseNpgsql(
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Reservations.Api\Application\SagaRecovery.cs:147:            FOR UPDATE SKIP LOCKED 
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Identity\IdentityDesignFactory.cs:9:        var options = new DbContextOptionsBuilder<IdentityDb>().UseNpgsql("Host=localhost;Database=tourlab",
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\PaymentEndpoints.cs:15:        services.AddDbContextFactory<PaymentsDb>(o=>o.UseNpgsql(config.GetConnectionString("Runtime"),pg=>pg.MigrationsHistoryTable("__EFMigrationsHistory","payments")));
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Gateway.Web\Identity\IdentityRegistration.cs:10:        services.AddDbContextFactory<IdentityDb>(o => o.UseNpgsql(
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\Application\PaymentProcessing.cs:304:                FOR UPDATE SKIP LOCKED 
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\Persistence\TransportStorage.cs:42:        model.Entity<PaymentOutbox>(b=> {b.ToTable("Outbox"); b.HasKey(x=>x.DeliveryId); b.HasIndex(x=>new{x.Destination,x.EffectKey}).IsUnique(); b.Property(x=>x.EnvelopeJson).HasColumnType("jsonb"); b.HasIndex(x=>new{x.PublishedAtUtc,x.OccurredAtUtc});});
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\Application\RefundProcessing.cs:68:                AND ({selected}::uuid IS NULL OR "Id"={selected}) ORDER BY "Id" FOR UPDATE SKIP LOCKED LIMIT 1
C:\Users\JesseRangel\Documents\ChatGPT\Tour Microservices/src\Payments.Api\Persistence\PaymentsDb.cs:57:    public PaymentsDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PaymentsDb>().UseNpgsql("Host=localhost;Database=tourlab", o=>o.MigrationsHistoryTable("__EFMigrationsHistory","payments")).Options);
