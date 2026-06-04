using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddColorHexToAvailabilitySlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "CalendarSourceInstances",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "CalendarOwnerAvailabilitySlots",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "CalendarSourceInstances");

            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "CalendarOwnerAvailabilitySlots");
        }
    }
}
