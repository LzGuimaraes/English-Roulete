using System.Security.Cryptography;
using System.Text.Json;
using EnglishRoulette.Application.Multiplayer;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Multiplayer;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Multiplayer;

public sealed class GameGuestRow
{
    public Guid Id { get; set; }
    public string Nickname { get; set; } = "";
    public string Avatar { get; set; } = "";
}
public sealed class LobbyRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string StateJson { get; set; } = "";
}
public sealed class LobbyMembershipRow
{
    public Guid PlayerId { get; set; }
    public Guid RoomId { get; set; }
}

public sealed class LobbyStore(AdventureDbContext db, TimeProvider clock) : ILobbyStore
{
    public async Task<GuestProfile?> GuestAsync(Guid id, CancellationToken token) =>
        await db.GameGuests.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, token) is { } guest
            ? new(guest.Id, guest.Nickname, guest.Avatar) : null;
    public async Task<GuestProfile> SaveGuestAsync(Guid? id, string nickname, string avatar, CancellationToken token)
    {
        nickname = LobbyRoom.NormalizeName(nickname); LobbyRoom.ValidateAvatar(avatar);
        var row = id is { } value ? await db.GameGuests.SingleOrDefaultAsync(row => row.Id == value, token) : null;
        if (row is null) { row = new() { Id = Guid.NewGuid() }; db.GameGuests.Add(row); }
        row.Nickname = nickname; row.Avatar = avatar; await db.SaveChangesAsync(token);
        return new(row.Id, row.Nickname, row.Avatar);
    }
    public async Task<LobbyState> CreateAsync(Guid player, int capacity, CancellationToken token, string language = "en")
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await LockPlayer(player, token); await EnsureAvailable(player, token);
        var guest = await GuestAsync(player, token) ?? throw Missing();
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = string.Concat(RandomNumberGenerator.GetBytes(8).Select(value => alphabet[value % alphabet.Length]));
        var now = clock.GetUtcNow();
        var state = LobbyRoom.Create(Guid.NewGuid(), code, capacity, new(player, guest.Nickname, guest.Avatar, false, now, now), now, language);
        var row = new LobbyRow { Id = state.Id, Code = state.Code, StateJson = JsonSerializer.Serialize(state) };
        db.Lobbies.Add(row); db.LobbyMemberships.Add(new() { PlayerId = player, RoomId = row.Id });
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return state;
    }
    public async Task<LobbyState> JoinAsync(Guid player, string code, CancellationToken token)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length != 8 || code.Any(character => !char.IsAsciiLetterOrDigit(character))) { throw Missing(); }
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await LockPlayer(player, token);
        var id = await db.Lobbies.AsNoTracking().Where(row => row.Code == code).Select(row => (Guid?)row.Id).SingleOrDefaultAsync(token) ?? throw Missing();
        await LockRoom(id, token);
        var row = await Load(id, token) ?? throw Missing();
        var state = LobbyRoom.Expire(Restore(row), clock.GetUtcNow());
        if (!state.Members.Any(member => member.PlayerId == player)) { await EnsureAvailable(player, token); }
        var guest = await GuestAsync(player, token) ?? throw Missing();
        var now = clock.GetUtcNow();
        state = LobbyRoom.Join(state, new(player, guest.Nickname, guest.Avatar, false, now, now), now);
        state = Tick(state, clock.GetUtcNow());
        await Save(row, state, token); await transaction.CommitAsync(token); return state;
    }
    public async Task<LobbyState?> CurrentAsync(Guid player, CancellationToken token)
    {
        var id = await db.LobbyMemberships.AsNoTracking().Where(row => row.PlayerId == player).Select(row => (Guid?)row.RoomId).SingleOrDefaultAsync(token);
        return id is { } value ? await ReadAsync(value, player, token) : null;
    }
    public async Task<LobbyState?> ReadAsync(Guid id, Guid player, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await LockRoom(id, token);
        var row = await Load(id, token);
        if (row is null) { return null; }
        var state = LobbyRoom.Expire(Restore(row), clock.GetUtcNow());
        var belongs = state.Members.Any(member => member.PlayerId == player);
        if (belongs) { state = LobbyRoom.Touch(state, player, clock.GetUtcNow()); }
        state = Tick(state, clock.GetUtcNow());
        await Save(row, state, token); await transaction.CommitAsync(token);
        return belongs ? state : null;
    }
    public async Task<LobbyState> ChangeAsync(Guid id, Guid actor, Func<LobbyState, DateTimeOffset, LobbyState> change, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await LockRoom(id, token);
        var row = await Load(id, token) ?? throw Missing();
        var state = LobbyRoom.Expire(Restore(row), clock.GetUtcNow());
        LobbyRoom.Authorize(state, actor);
        state = change(Tick(LobbyRoom.Touch(state, actor, clock.GetUtcNow()), clock.GetUtcNow()), clock.GetUtcNow());
        state = Tick(state, clock.GetUtcNow());
        await Save(row, state, token); await transaction.CommitAsync(token); return state;
    }
    private async Task EnsureAvailable(Guid player, CancellationToken token)
    {
        if (await db.LobbyMemberships.AsNoTracking().AnyAsync(row => row.PlayerId == player, token))
        { throw new GameRuleException("ALREADY_IN_ROOM", "Saia da sala atual antes de entrar em outra."); }
    }
    public async Task<IReadOnlyList<LobbyState>> TickMatchesAsync(CancellationToken token)
    {
        var ids = await db.Lobbies.AsNoTracking().Where(row => EF.Functions.JsonExists(row.StateJson, "Match") &&
            !EF.Functions.JsonContains(row.StateJson, "{\"Match\":null}") &&
            !EF.Functions.JsonContains(row.StateJson, "{\"Match\":{\"Phase\":7}}") &&
            !EF.Functions.JsonContains(row.StateJson, "{\"Match\":{\"Phase\":8}}"))
            .Select(row => row.Id).ToArrayAsync(token);
        var changed = new List<LobbyState>();
        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await LockRoom(id, token);
            var row = await Load(id, token);
            if (row is null) { continue; }
            var previous = Restore(row);
            var state = Tick(LobbyRoom.Expire(previous, clock.GetUtcNow()), clock.GetUtcNow());
            if (state != previous) { await Save(row, state, token); changed.Add(state); }
            await transaction.CommitAsync(token);
            db.Entry(row).State = EntityState.Detached;
        }
        return changed;
    }
    private static LobbyState Tick(LobbyState state, DateTimeOffset now)
    {
        if (state.Match is not { } match) { return state; }
        var next = MultiplayerGame.Advance(match, state.Members, now, RandomNumberGenerator.GetInt32);
        return ReferenceEquals(match, next) ? state : state with { Match = next, StateVersion = state.StateVersion + 1 };
    }
    private async Task<LobbyRow?> Load(Guid id, CancellationToken token)
    {
        var tracked = db.Lobbies.Local.SingleOrDefault(row => row.Id == id);
        if (tracked is not null) { await db.Entry(tracked).ReloadAsync(token); return tracked; }
        return await db.Lobbies.SingleOrDefaultAsync(row => row.Id == id, token);
    }
    private async Task Save(LobbyRow row, LobbyState state, CancellationToken token)
    {
        var previousIds = Restore(row).Members.Select(member => member.PlayerId).ToHashSet();
        row.StateJson = JsonSerializer.Serialize(state);
        if (!previousIds.SetEquals(state.Members.Select(member => member.PlayerId)))
        {
            await db.LobbyMemberships.Where(member => member.RoomId == row.Id).ExecuteDeleteAsync(token);
            db.LobbyMemberships.AddRange(state.Members.Select(member => new LobbyMembershipRow { PlayerId = member.PlayerId, RoomId = row.Id }));
        }
        await db.SaveChangesAsync(token);
        foreach (var entry in db.ChangeTracker.Entries<LobbyMembershipRow>().ToArray()) { entry.State = EntityState.Detached; }
    }
    private Task<int> LockRoom(Guid id, CancellationToken token) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM multiplayer_lobbies WHERE id = {id} FOR UPDATE", token);
    private Task<int> LockPlayer(Guid id, CancellationToken token) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", token);
    private static LobbyState Restore(LobbyRow row) => JsonSerializer.Deserialize<LobbyState>(row.StateJson) ?? throw new InvalidDataException("Invalid lobby checkpoint.");
    private static GameRuleException Missing() => new("NOT_FOUND", "Sala ou jogador não encontrado.");
}
