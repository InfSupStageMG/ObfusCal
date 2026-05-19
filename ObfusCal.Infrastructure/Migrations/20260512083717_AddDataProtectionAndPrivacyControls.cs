using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataProtectionAndPrivacyControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #83 - Data Protection and Privacy Controls
            // Adds CreatedAtUtc to BusySlot shadow-slot rows so the retention background
            // service can purge rows older than SyncOptions.ShadowSlotRetentionDays.
            // Column-level encryption (ApiKeyHash, SecretDataJson) is handled at the
            // application layer via EF Core value converters and requires no schema change.

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "BusySlots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTimeOffset.UtcNow);

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_CreatedAtUtc",
                table: "BusySlots",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusySlots_CreatedAtUtc",
                table: "BusySlots");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "BusySlots");
        }
    }
}
