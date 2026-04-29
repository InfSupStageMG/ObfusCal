using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddObfuscationProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObfuscationProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Context = table.Column<int>(type: "integer", nullable: false),
                    RemoveTitle = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveDescription = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveLocation = table.Column<bool>(type: "boolean", nullable: false),
                    RemoveAttendees = table.Column<bool>(type: "boolean", nullable: false),
                    RoundTimes = table.Column<bool>(type: "boolean", nullable: false),
                    RoundingIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    MergeBlocks = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObfuscationProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObfuscationProfiles_CalendarOwners_CalendarOwnerId",
                        column: x => x.CalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObfuscationProfiles_CalendarOwnerId_Context",
                table: "ObfuscationProfiles",
                columns: new[] { "CalendarOwnerId", "Context" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "ObfuscationProfiles"
                ("Id", "CalendarOwnerId", "Context", "RemoveTitle", "RemoveDescription", "RemoveLocation", "RemoveAttendees", "RoundTimes", "RoundingIntervalMinutes", "MergeBlocks")
                SELECT
                    (substring(md5(o."Id"::text || '-Internal') from 1 for 8) || '-' ||
                     substring(md5(o."Id"::text || '-Internal') from 9 for 4) || '-' ||
                     substring(md5(o."Id"::text || '-Internal') from 13 for 4) || '-' ||
                     substring(md5(o."Id"::text || '-Internal') from 17 for 4) || '-' ||
                     substring(md5(o."Id"::text || '-Internal') from 21 for 12))::uuid,
                    o."Id",
                    0,
                    true,
                    true,
                    true,
                    true,
                    true,
                    15,
                    true
                FROM "CalendarOwners" o
                ON CONFLICT ("CalendarOwnerId", "Context") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ObfuscationProfiles"
                ("Id", "CalendarOwnerId", "Context", "RemoveTitle", "RemoveDescription", "RemoveLocation", "RemoveAttendees", "RoundTimes", "RoundingIntervalMinutes", "MergeBlocks")
                SELECT
                    (substring(md5(o."Id"::text || '-Client') from 1 for 8) || '-' ||
                     substring(md5(o."Id"::text || '-Client') from 9 for 4) || '-' ||
                     substring(md5(o."Id"::text || '-Client') from 13 for 4) || '-' ||
                     substring(md5(o."Id"::text || '-Client') from 17 for 4) || '-' ||
                     substring(md5(o."Id"::text || '-Client') from 21 for 12))::uuid,
                    o."Id",
                    1,
                    true,
                    true,
                    true,
                    true,
                    true,
                    15,
                    true
                FROM "CalendarOwners" o
                ON CONFLICT ("CalendarOwnerId", "Context") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObfuscationProfiles");
        }
    }
}
