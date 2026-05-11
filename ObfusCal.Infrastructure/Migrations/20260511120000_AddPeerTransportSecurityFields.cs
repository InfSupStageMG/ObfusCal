using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ObfusCal.Infrastructure.Persistence;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260511120000_AddPeerTransportSecurityFields")]
    public partial class AddPeerTransportSecurityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinnedCertificateThumbprint",
                table: "PeerConnections",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientCertificateThumbprint",
                table: "PeerConnections",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinnedCertificateThumbprint",
                table: "PeerConnections");

            migrationBuilder.DropColumn(
                name: "ClientCertificateThumbprint",
                table: "PeerConnections");
        }
    }
}

