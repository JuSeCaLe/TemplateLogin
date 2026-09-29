using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseFolderNumberAndFinancialFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FinancialInfo_PaymentAgreement",
                table: "Case",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinancialInfo_PortfolioSale",
                table: "Case",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderNumber",
                table: "Case",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancialInfo_PaymentAgreement",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "FinancialInfo_PortfolioSale",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "FolderNumber",
                table: "Case");
        }
    }
}
