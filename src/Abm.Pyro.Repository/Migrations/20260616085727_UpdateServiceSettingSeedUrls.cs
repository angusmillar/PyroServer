using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abm.Pyro.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateServiceSettingSeedUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ServiceSetting",
                keyColumn: "ServiceSettingId",
                keyValue: 1,
                column: "Json",
                value: "{\"ProfilePackageServiceUrl\":\"https://some-profile-package-service-url.com\",\"TerminologyServiceUrl\":\"https://some-terminology-service-url.com\",\"ValidateOnCreate\":false,\"ValidateOnUpdate\":false,\"VersionId\":\"1\",\"LastUpdated\":\"2025-03-01T00:00:00Z\"}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ServiceSetting",
                keyColumn: "ServiceSettingId",
                keyValue: 1,
                column: "Json",
                value: "{\"ProfilePackageServiceUrl\":null,\"TerminologyServiceUrl\":null,\"ValidateOnCreate\":false,\"ValidateOnUpdate\":false,\"VersionId\":\"1\",\"LastUpdated\":\"2025-03-01T00:00:00Z\"}");
        }
    }
}
