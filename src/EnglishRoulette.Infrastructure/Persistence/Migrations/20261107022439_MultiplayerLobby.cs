using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnglishRoulette.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class MultiplayerLobby : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "game_guests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                nickname = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                avatar = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_game_guests", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "multiplayer_lobbies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                state_json = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_multiplayer_lobbies", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "lobby_memberships",
            columns: table => new
            {
                player_id = table.Column<Guid>(type: "uuid", nullable: false),
                room_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lobby_memberships", x => x.player_id);
                table.ForeignKey(
                    name: "FK_lobby_memberships_game_guests_player_id",
                    column: x => x.player_id,
                    principalTable: "game_guests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_lobby_memberships_multiplayer_lobbies_room_id",
                    column: x => x.room_id,
                    principalTable: "multiplayer_lobbies",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_lobby_memberships_room_id",
            table: "lobby_memberships",
            column: "room_id");

        migrationBuilder.CreateIndex(
            name: "IX_multiplayer_lobbies_code",
            table: "multiplayer_lobbies",
            column: "code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "lobby_memberships");

        migrationBuilder.DropTable(
            name: "game_guests");

        migrationBuilder.DropTable(
            name: "multiplayer_lobbies");
    }
}
