using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishRoulette.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class StoryContentLanguage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "language",
            table: "catalog_versions",
            type: "character varying(10)",
            maxLength: 10,
            nullable: false,
            defaultValue: "en");

        migrationBuilder.AddCheckConstraint(
            name: "ck_catalog_language",
            table: "catalog_versions",
            sql: "language IN ('en','pt-BR')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_catalog_language",
            table: "catalog_versions");

        migrationBuilder.DropColumn(
            name: "language",
            table: "catalog_versions");
    }
}
