using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reservations.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowTracing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Traceparent",
                schema: "reservations",
                table: "Sagas",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracestate",
                schema: "reservations",
                table: "Sagas",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Traceparent",
                schema: "reservations",
                table: "Sagas");

            migrationBuilder.DropColumn(
                name: "Tracestate",
                schema: "reservations",
                table: "Sagas");
        }
    }
}
