using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarOwnerAvailabilitySyncState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LastSyncSucceeded",
                table: "CalendarOwners",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAt",
                table: "CalendarOwners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalendarOwnerAvailabilitySlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    AttendeeEmails = table.Column<string[]>(type: "text[]", nullable: true),
                    Location = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwnerAvailabilitySlots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerAvailabilitySlots_CalendarOwnerId",
                table: "CalendarOwnerAvailabilitySlots",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerAvailabilitySlots_CalendarOwnerId_Start_End",
                table: "CalendarOwnerAvailabilitySlots",
                columns: new[] { "CalendarOwnerId", "Start", "End" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarOwnerAvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "LastSyncSucceeded",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "CalendarOwners");
        }
    }
}
