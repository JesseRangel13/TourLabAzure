using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowTracing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Traceparent",
                schema: "payments",
                table: "RefundOperations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracestate",
                schema: "payments",
                table: "RefundOperations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Traceparent",
                schema: "payments",
                table: "Operations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tracestate",
                schema: "payments",
                table: "Operations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Traceparent",
                schema: "payments",
                table: "RefundOperations");

            migrationBuilder.DropColumn(
                name: "Tracestate",
                schema: "payments",
                table: "RefundOperations");

            migrationBuilder.DropColumn(
                name: "Traceparent",
                schema: "payments",
                table: "Operations");

            migrationBuilder.DropColumn(
                name: "Tracestate",
                schema: "payments",
                table: "Operations");
        }
    }
}
