using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Multiplayer;

internal static class LobbyModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<GameGuestRow>().ToTable("game_guests").HasKey(row => row.Id);
        model.Entity<GameGuestRow>().Property(row => row.Nickname).HasMaxLength(20);
        model.Entity<GameGuestRow>().Property(row => row.Avatar).HasMaxLength(10);
        model.Entity<LobbyRow>().ToTable("multiplayer_lobbies").HasKey(row => row.Id);
        model.Entity<LobbyRow>().HasIndex(row => row.Code).IsUnique();
        model.Entity<LobbyRow>().Property(row => row.Code).HasMaxLength(8);
        model.Entity<LobbyRow>().Property(row => row.StateJson).HasColumnType("jsonb");
        model.Entity<LobbyMembershipRow>().ToTable("lobby_memberships").HasKey(row => row.PlayerId);
        model.Entity<LobbyMembershipRow>().HasOne<GameGuestRow>().WithMany().HasForeignKey(row => row.PlayerId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<LobbyMembershipRow>().HasOne<LobbyRow>().WithMany().HasForeignKey(row => row.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
