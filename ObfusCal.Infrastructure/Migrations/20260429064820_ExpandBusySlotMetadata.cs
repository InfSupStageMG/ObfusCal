using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandBusySlotMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "AttendeeEmails",
                table: "BusySlots",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "BusySlots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "BusySlots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "BusySlots",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendeeEmails",
                table: "BusySlots");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "BusySlots");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "BusySlots");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "BusySlots");
        }
    }
}
