using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reservations.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DashboardSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectionSequence",
                schema: "reservations",
                table: "Reservations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ProjectionSequence",
                schema: "reservations",
                table: "Reservations",
                column: "ProjectionSequence",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_ProjectionSequence",
                schema: "reservations",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "ProjectionSequence",
                schema: "reservations",
                table: "Reservations");
        }
    }
}
