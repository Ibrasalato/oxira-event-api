using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Oxira.Events.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertsSent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertsSent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertSubscribers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Sectors = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Token = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceMedium = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceCampaign = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceReferrer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceLanding = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UnsubscribedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertSubscribers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Page = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceMedium = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceCampaign = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceReferrer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceLanding = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Page = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Service = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventDate = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    StandSize = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Needs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreferredContact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Page = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DesignJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SourceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceMedium = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceCampaign = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceReferrer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceLanding = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StaffNote = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RenderLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Ip = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Lang = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Ok = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenderLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsageCounters",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageCounters", x => new { x.Key, x.Day });
                });

            migrationBuilder.CreateTable(
                name: "LeadFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeadId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Mime = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadFiles_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertsSent_EventKey",
                table: "AlertsSent",
                column: "EventKey");

            migrationBuilder.CreateIndex(
                name: "IX_AlertsSent_SubscriberId_EventKey_Stage",
                table: "AlertsSent",
                columns: new[] { "SubscriberId", "EventKey", "Stage" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertSubscribers_Email",
                table: "AlertSubscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertSubscribers_Token",
                table: "AlertSubscribers",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StartsAt",
                table: "Bookings",
                column: "StartsAt",
                unique: true,
                filter: "[Status] <> 'cancelled'");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_Id",
                table: "ChatMessages",
                columns: new[] { "SessionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadFiles_LeadId",
                table: "LeadFiles",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CreatedAt",
                table: "Leads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status_CreatedAt",
                table: "Leads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RenderLogs_CreatedAt",
                table: "RenderLogs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertsSent");

            migrationBuilder.DropTable(
                name: "AlertSubscribers");

            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "LeadFiles");

            migrationBuilder.DropTable(
                name: "RenderLogs");

            migrationBuilder.DropTable(
                name: "UsageCounters");

            migrationBuilder.DropTable(
                name: "Leads");
        }
    }
}
