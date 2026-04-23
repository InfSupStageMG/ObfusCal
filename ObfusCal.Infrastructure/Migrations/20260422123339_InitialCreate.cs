using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusySlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<string>(type: "text", nullable: false),
                    SourceEventId = table.Column<string>(type: "text", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusySlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarOwners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeerConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    BaseAddress = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarOwnerPeerMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerConnectionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwnerPeerMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarOwnerPeerMappings_CalendarOwners_CalendarOwnerId",
                        column: x => x.CalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarOwnerPeerMappings_PeerConnections_PeerConnectionId",
                        column: x => x.PeerConnectionId,
                        principalTable: "PeerConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_PeerId",
                table: "BusySlots",
                column: "PeerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerPeerMappings_CalendarOwnerId",
                table: "CalendarOwnerPeerMappings",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerPeerMappings_PeerConnectionId",
                table: "CalendarOwnerPeerMappings",
                column: "PeerConnectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusySlots");

            migrationBuilder.DropTable(
                name: "CalendarOwnerPeerMappings");

            migrationBuilder.DropTable(
                name: "CalendarOwners");

            migrationBuilder.DropTable(
                name: "PeerConnections");
        }
    }
}
