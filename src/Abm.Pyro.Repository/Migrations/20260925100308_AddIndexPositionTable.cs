using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Abm.Pyro.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexPositionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexPosition",
                columns: table => new
                {
                    IndexPositionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Position = table.Column<Point>(type: "geography", nullable: false),
                    ResourceStoreId = table.Column<int>(type: "int", nullable: true),
                    SearchParameterStoreId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexPosition", x => x.IndexPositionId);
                    table.ForeignKey(
                        name: "FK_IndexPosition_ResourceStore_ResourceStoreId",
                        column: x => x.ResourceStoreId,
                        principalTable: "ResourceStore",
                        principalColumn: "ResourceStoreId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndexPosition_SearchParameterStore_SearchParameterStoreId",
                        column: x => x.SearchParameterStoreId,
                        principalTable: "SearchParameterStore",
                        principalColumn: "SearchParameterStoreId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexPosition_ResourceStoreId",
                table: "IndexPosition",
                column: "ResourceStoreId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexPosition_SearchParameterStoreId",
                table: "IndexPosition",
                column: "SearchParameterStoreId");

            migrationBuilder.Sql(
                "CREATE SPATIAL INDEX SPATIAL_IndexPosition_Position " +
                "ON IndexPosition(Position) USING GEOGRAPHY_AUTO_GRID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX SPATIAL_IndexPosition_Position ON IndexPosition;");

            migrationBuilder.DropTable(
                name: "IndexPosition");
        }
    }
}
