using EnglishRoulette.Domain.Multiplayer;

namespace EnglishRoulette.Application.Multiplayer;

public sealed record GuestProfile(Guid Id, string Nickname, string Avatar);
public interface ILobbyStore
{
    Task<GuestProfile?> GuestAsync(Guid id, CancellationToken token);
    Task<GuestProfile> SaveGuestAsync(Guid? id, string nickname, string avatar, CancellationToken token);
    Task<LobbyState> CreateAsync(Guid player, int capacity, CancellationToken token);
    Task<LobbyState> JoinAsync(Guid player, string code, CancellationToken token);
    Task<LobbyState?> CurrentAsync(Guid player, CancellationToken token);
    Task<LobbyState?> ReadAsync(Guid id, Guid player, CancellationToken token);
    Task<LobbyState> ChangeAsync(Guid id, Guid actor, Func<LobbyState, DateTimeOffset, LobbyState> change, CancellationToken token);
}
