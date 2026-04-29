using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerScopeToBusySlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CalendarOwnerId",
                table: "BusySlots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_CalendarOwnerId",
                table: "BusySlots",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_PeerId_CalendarOwnerId",
                table: "BusySlots",
                columns: new[] { "PeerId", "CalendarOwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusySlots_CalendarOwnerId",
                table: "BusySlots");

            migrationBuilder.DropIndex(
                name: "IX_BusySlots_PeerId_CalendarOwnerId",
                table: "BusySlots");

            migrationBuilder.DropColumn(
                name: "CalendarOwnerId",
                table: "BusySlots");
        }
    }
}
