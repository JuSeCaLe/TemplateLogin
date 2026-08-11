using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDemandanteTableUseRolesDirectly : Migration
    {
        // Id fijo para el rol-demandante técnico que reemplaza al Demandante
        // centinela "Sin Asignar" (usado por la migración anterior para el
        // backfill de casos huérfanos). No tenía rol vinculado, así que se crea
        // uno nuevo aquí si hace falta.
        private const string SinAsignarRoleId = "00000000-0000-0000-0000-000000000002";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Columnas nuevas, nullable/temporales donde haga falta para el backfill.
            migrationBuilder.AddColumn<bool>(
                name: "IsDemandante",
                table: "AspNetRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DemandanteRoleId",
                table: "Case",
                type: "nvarchar(450)",
                nullable: true);

            // 2) Los roles "dda-<Nombre>" pasan a ser roles-demandante normales:
            // se marca IsDemandante=1 y se quita el prefijo "dda-" (4 caracteres)
            // del propio row (mismo Role.Id => AspNetUserRoles no se toca, las
            // asignaciones de usuarios sobreviven solas).
            migrationBuilder.Sql(@"
                UPDATE [AspNetRoles]
                SET [IsDemandante] = 1,
                    [Name] = SUBSTRING([Name], 5, LEN([Name]) - 4),
                    [NormalizedName] = UPPER(SUBSTRING([Name], 5, LEN([Name]) - 4))
                WHERE [DemandanteId] IS NOT NULL;
            ");

            // 3) El Demandante centinela "Sin Asignar" no tenía rol vinculado
            // (se excluía a propósito del seed anterior). Se crea el rol
            // equivalente si todavía no existe uno para él.
            migrationBuilder.Sql($@"
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [Id] = '{SinAsignarRoleId}')
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp], [IsDemandante], [Active], [CreatedAt], [Description])
                    VALUES ('{SinAsignarRoleId}', 'Sin Asignar', 'SIN ASIGNAR', CONVERT(nvarchar(36), NEWID()), 1, 0, SYSDATETIME(), 'Rol técnico para casos sin demandante identificado durante la migración');
            ");

            // 4) Backfill: cada Case apunta al rol cuyo (antiguo) DemandanteId
            // coincide; si no hay match (huérfano), cae al rol sentinel de arriba.
            migrationBuilder.Sql($@"
                UPDATE c
                SET c.[DemandanteRoleId] = COALESCE(
                    (SELECT TOP 1 r.[Id] FROM [AspNetRoles] r WHERE r.[DemandanteId] = c.[DemandanteId]),
                    '{SinAsignarRoleId}')
                FROM [Case] c;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "DemandanteRoleId",
                table: "Case",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Case_DemandanteRoleId",
                table: "Case",
                column: "DemandanteRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Case_AspNetRoles_DemandanteRoleId",
                table: "Case",
                column: "DemandanteRoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 5) Ahora sí, se limpia todo lo viejo: FKs/índices/columnas hacia
            // Demandante, y la tabla misma.
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

            migrationBuilder.DropTable(
                name: "Demandante");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reconstruye la forma del esquema anterior (best-effort). No se
            // garantiza reconstruir los datos de Demandante ni el vínculo
            // Role<->Demandante: fusionar dos entidades en una no es una
            // operación reversible sin pérdida.
            migrationBuilder.DropForeignKey(
                name: "FK_Case_AspNetRoles_DemandanteRoleId",
                table: "Case");

            migrationBuilder.DropIndex(
                name: "IX_Case_DemandanteRoleId",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "DemandanteRoleId",
                table: "Case");

            migrationBuilder.DropColumn(
                name: "IsDemandante",
                table: "AspNetRoles");

            migrationBuilder.AddColumn<Guid>(
                name: "DemandanteId",
                table: "Case",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "DemandanteId",
                table: "AspNetRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Demandante",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSDATETIME()"),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Demandante", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Demandante_Name",
                table: "Demandante",
                column: "Name",
                unique: true);

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
    }
}
