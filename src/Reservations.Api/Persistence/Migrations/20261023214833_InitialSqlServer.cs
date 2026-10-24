using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reservations.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reservations");

            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "reservations",
                columns: table => new
                {
                    ConsumerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.ConsumerName, x.MessageId });
                });

            migrationBuilder.CreateTable(
                name: "Outbox",
                schema: "reservations",
                columns: table => new
                {
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EffectKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnvelopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outbox", x => x.DeliveryId);
                    table.CheckConstraint("CK_Outbox_Json", "ISJSON([EnvelopeJson])=1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Destination_EffectKey",
                schema: "reservations",
                table: "Outbox",
                columns: new[] { "Destination", "EffectKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_PublishedAtUtc_OccurredAtUtc",
                schema: "reservations",
                table: "Outbox",
                columns: new[] { "PublishedAtUtc", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "reservations");

            migrationBuilder.DropTable(
                name: "Outbox",
                schema: "reservations");
        }
    }
}
