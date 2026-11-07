using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EnglishRoulette.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AdminIdentityAndCatalog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_audit",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                target_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "text", nullable: false),
                at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_audit", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "admin_roles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "admin_users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "catalog_media",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "text", nullable: false),
                sha256 = table.Column<string>(type: "text", nullable: false),
                narration_sha256 = table.Column<string>(type: "text", nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                duration_ms = table.Column<int>(type: "integer", nullable: false),
                attribution = table.Column<string>(type: "text", nullable: false),
                license_evidence = table.Column<string>(type: "text", nullable: false),
                published = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_media", x => x.id);
                table.CheckConstraint("ck_catalog_media", "size_bytes > 0 AND duration_ms > 0");
            });

        migrationBuilder.CreateTable(
            name: "catalog_stories",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_stories", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "admin_role_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_role_claims", x => x.id);
                table.ForeignKey(
                    name: "FK_admin_role_claims_admin_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "admin_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "admin_user_claims",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_user_claims", x => x.id);
                table.ForeignKey(
                    name: "FK_admin_user_claims_admin_users_user_id",
                    column: x => x.user_id,
                    principalTable: "admin_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "admin_user_logins",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "text", nullable: false),
                provider_key = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "FK_admin_user_logins_admin_users_user_id",
                    column: x => x.user_id,
                    principalTable: "admin_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "admin_user_roles",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "FK_admin_user_roles_admin_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "admin_roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_admin_user_roles_admin_users_user_id",
                    column: x => x.user_id,
                    principalTable: "admin_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "admin_user_tokens",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "FK_admin_user_tokens_admin_users_user_id",
                    column: x => x.user_id,
                    principalTable: "admin_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "catalog_versions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                story_id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                synopsis = table.Column<string>(type: "text", nullable: false),
                genre = table.Column<string>(type: "text", nullable: false),
                difficulty = table.Column<string>(type: "text", nullable: false),
                author = table.Column<string>(type: "text", nullable: false),
                license_type = table.Column<string>(type: "text", nullable: false),
                license_evidence = table.Column<string>(type: "text", nullable: false),
                attribution = table.Column<string>(type: "text", nullable: false),
                initial_scene_key = table.Column<string>(type: "text", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_versions", x => x.id);
                table.CheckConstraint("ck_catalog_revision", "revision > 0 AND number > 0");
                table.CheckConstraint("ck_catalog_status", "status IN ('Draft','Published','Archived')");
                table.ForeignKey(
                    name: "FK_catalog_versions_catalog_stories_story_id",
                    column: x => x.story_id,
                    principalTable: "catalog_stories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "catalog_scenes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                version_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "text", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                narration = table.Column<string>(type: "text", nullable: false),
                is_ending = table.Column<bool>(type: "boolean", nullable: false),
                prompt = table.Column<string>(type: "text", nullable: false),
                correct_option_key = table.Column<string>(type: "text", nullable: false),
                explanation = table.Column<string>(type: "text", nullable: false),
                audio_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_scenes", x => x.id);
                table.ForeignKey(
                    name: "FK_catalog_scenes_catalog_media_audio_id",
                    column: x => x.audio_id,
                    principalTable: "catalog_media",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_catalog_scenes_catalog_versions_version_id",
                    column: x => x.version_id,
                    principalTable: "catalog_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "catalog_options",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "text", nullable: false),
                text = table.Column<string>(type: "text", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_options", x => x.id);
                table.ForeignKey(
                    name: "FK_catalog_options_catalog_scenes_scene_id",
                    column: x => x.scene_id,
                    principalTable: "catalog_scenes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "catalog_transitions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                scene_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "text", nullable: false),
                label = table.Column<string>(type: "text", nullable: false),
                target_scene_key = table.Column<string>(type: "text", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_catalog_transitions", x => x.id);
                table.ForeignKey(
                    name: "FK_catalog_transitions_catalog_scenes_scene_id",
                    column: x => x.scene_id,
                    principalTable: "catalog_scenes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_admin_audit_at_utc",
            table: "admin_audit",
            column: "at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_admin_role_claims_role_id",
            table: "admin_role_claims",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "admin_roles",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_admin_user_claims_user_id",
            table: "admin_user_claims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_admin_user_logins_user_id",
            table: "admin_user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_admin_user_roles_role_id",
            table: "admin_user_roles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "admin_users",
            column: "normalized_email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "admin_users",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_catalog_options_scene_id_key",
            table: "catalog_options",
            columns: new[] { "scene_id", "key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_catalog_scenes_audio_id",
            table: "catalog_scenes",
            column: "audio_id");

        migrationBuilder.CreateIndex(
            name: "IX_catalog_scenes_version_id_key",
            table: "catalog_scenes",
            columns: new[] { "version_id", "key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_catalog_stories_slug",
            table: "catalog_stories",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_catalog_transitions_scene_id_key",
            table: "catalog_transitions",
            columns: new[] { "scene_id", "key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_catalog_versions_story_id",
            table: "catalog_versions",
            column: "story_id",
            unique: true,
            filter: "status = 'Published'");

        migrationBuilder.CreateIndex(
            name: "IX_catalog_versions_story_id_number",
            table: "catalog_versions",
            columns: new[] { "story_id", "number" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin_audit");

        migrationBuilder.DropTable(
            name: "admin_role_claims");

        migrationBuilder.DropTable(
            name: "admin_user_claims");

        migrationBuilder.DropTable(
            name: "admin_user_logins");

        migrationBuilder.DropTable(
            name: "admin_user_roles");

        migrationBuilder.DropTable(
            name: "admin_user_tokens");

        migrationBuilder.DropTable(
            name: "catalog_options");

        migrationBuilder.DropTable(
            name: "catalog_transitions");

        migrationBuilder.DropTable(
            name: "admin_roles");

        migrationBuilder.DropTable(
            name: "admin_users");

        migrationBuilder.DropTable(
            name: "catalog_scenes");

        migrationBuilder.DropTable(
            name: "catalog_media");

        migrationBuilder.DropTable(
            name: "catalog_versions");

        migrationBuilder.DropTable(
            name: "catalog_stories");
    }
}
