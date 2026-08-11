using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseDriveFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriveFolderId",
                table: "Case",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriveFolderId",
                table: "Case");
        }
    }
}
