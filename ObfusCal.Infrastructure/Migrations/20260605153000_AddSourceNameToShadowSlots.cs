using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ObfusCal.Infrastructure.Persistence;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260605153000_AddSourceNameToShadowSlots")]
public partial class AddSourceNameToShadowSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SourceName",
            table: "CalendarSourceInstances",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceName",
            table: "CalendarOwnerAvailabilitySlots",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceName",
            table: "BusySlots",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SourceName",
            table: "CalendarSourceInstances");

        migrationBuilder.DropColumn(
            name: "SourceName",
            table: "CalendarOwnerAvailabilitySlots");

        migrationBuilder.DropColumn(
            name: "SourceName",
            table: "BusySlots");
    }
}

