using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Case",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    Process_Radicado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Process_ProcessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Process_Court = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Process_City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FinancialInfo_Capital = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FinancialInfo_Obligations = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinancialInfo_FngFag = table.Column<bool>(type: "bit", nullable: true),
                    Measures_Embargo = table.Column<bool>(type: "bit", nullable: true),
                    Measures_EmbargoDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Measures_RemanentEmbargo = table.Column<bool>(type: "bit", nullable: true),
                    Measures_RemanentEntity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Stages_PaymentOrder = table.Column<bool>(type: "bit", nullable: true),
                    Stages_PersonalNotification = table.Column<bool>(type: "bit", nullable: true),
                    Stages_PersonalNotificationDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Stages_FirstInstanceSentence = table.Column<bool>(type: "bit", nullable: true),
                    Stages_FirstInstanceDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Stages_SecondInstance = table.Column<bool>(type: "bit", nullable: true),
                    Stages_SecondInstanceDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Auction_AppraisalStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Auction_AppraisalDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Auction_AppraisalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Auction_AuctionStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Auction_AuctionDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Auction_Awarded = table.Column<bool>(type: "bit", nullable: true),
                    Auction_AwardDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Closure_TerminationDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Closure_TerminationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Closure_TitlesStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Closure_Delivery = table.Column<bool>(type: "bit", nullable: true),
                    Closure_DeliveryDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Closure_FileReturnStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Closure_FileReturnDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Case", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseParty",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<int>(type: "int", nullable: false),
                    Person = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProcessRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseParty", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseParty_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseProceduralNote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseProceduralNote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseProceduralNote_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseProcessStage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CaseId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubStageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Observation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseProcessStage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseProcessStage_Case_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Case",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseParty_CaseId",
                table: "CaseParty",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseProceduralNote_CaseId",
                table: "CaseProceduralNote",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseProcessStage_CaseId",
                table: "CaseProcessStage",
                column: "CaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseParty");

            migrationBuilder.DropTable(
                name: "CaseProceduralNote");

            migrationBuilder.DropTable(
                name: "CaseProcessStage");

            migrationBuilder.DropTable(
                name: "Case");
        }
    }
}
