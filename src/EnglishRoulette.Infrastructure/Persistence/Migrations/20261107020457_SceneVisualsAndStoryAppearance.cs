using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishRoulette.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SceneVisualsAndStoryAppearance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "appearance_json",
            table: "catalog_versions",
            type: "text",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<Guid>(
            name: "visual_id",
            table: "catalog_scenes",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "catalog_visuals",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "text", nullable: false),
                content_type = table.Column<string>(type: "text", nullable: false),
                sha256 = table.Column<string>(type: "text", nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                width = table.Column<int>(type: "integer", nullable: false),
                height = table.Column<int>(type: "integer", nullable: false),
                animated = table.Column<bool>(type: "boolean", nullable: false),
                alt_text = table.Column<string>(type: "text", nullable: false),
                attribution = table.Column<string>(type: "text", nullable: false),
                license_evidence = table.Column<string>(type: "text", nullable: false),
                published = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_visuals", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_catalog_scenes_visual_id",
            table: "catalog_scenes",
            column: "visual_id");

        migrationBuilder.AddForeignKey(
            name: "FK_catalog_scenes_catalog_visuals_visual_id",
            table: "catalog_scenes",
            column: "visual_id",
            principalTable: "catalog_visuals",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_catalog_scenes_catalog_visuals_visual_id",
            table: "catalog_scenes");

        migrationBuilder.DropTable(
            name: "catalog_visuals");

        migrationBuilder.DropIndex(
            name: "IX_catalog_scenes_visual_id",
            table: "catalog_scenes");

        migrationBuilder.DropColumn(
            name: "appearance_json",
            table: "catalog_versions");

        migrationBuilder.DropColumn(
            name: "visual_id",
            table: "catalog_scenes");
    }
}
