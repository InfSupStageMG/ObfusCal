using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ObfusCal.Infrastructure.Persistence;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260605170000_BackfillMissingSourceNameColumns")]
public partial class BackfillMissingSourceNameColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                -- This compensates for environments that already applied the earlier SourceName migration
                -- before it was expanded to cover all three tables.
                IF NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'CalendarSourceInstances'
                      AND column_name = 'SourceName') THEN
                    ALTER TABLE "CalendarSourceInstances" ADD COLUMN "SourceName" character varying(256);
                END IF;

                IF NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'CalendarOwnerAvailabilitySlots'
                      AND column_name = 'SourceName') THEN
                    ALTER TABLE "CalendarOwnerAvailabilitySlots" ADD COLUMN "SourceName" character varying(256);
                END IF;

                IF NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'BusySlots'
                      AND column_name = 'SourceName') THEN
                    ALTER TABLE "BusySlots" ADD COLUMN "SourceName" character varying(256);
                END IF;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "This repair migration is intentionally non-reversible because SourceName columns may already belong to previously applied schema changes.");
    }
}

