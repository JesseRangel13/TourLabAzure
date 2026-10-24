using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationsRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DlqReplays",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    BodyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LogicalMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DlqReplays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationsAudit",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReplayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationsAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationsAudit_OccurredAtUtc",
                schema: "payments",
                table: "OperationsAudit",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DlqReplays",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "OperationsAudit",
                schema: "payments");
        }
    }
}
