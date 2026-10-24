using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reservations.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TourName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Participants = table.Column<int>(type: "int", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    TotalAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.CheckConstraint("CK_Reservation", "[Participants] BETWEEN 1 AND 6 AND [UnitAmountMinor] > 0 AND CONVERT(decimal(38,0),[TotalAmountMinor]) = CONVERT(decimal(38,0),[UnitAmountMinor]) * [Participants] AND [Currency]='MXN' AND [Version]>0");
                });

            migrationBuilder.CreateTable(
                name: "TourProjections",
                schema: "reservations",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    PriceVersion = table.Column<long>(type: "bigint", nullable: false),
                    UnitAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourProjections", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRequests",
                schema: "reservations",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseStatus = table.Column<int>(type: "int", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRequests", x => new { x.UserId, x.OperationName, x.Key });
                    table.ForeignKey(
                        name: "FK_IdempotencyRequests_Reservations_ResourceId",
                        column: x => x.ResourceId,
                        principalSchema: "reservations",
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sagas",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HoldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefundOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoldExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefundRequired = table.Column<bool>(type: "bit", nullable: false),
                    RefundCompleted = table.Column<bool>(type: "bit", nullable: false),
                    SeatsReleaseRequired = table.Column<bool>(type: "bit", nullable: false),
                    SeatsReleased = table.Column<bool>(type: "bit", nullable: false),
                    PaymentRequested = table.Column<bool>(type: "bit", nullable: false),
                    PaymentResolved = table.Column<bool>(type: "bit", nullable: false),
                    CancellationRequested = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sagas", x => x.Id);
                    table.CheckConstraint("CK_Saga", "[Version]>0");
                    table.ForeignKey(
                        name: "FK_Sagas_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "reservations",
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "History",
                schema: "reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    To = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                    table.ForeignKey(
                        name: "FK_History_Sagas_SagaId",
                        column: x => x.SagaId,
                        principalSchema: "reservations",
                        principalTable: "Sagas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_History_SagaId_MessageId",
                schema: "reservations",
                table: "History",
                columns: new[] { "SagaId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_History_SagaId_OccurredAtUtc",
                schema: "reservations",
                table: "History",
                columns: new[] { "SagaId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRequests_ResourceId",
                schema: "reservations",
                table: "IdempotencyRequests",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Status",
                schema: "reservations",
                table: "Reservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId_CreatedAtUtc",
                schema: "reservations",
                table: "Reservations",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Sagas_HoldId",
                schema: "reservations",
                table: "Sagas",
                column: "HoldId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sagas_PaymentOperationId",
                schema: "reservations",
                table: "Sagas",
                column: "PaymentOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sagas_RefundOperationId",
                schema: "reservations",
                table: "Sagas",
                column: "RefundOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sagas_ReservationId",
                schema: "reservations",
                table: "Sagas",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sagas_State_DeadlineUtc",
                schema: "reservations",
                table: "Sagas",
                columns: new[] { "State", "DeadlineUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "History",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "IdempotencyRequests",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "TourProjections",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "Sagas",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "Reservations",
                schema: "reservations");
        }
    }
}
