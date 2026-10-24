using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TourImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TourImages",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TourId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TourImages", x => x.Id);
                    table.CheckConstraint("CK_TourImage", "[Status] IN ('Pending','Ready','DeletePending','Deleted') AND [ByteSize] BETWEEN 1 AND 2097152 AND [Version]>0 AND [Attempts]>=0");
                    table.ForeignKey(
                        name: "FK_TourImages_Tours_TourId",
                        column: x => x.TourId,
                        principalSchema: "catalog",
                        principalTable: "Tours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImageHistory",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageHistory_TourImages_ImageId",
                        column: x => x.ImageId,
                        principalSchema: "catalog",
                        principalTable: "TourImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageHistory_ImageId_Id",
                schema: "catalog",
                table: "ImageHistory",
                columns: new[] { "ImageId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TourImages_ObjectKey",
                schema: "catalog",
                table: "TourImages",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TourImages_Status_LeaseUntilUtc_Id",
                schema: "catalog",
                table: "TourImages",
                columns: new[] { "Status", "LeaseUntilUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TourImages_TourId",
                schema: "catalog",
                table: "TourImages",
                column: "TourId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageHistory",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TourImages",
                schema: "catalog");
        }
    }
}
