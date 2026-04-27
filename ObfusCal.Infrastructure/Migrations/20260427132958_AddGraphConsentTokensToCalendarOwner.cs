using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGraphConsentTokensToCalendarOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GraphAccessTokenProtected",
                table: "CalendarOwners",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GraphConsentGrantedAtUtc",
                table: "CalendarOwners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GraphRefreshTokenProtected",
                table: "CalendarOwners",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GraphTokenExpiresAtUtc",
                table: "CalendarOwners",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GraphTokenLastRefreshedAtUtc",
                table: "CalendarOwners",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraphAccessTokenProtected",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "GraphConsentGrantedAtUtc",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "GraphRefreshTokenProtected",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "GraphTokenExpiresAtUtc",
                table: "CalendarOwners");

            migrationBuilder.DropColumn(
                name: "GraphTokenLastRefreshedAtUtc",
                table: "CalendarOwners");
        }
    }
}
