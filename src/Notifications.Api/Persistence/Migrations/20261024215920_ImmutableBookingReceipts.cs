using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImmutableBookingReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReceiptAttempts",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptJobs",
                schema: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceBusinessVersion = table.Column<long>(type: "bigint", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BlobKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadyAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptJobs", x => x.Id);
                    table.CheckConstraint("CK_ReceiptJobs_Json", "ISJSON([SnapshotJson])=1");
                    table.CheckConstraint("CK_ReceiptJobs_Status", "[Status] IN ('Pending','Ready','Conflict')");
                    table.CheckConstraint("CK_ReceiptJobs_Version", "[SourceBusinessVersion]>0 AND [Version]>0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAttempts_JobId_OccurredAtUtc",
                schema: "notifications",
                table: "ReceiptAttempts",
                columns: new[] { "JobId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptJobs_ReservationId_Kind_SourceBusinessVersion",
                schema: "notifications",
                table: "ReceiptJobs",
                columns: new[] { "ReservationId", "Kind", "SourceBusinessVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptAttempts",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "ReceiptJobs",
                schema: "notifications");
        }
    }
}
