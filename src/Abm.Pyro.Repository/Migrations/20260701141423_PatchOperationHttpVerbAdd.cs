using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abm.Pyro.Repository.Migrations
{
    /// <inheritdoc />
    public partial class PatchOperationHttpVerbAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "HttpVerb",
                columns: new[] { "HttpVerbId", "Name" },
                values: new object[] { 5, "Patch" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "HttpVerb",
                keyColumn: "HttpVerbId",
                keyValue: 5);
        }
    }
}
