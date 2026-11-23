using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Login.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemorialTemplateContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                table: "MemorialTemplate",
                type: "longblob",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedPlaceholders",
                table: "MemorialTemplate",
                type: "longtext",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            // CURRENT_TIMESTAMP en vez de un literal: DATETIME en MySQL/MariaDB
            // no admite el año 1 que EF genera por defecto para DateTime.MinValue,
            // y esta columna se agrega sobre una tabla que ya tiene filas.
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MemorialTemplate",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content",
                table: "MemorialTemplate");

            migrationBuilder.DropColumn(
                name: "DetectedPlaceholders",
                table: "MemorialTemplate");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MemorialTemplate");
        }
    }
}
