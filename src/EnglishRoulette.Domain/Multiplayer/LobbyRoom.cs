using System.Text;

namespace EnglishRoulette.Domain.Multiplayer;

public sealed record LobbyMember(Guid PlayerId, string Nickname, string Avatar, bool Ready,
    DateTimeOffset JoinedAtUtc, DateTimeOffset LastSeenUtc);
public sealed record LobbyState(Guid Id, string Code, int Capacity, Guid HostId, long StateVersion,
    Guid? StoryVersionId, IReadOnlyList<LobbyMember> Members, DateTimeOffset LastActivityUtc, string Status = "Lobby");

public static class LobbyRoom
{
    public static readonly TimeSpan ConnectedWindow = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ReconnectGrace = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan InactivityLimit = TimeSpan.FromMinutes(30);
    public static IReadOnlyList<string> Avatars { get; } = Array.AsReadOnly(new[] { "🦊", "🐼", "🐙", "🦄", "🤖", "🐸", "🐯", "🐨" });
    public static string NormalizeName(string nickname)
    {
        var value = nickname.Trim().Normalize(NormalizationForm.FormKC);
        if (value.Length is < 3 or > 20 || value.Any(char.IsControl))
        { throw new GameRuleException("INVALID_PROFILE", "Use um apelido de 3 a 20 caracteres, sem caracteres de controle."); }
        return value;
    }
    public static void ValidateAvatar(string avatar)
    { if (!Avatars.Contains(avatar, StringComparer.Ordinal)) { throw new GameRuleException("INVALID_PROFILE", "Escolha um avatar do catálogo."); } }
    public static LobbyState Create(Guid id, string code, int capacity, LobbyMember host, DateTimeOffset now)
    {
        if (capacity is < 2 or > 8) { throw new GameRuleException("INVALID_CAPACITY", "Escolha de 2 a 8 vagas."); }
        return new(id, code, capacity, host.PlayerId, 1, null, Array.AsReadOnly(new[] { host }), now);
    }
    public static LobbyState Join(LobbyState state, LobbyMember member, DateTimeOffset now)
    {
        Active(state);
        if (state.Members.Any(item => item.PlayerId == member.PlayerId)) { return Touch(state, member.PlayerId, now); }
        if (state.Members.Count >= state.Capacity) { throw new GameRuleException("ROOM_FULL", "A sala está lotada."); }
        if (state.Members.Any(item => string.Equals(item.Nickname, member.Nickname, StringComparison.OrdinalIgnoreCase)))
        { throw new GameRuleException("NAME_TAKEN", "Esse apelido já está em uso nesta sala."); }
        return state with { Members = Array.AsReadOnly(state.Members.Append(member).ToArray()), StateVersion = state.StateVersion + 1, LastActivityUtc = now };
    }
    public static LobbyState Touch(LobbyState state, Guid player, DateTimeOffset now) => state with
    { Members = Array.AsReadOnly(state.Members.Select(member => member.PlayerId == player ? member with { LastSeenUtc = now } : member).ToArray()), LastActivityUtc = now };
    public static LobbyState SetReady(LobbyState state, Guid player, long expectedVersion, bool ready, DateTimeOffset now)
    {
        Authorize(state, player); Version(state, expectedVersion);
        if (state.Members.Single(member => member.PlayerId == player).Ready == ready) { return Touch(state, player, now); }
        return Touch(state with { Members = Array.AsReadOnly(state.Members.Select(member => member.PlayerId == player ? member with { Ready = ready } : member).ToArray()), StateVersion = state.StateVersion + 1 }, player, now);
    }
    public static LobbyState ChooseStory(LobbyState state, Guid actor, long version, Guid storyVersionId, DateTimeOffset now)
    {
        Authorize(state, actor); Version(state, version);
        if (state.HostId != actor) { throw new GameRuleException("ROOM_FORBIDDEN", "Só o anfitrião escolhe a história."); }
        if (state.StoryVersionId == storyVersionId) { return Touch(state, actor, now); }
        return Touch(state with
        {
            StoryVersionId = storyVersionId,
            StateVersion = state.StateVersion + 1,
            Members = Array.AsReadOnly(state.Members.Select(member => member with { Ready = false }).ToArray())
        }, actor, now);
    }
    public static LobbyState Leave(LobbyState state, Guid player, DateTimeOffset now)
    {
        Authorize(state, player);
        return Without(state, state.Members.Where(member => member.PlayerId != player).ToArray(), now);
    }
    public static LobbyState Expire(LobbyState state, DateTimeOffset now)
    {
        if (state.Status != "Lobby") { return state; }
        if (now - state.LastActivityUtc >= InactivityLimit)
        { return state with { Status = "Expired", Members = Array.Empty<LobbyMember>(), StateVersion = state.StateVersion + 1 }; }
        var members = state.Members.Where(member => now - member.LastSeenUtc < ReconnectGrace).ToArray();
        return members.Length == state.Members.Count ? state : Without(state, members, now);
    }
    private static LobbyState Without(LobbyState state, LobbyMember[] members, DateTimeOffset now) => state with
    {
        Members = Array.AsReadOnly(members),
        StateVersion = state.StateVersion + 1,
        LastActivityUtc = now,
        HostId = members.Any(member => member.PlayerId == state.HostId) ? state.HostId : members.OrderBy(member => now - member.LastSeenUtc >= ConnectedWindow).ThenBy(member => member.JoinedAtUtc).ThenBy(member => member.PlayerId).FirstOrDefault()?.PlayerId ?? Guid.Empty,
        Status = members.Length == 0 ? "Closed" : state.Status
    };
    public static void Authorize(LobbyState state, Guid player)
    {
        Active(state);
        if (!state.Members.Any(member => member.PlayerId == player))
        { throw new GameRuleException("ROOM_FORBIDDEN", "Você não participa desta sala."); }
    }
    private static void Active(LobbyState state)
    { if (state.Status != "Lobby") { throw new GameRuleException("ROOM_EXPIRED", "Esta sala foi encerrada ou expirou."); } }
    private static void Version(LobbyState state, long version)
    { if (state.StateVersion != version) { throw new GameRuleException("STATE_CONFLICT", "A sala mudou. Atualize e tente novamente."); } }
}
