using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ObfusCal.Infrastructure.Persistence;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260430152000_AddCalendarSourcePluginIdToCalendarOwner")]
    public partial class AddCalendarSourcePluginIdToCalendarOwner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarSourcePluginId",
                table: "CalendarOwners",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalendarSourcePluginId",
                table: "CalendarOwners");
        }
    }
}


