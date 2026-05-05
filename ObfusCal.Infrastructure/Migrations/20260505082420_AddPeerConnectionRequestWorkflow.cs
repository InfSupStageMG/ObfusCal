using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerConnectionRequestWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientOrganisationName",
                table: "PeerConnections",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientOrganisationNameNormalized",
                table: "PeerConnections",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByCalendarOwnerId",
                table: "PeerConnections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PeerConnections",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.CreateIndex(
                name: "IX_PeerConnections_RequestedByCalendarOwnerId_ClientOrganisati~",
                table: "PeerConnections",
                columns: new[] { "RequestedByCalendarOwnerId", "ClientOrganisationNameNormalized" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PeerConnections_CalendarOwners_RequestedByCalendarOwnerId",
                table: "PeerConnections",
                column: "RequestedByCalendarOwnerId",
                principalTable: "CalendarOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PeerConnections_CalendarOwners_RequestedByCalendarOwnerId",
                table: "PeerConnections");

            migrationBuilder.DropIndex(
                name: "IX_PeerConnections_RequestedByCalendarOwnerId_ClientOrganisati~",
                table: "PeerConnections");

            migrationBuilder.DropColumn(
                name: "ClientOrganisationName",
                table: "PeerConnections");

            migrationBuilder.DropColumn(
                name: "ClientOrganisationNameNormalized",
                table: "PeerConnections");

            migrationBuilder.DropColumn(
                name: "RequestedByCalendarOwnerId",
                table: "PeerConnections");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PeerConnections");
        }
    }
}
