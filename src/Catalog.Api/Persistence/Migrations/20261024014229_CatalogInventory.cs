using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tours",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tours", x => x.Id);
                    table.CheckConstraint("CK_Tour", "LEN(LTRIM(RTRIM([Name])))>0 AND [Version]>0");
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TourId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnitAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    AvailableSeats = table.Column<int>(type: "int", nullable: false),
                    PriceVersion = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.CheckConstraint("CK_Session", "[UnitAmountMinor]>0 AND [Currency]='MXN' AND [Capacity]>=0 AND [AvailableSeats]>=0 AND [AvailableSeats]<=[Capacity] AND [Version]>0 AND [PriceVersion]>0");
                    table.ForeignKey(
                        name: "FK_Sessions_Tours_TourId",
                        column: x => x.TourId,
                        principalSchema: "catalog",
                        principalTable: "Tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TourName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Participants = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UnitAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    TotalAmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.CheckConstraint("CK_Quote", "[Participants] BETWEEN 1 AND 6 AND [UnitAmountMinor]>0 AND [TotalAmountMinor]>0 AND [Currency]='MXN' AND LEN([UserId])>0 AND CONVERT(decimal(38,0),[TotalAmountMinor])=CONVERT(decimal(38,0),[UnitAmountMinor])*[Participants]");
                    table.ForeignKey(
                        name: "FK_Quotes_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "catalog",
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Holds",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SagaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Participants = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holds", x => x.Id);
                    table.CheckConstraint("CK_Hold", "[Version]>0 AND [Status] IN ('Held','Confirmed','Released','Expired','Rejected') AND (([SessionId] IS NULL AND [QuoteId] IS NULL AND [Participants]=0 AND [Status] IN ('Released','Rejected')) OR ([SessionId] IS NOT NULL AND [QuoteId] IS NOT NULL AND [ReservationId] IS NOT NULL AND [UserId] IS NOT NULL AND [Participants] BETWEEN 1 AND 6 AND [ExpiresAtUtc] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_Holds_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "catalog",
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Holds_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "catalog",
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holds_QuoteId",
                schema: "catalog",
                table: "Holds",
                column: "QuoteId",
                unique: true,
                filter: "[QuoteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Holds_SessionId",
                schema: "catalog",
                table: "Holds",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Holds_Status_ExpiresAtUtc",
                schema: "catalog",
                table: "Holds",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ExpiresAtUtc",
                schema: "catalog",
                table: "Quotes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_RequestId",
                schema: "catalog",
                table: "Quotes",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SessionId",
                schema: "catalog",
                table: "Quotes",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TourId_StartsAtUtc",
                schema: "catalog",
                table: "Sessions",
                columns: new[] { "TourId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tours_Active_Name_Id",
                schema: "catalog",
                table: "Tours",
                columns: new[] { "Active", "Name", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Holds",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Sessions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Tours",
                schema: "catalog");
        }
    }
}
