using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishRoulette.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSoloCheckpoints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "solo_session_checkpoints",
            columns: table => new
            {
                credential_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                checkpoint_json = table.Column<string>(type: "jsonb", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_solo_session_checkpoints", x => x.credential_hash);
                table.CheckConstraint("ck_solo_credential_hash", "credential_hash ~ '^[A-F0-9]{64}$'");
                table.CheckConstraint("ck_solo_revision", "revision >= 1");
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "solo_session_checkpoints");
    }
}
