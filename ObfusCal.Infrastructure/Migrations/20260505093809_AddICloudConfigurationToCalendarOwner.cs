using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddICloudConfigurationToCalendarOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ICloudAppSpecificPasswordProtected",
                table: "CalendarOwners",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ICloudAppleIdProtected",
                table: "CalendarOwners",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ICloudCalendarUrl",
                table: "CalendarOwners",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ICloudAppSpecificPasswordProtected",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "ICloudAppleIdProtected",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "ICloudCalendarUrl",
                table: "CalendarOwners");
        }
    }
}
