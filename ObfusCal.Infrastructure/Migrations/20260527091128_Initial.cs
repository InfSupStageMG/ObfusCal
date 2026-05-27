using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ObfusCal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusySlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerId = table.Column<string>(type: "text", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceEventId = table.Column<string>(type: "text", nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AttendeeEmails = table.Column<string[]>(type: "text[]", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusySlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarOwnerAvailabilitySlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    AttendeeEmails = table.Column<string[]>(type: "text[]", nullable: true),
                    Location = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SourceSlotsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwnerAvailabilitySlots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarOwners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EntraObjectId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CalendarSourcePluginId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ICloudCalendarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ICloudAppleIdProtected = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    ICloudAppSpecificPasswordProtected = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    GraphAccessTokenProtected = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    GraphRefreshTokenProtected = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    GraphGrantedScopes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    GraphTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GraphTokenLastRefreshedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GraphConsentGrantedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncSucceeded = table.Column<bool>(type: "boolean", nullable: true),
                    WriteBackEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WriteBackPlaceholderTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PluginAllowlistOverrides",
                columns: table => new
                {
                    PluginId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginAllowlistOverrides", x => x.PluginId);
                });

            migrationBuilder.CreateTable(
                name: "CalendarOwnerICalFeeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwnerICalFeeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarOwnerICalFeeds_CalendarOwners_CalendarOwnerId",
                        column: x => x.CalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "PeerConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<string>(type: "text", nullable: false),
                    BaseAddress = table.Column<string>(type: "text", nullable: false),
                    PinnedCertificateThumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClientCertificateThumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ApiKeyHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Scopes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientOrganisationName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClientOrganisationNameNormalized = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RequestedByCalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncSucceeded = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerConnections_CalendarOwners_RequestedByCalendarOwnerId",
                        column: x => x.RequestedByCalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarOwnerPeerMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarOwnerRef = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarOwnerPeerMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarOwnerPeerMappings_CalendarOwners_CalendarOwnerId",
                        column: x => x.CalendarOwnerId,
                        principalTable: "CalendarOwners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalendarOwnerPeerMappings_PeerConnections_PeerConnectionId",
                        column: x => x.PeerConnectionId,
                        principalTable: "PeerConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_CalendarOwnerId",
                table: "BusySlots",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_CreatedAtUtc",
                table: "BusySlots",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_PeerId",
                table: "BusySlots",
                column: "PeerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusySlots_PeerId_CalendarOwnerId",
                table: "BusySlots",
                columns: new[] { "PeerId", "CalendarOwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerAvailabilitySlots_CalendarOwnerId",
                table: "CalendarOwnerAvailabilitySlots",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerAvailabilitySlots_CalendarOwnerId_Start_End",
                table: "CalendarOwnerAvailabilitySlots",
                columns: new[] { "CalendarOwnerId", "Start", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerICalFeeds_CalendarOwnerId",
                table: "CalendarOwnerICalFeeds",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerPeerMappings_CalendarOwnerId",
                table: "CalendarOwnerPeerMappings",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwnerPeerMappings_PeerConnectionId",
                table: "CalendarOwnerPeerMappings",
                column: "PeerConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarOwners_EntraObjectId",
                table: "CalendarOwners",
                column: "EntraObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSourceInstances_CalendarOwnerId",
                table: "CalendarSourceInstances",
                column: "CalendarOwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSourceInstances_CalendarOwnerId_PluginId",
                table: "CalendarSourceInstances",
                columns: new[] { "CalendarOwnerId", "PluginId" });

            migrationBuilder.CreateIndex(
                name: "IX_ObfuscationProfiles_CalendarOwnerId_Context",
                table: "ObfuscationProfiles",
                columns: new[] { "CalendarOwnerId", "Context" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeerConnections_RequestedByCalendarOwnerId_ClientOrganisati~",
                table: "PeerConnections",
                columns: new[] { "RequestedByCalendarOwnerId", "ClientOrganisationNameNormalized" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusySlots");

            migrationBuilder.DropTable(
                name: "CalendarOwnerAvailabilitySlots");

            migrationBuilder.DropTable(
                name: "CalendarOwnerICalFeeds");

            migrationBuilder.DropTable(
                name: "CalendarOwnerPeerMappings");

            migrationBuilder.DropTable(
                name: "CalendarSourceInstances");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "ObfuscationProfiles");

            migrationBuilder.DropTable(
                name: "PluginAllowlistOverrides");

            migrationBuilder.DropTable(
                name: "PeerConnections");

            migrationBuilder.DropTable(
                name: "CalendarOwners");
        }
    }
}
