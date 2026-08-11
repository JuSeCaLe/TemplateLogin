using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandanteRoleAndCaseLink : Migration
    {
        // Demandante técnico usado para backfill de Case.DemandanteId cuando no se
        // puede resolver el demandante original desde CaseParty (ver Up()).
        private const string SinAsignarId = "00000000-0000-0000-0000-000000000001";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Case.DemandanteId nace nullable para poder hacer el backfill antes
            // de forzar NOT NULL + FK (ver más abajo).
            migrationBuilder.AddColumn<Guid>(
                name: "DemandanteId",
                table: "Case",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DemandanteId",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql($@"
                IF NOT EXISTS (SELECT 1 FROM [Demandante] WHERE [Id] = '{SinAsignarId}')
                    INSERT INTO [Demandante] ([Id], [Name], [Description], [Active], [CreatedAt])
                    VALUES ('{SinAsignarId}', 'Sin Asignar', 'Registro técnico para casos sin demandante identificado durante la migración', 0, SYSDATETIME());
            ");

            migrationBuilder.Sql($@"
                UPDATE c
                SET c.[DemandanteId] = COALESCE(m.[DemandanteId], '{SinAsignarId}')
                FROM [Case] c
                OUTER APPLY (
                    SELECT TOP 1 d.[Id] AS [DemandanteId]
                    FROM [CaseParty] cp
                    JOIN [Demandante] d ON TRY_CONVERT(uniqueidentifier, cp.[Person]) = d.[Id]
                    WHERE cp.[CaseId] = c.[Id] AND UPPER(cp.[ProcessRole]) = 'DEMANDANTE'
                ) m;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "DemandanteId",
                table: "Case",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Case_DemandanteId",
                table: "Case",
                column: "DemandanteId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoles_DemandanteId",
                table: "AspNetRoles",
                column: "DemandanteId",
                unique: true,
                filter: "[DemandanteId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoles_Demandante_DemandanteId",
                table: "AspNetRoles",
                column: "DemandanteId",
                principalTable: "Demandante",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Case_Demandante_DemandanteId",
                table: "Case",
                column: "DemandanteId",
                principalTable: "Demandante",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoles_Demandante_DemandanteId",
                table: "AspNetRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Case_Demandante_DemandanteId",
                table: "Case");

            migrationBuilder.DropIndex(
                name: "IX_Case_DemandanteId",
                table: "Case");

            migrationBuilder.DropIndex(
                name: "IX_AspNetRoles_DemandanteId",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "DemandanteId",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "DemandanteId",
                table: "AspNetRoles");

            // El Demandante centinela "Sin Asignar" se deja intacto (dato histórico
            // inofensivo); no se elimina para evitar tocar filas fuera del alcance
            // de esta migración.
        }
    }
}
