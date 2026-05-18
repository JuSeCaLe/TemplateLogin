using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogProcessType",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogProcessType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogStage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogProcessTypeId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogStage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogStage_CatalogProcessType_CatalogProcessTypeId",
                        column: x => x.CatalogProcessTypeId,
                        principalTable: "CatalogProcessType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogSubStage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CatalogStageId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogSubStage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogSubStage_CatalogStage_CatalogStageId",
                        column: x => x.CatalogStageId,
                        principalTable: "CatalogStage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogStage_CatalogProcessTypeId",
                table: "CatalogStage",
                column: "CatalogProcessTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogSubStage_CatalogStageId",
                table: "CatalogSubStage",
                column: "CatalogStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogSubStage");

            migrationBuilder.DropTable(
                name: "CatalogStage");

            migrationBuilder.DropTable(
                name: "CatalogProcessType");
        }
    }
}
