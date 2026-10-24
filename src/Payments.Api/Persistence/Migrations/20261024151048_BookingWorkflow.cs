using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audit",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faults",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remaining = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NextReconcileAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReconcileAttempts = table.Column<int>(type: "int", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.CheckConstraint("CK_Payment", "[AmountMinor]>0 AND [Currency]='MXN' AND [Version]>0");
                });

            migrationBuilder.CreateTable(
                name: "ProviderEffects",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeoutDelivered = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderEffects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderRefunds",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Failures = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderRefunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefundOperations",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastRetryVersion = table.Column<long>(type: "bigint", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundOperations", x => x.Id);
                    table.CheckConstraint("CK_Refund", "[AmountMinor]>0 AND [Currency]='MXN' AND [Version]>0");
                });

            migrationBuilder.InsertData(
                schema: "payments",
                table: "Faults",
                columns: new[] { "Id", "Mode", "Remaining" },
                values: new object[] { 1, "Success", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_Operations_Status_NextReconcileAtUtc_LeaseUntilUtc",
                schema: "payments",
                table: "Operations",
                columns: new[] { "Status", "NextReconcileAtUtc", "LeaseUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Operations_UserId_ReservationId",
                schema: "payments",
                table: "Operations",
                columns: new[] { "UserId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderRefunds_PaymentOperationId",
                schema: "payments",
                table: "ProviderRefunds",
                column: "PaymentOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundOperations_PaymentOperationId",
                schema: "payments",
                table: "RefundOperations",
                column: "PaymentOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundOperations_Status_NextAttemptAtUtc",
                schema: "payments",
                table: "RefundOperations",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Audit",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "Faults",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "Operations",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "ProviderEffects",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "ProviderRefunds",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "RefundOperations",
                schema: "payments");
        }
    }
}
