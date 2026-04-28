using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarOwnerICalFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarOwnerICalFeeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwnerICalFeeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarOwnerICalFeeds_CalendarOwners_CalendarOwnerId",
                        column: x => x.CalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerICalFeeds_CalendarOwnerId",
                table: "CalendarOwnerICalFeeds",
                column: "CalendarOwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarOwnerICalFeeds");
        }
    }
}
