using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGraphGrantedScopesAndConsentModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GraphGrantedScopes",
                table: "CalendarOwners",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraphGrantedScopes",
                table: "CalendarOwners");
        }
    }
}
