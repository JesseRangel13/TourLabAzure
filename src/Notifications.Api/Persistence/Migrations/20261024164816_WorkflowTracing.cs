using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notifications.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowTracing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Traceparent",
                schema: "notifications",
                table: "Notifications",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracestate",
                schema: "notifications",
                table: "Notifications",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Traceparent",
                schema: "notifications",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Tracestate",
                schema: "notifications",
                table: "Notifications");
        }
    }
}
