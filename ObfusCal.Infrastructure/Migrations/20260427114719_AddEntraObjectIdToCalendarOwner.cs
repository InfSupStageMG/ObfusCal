using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntraObjectIdToCalendarOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntraObjectId",
                table: "CalendarOwners",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwners_EntraObjectId",
                table: "CalendarOwners",
                column: "EntraObjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarOwners_EntraObjectId",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "EntraObjectId",
                table: "CalendarOwners");
        }
    }
}
