using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerTrustHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "PeerConnections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scopes",
                table: "PeerConnections",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "push_shadow_slots pull_busy_slots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "PeerConnections");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "PeerConnections");
        }
    }
}
