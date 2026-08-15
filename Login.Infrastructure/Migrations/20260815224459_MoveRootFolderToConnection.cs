using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRootFolderToConnection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RootFolderId",
                table: "GoogleDriveConnection",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RootFolderId",
                table: "GoogleDriveConnection");
        }
    }
}
