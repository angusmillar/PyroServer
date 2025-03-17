using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abm.Pyro.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceSettingsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceSetting",
                columns: table => new
                {
                    ServiceSettingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ServiceSettingTypeId = table.Column<int>(type: "int", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSetting", x => x.ServiceSettingId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceSettingType",
                columns: table => new
                {
                    ServiceSettingTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceSettingType", x => x.ServiceSettingTypeId);
                });

            migrationBuilder.InsertData(
                table: "ServiceSetting",
                columns: new[] { "ServiceSettingId", "IsCurrent", "Json", "LastUpdatedUtc", "ServiceSettingTypeId", "VersionId" },
                values: new object[] { 1, true, "{\"ProfilePackageServiceUrl\":null,\"TerminologyServiceUrl\":null,\"ValidateOnCreate\":false,\"ValidateOnUpdate\":false,\"VersionId\":\"1\",\"LastUpdated\":\"2025-03-01T00:00:00Z\"}", new DateTime(2025, 3, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, 1 });

            migrationBuilder.InsertData(
                table: "ServiceSettingType",
                columns: new[] { "ServiceSettingTypeId", "Name" },
                values: new object[] { 1, "FhirValidation" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSetting_ServiceSettingTypeId_IsCurrent",
                table: "ServiceSetting",
                columns: new[] { "ServiceSettingTypeId", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSetting_ServiceSettingTypeId_VersionId",
                table: "ServiceSetting",
                columns: new[] { "ServiceSettingTypeId", "VersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceSetting_ServiceSettingTypeId_VersionId_IsCurrent",
                table: "ServiceSetting",
                columns: new[] { "ServiceSettingTypeId", "VersionId", "IsCurrent" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceSetting");

            migrationBuilder.DropTable(
                name: "ServiceSettingType");
        }
    }
}
