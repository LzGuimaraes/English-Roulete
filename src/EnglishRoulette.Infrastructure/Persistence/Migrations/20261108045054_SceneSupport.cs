using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishRoulette.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class SceneSupport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "support_json",
            table: "catalog_scenes",
            type: "jsonb",
            nullable: false,
            defaultValue: "{}");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "support_json",
            table: "catalog_scenes");
    }
}
