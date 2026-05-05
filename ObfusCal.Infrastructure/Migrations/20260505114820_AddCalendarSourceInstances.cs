using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarSourceInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarSourceInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: true),
                    SecretDataJson = table.Column<string>(type: "character varying(32768)", maxLength: 32768, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSourceInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSourceInstances_CalendarOwners_CalendarOwnerId",
                        column: x => x.CalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSourceInstances_CalendarOwnerId",
                table: "CalendarSourceInstances",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSourceInstances_CalendarOwnerId_PluginId",
                table: "CalendarSourceInstances",
                columns: new[] { "CalendarOwnerId", "PluginId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarSourceInstances");
        }
    }
}
