using System.Text;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Domain.Multiplayer;

public sealed record LobbyMember(Guid PlayerId, string Nickname, string Avatar, bool Ready,
    DateTimeOffset JoinedAtUtc, DateTimeOffset LastSeenUtc);
public sealed record SeriesPlayer(Guid PlayerId, int Score, int CorrectAnswers);
public sealed record LobbyChatMessage(Guid Id, Guid RequestId, Guid PlayerId, string Nickname, string Avatar,
    string Text, DateTimeOffset SentAtUtc);
public sealed record LobbyState(Guid Id, string Code, int Capacity, Guid HostId, long StateVersion,
    Guid? StoryVersionId, IReadOnlyList<LobbyMember> Members, DateTimeOffset LastActivityUtc, string Status = "Lobby",
    DateTimeOffset? RouletteStartedAtUtc = null, MultiplayerMatch? Match = null, IReadOnlyList<SeriesPlayer>? SeriesScores = null,
    Guid? PreparedFromMatchId = null, int CompletedGames = 0, string Language = StoryLanguage.English,
    int QuestionSeconds = QuestionTiming.MultiplayerDefaultSeconds, IReadOnlyList<LobbyChatMessage>? ChatMessages = null,
    string? Difficulty = null);

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
    public static LobbyState Create(Guid id, string code, int capacity, LobbyMember host, DateTimeOffset now, string language = StoryLanguage.English, string? difficulty = null)
    {
        if (capacity is < 2 or > 8) { throw new GameRuleException("INVALID_CAPACITY", "Escolha de 2 a 8 vagas."); }
        return new(id, code, capacity, host.PlayerId, 1, null, Array.AsReadOnly(new[] { host }), now,
            Language: StoryLanguage.Require(language), Difficulty: StoryDifficulty.Require(difficulty));
    }
    public static LobbyState Join(LobbyState state, LobbyMember member, DateTimeOffset now)
    {
        Active(state);
        if (state.Members.Any(item => item.PlayerId == member.PlayerId)) { return Touch(state, member.PlayerId, now); }
        if (state.Match is not null) { throw new GameRuleException("ROOM_IN_PROGRESS", "Esta partida já começou. Aguarde outra sala."); }
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
        if (state.Match is not null) { throw new GameRuleException("ROOM_IN_PROGRESS", "A prontidão do lobby não muda durante a partida."); }
        if (state.Members.Single(member => member.PlayerId == player).Ready == ready) { return Touch(state, player, now); }
        return Touch(state with { Members = Array.AsReadOnly(state.Members.Select(member => member.PlayerId == player ? member with { Ready = ready } : member).ToArray()), StateVersion = state.StateVersion + 1 }, player, now);
    }
    public static LobbyState SetQuestionSeconds(LobbyState state, Guid player, long expectedVersion, int seconds, DateTimeOffset now)
        => SetSettings(state, player, expectedVersion, seconds, state.Capacity, state.Language, now, state.Difficulty);

    public static LobbyState SetSettings(LobbyState state, Guid player, long expectedVersion, int seconds, int capacity, string language, DateTimeOffset now, string? difficulty = null)
    {
        Authorize(state, player); Version(state, expectedVersion);
        if (state.HostId != player) { throw new GameRuleException("ROOM_FORBIDDEN", "Só o anfitrião configura a sala."); }
        if (state.Match is not null || state.StoryVersionId is not null) { throw new GameRuleException("ROOM_IN_PROGRESS", "As configurações ficam bloqueadas após o sorteio."); }
        QuestionTiming.FromSeconds(seconds);
        if (capacity is < 2 or > 8 || capacity < state.Members.Count)
        { throw new GameRuleException("INVALID_CAPACITY", "As vagas devem ser de 2 a 8 e acomodar todos os participantes atuais."); }
        language = StoryLanguage.Require(language);
        difficulty = StoryDifficulty.Require(difficulty);
        if (state.QuestionSeconds == seconds && state.Capacity == capacity && state.Language == language && state.Difficulty == difficulty) { return Touch(state, player, now); }
        return Touch(state with
        {
            QuestionSeconds = seconds,
            Capacity = capacity,
            Language = language,
            Difficulty = difficulty,
            StateVersion = state.StateVersion + 1
        }, player, now);
    }
    public static LobbyState AddChatMessage(LobbyState state, Guid player, Guid requestId, string message, DateTimeOffset now)
    {
        Authorize(state, player);
        if (requestId == Guid.Empty) { throw new GameRuleException("INVALID_CHAT", "Identifique o envio do chat."); }
        var text = message.Trim();
        var existing = (state.ChatMessages ?? []).SingleOrDefault(item => item.RequestId == requestId);
        if (existing is not null)
        {
            if (existing.PlayerId != player || !string.Equals(existing.Text, text, StringComparison.Ordinal))
            { throw new GameRuleException("IDEMPOTENCY_CONFLICT", "Este envio de chat já foi usado para outra mensagem."); }
            return Touch(state, player, now);
        }
        if (text.Length is < 1 or > 300 || text.Any(char.IsControl))
        { throw new GameRuleException("INVALID_CHAT", "A mensagem deve ter entre 1 e 300 caracteres."); }
        var member = state.Members.Single(item => item.PlayerId == player);
        var messages = (state.ChatMessages ?? []).Append(new LobbyChatMessage(Guid.NewGuid(), requestId, player,
            member.Nickname, member.Avatar, text, now.ToUniversalTime())).TakeLast(100).ToArray();
        return Touch(state with { ChatMessages = Array.AsReadOnly(messages), StateVersion = state.StateVersion + 1 }, player, now);
    }
    public static LobbyState Spin(LobbyState state, Guid actor, long version, Guid storyVersionId, DateTimeOffset now)
    {
        Authorize(state, actor); Version(state, version);
        if (state.HostId != actor) { throw new GameRuleException("ROOM_FORBIDDEN", "Só o anfitrião inicia a roleta."); }
        if (state.RouletteStartedAtUtc is not null)
        { throw new GameRuleException("ROULETTE_ALREADY_DRAWN", "A história desta sala já foi sorteada."); }
        if (state.Members.Count < 2 || state.Members.Any(member => !member.Ready || now - member.LastSeenUtc >= ConnectedWindow))
        { throw new GameRuleException("ROOM_NOT_READY", "A roleta precisa de pelo menos dois jogadores conectados e todos prontos."); }
        return Touch(state with
        {
            StoryVersionId = storyVersionId,
            RouletteStartedAtUtc = now,
            StateVersion = state.StateVersion + 1,
        }, actor, now);
    }
    public static LobbyState PrepareNext(LobbyState state, Guid actor, long version, Guid completedMatchId, DateTimeOffset now)
    {
        Authorize(state, actor);
        if (state.HostId != actor) { throw new GameRuleException("ROOM_FORBIDDEN", "Só o anfitrião prepara a próxima partida."); }
        // A lost response may be retried in the prepared lobby. Never reset a newer match.
        if (state.Match is null && state.PreparedFromMatchId == completedMatchId) { return Touch(state, actor, now); }
        Version(state, version);
        if (state.Match is not { Phase: MultiplayerPhase.StoryResult } match || match.Id != completedMatchId)
        { throw new GameRuleException("INVALID_GAME_PHASE", "A próxima partida pode ser preparada após o resultado final desta história."); }
        var totals = state.Members.Select(member =>
        {
            var player = match.Players.SingleOrDefault(player => player.Id == member.PlayerId);
            return new SeriesPlayer(member.PlayerId, player?.Score ?? 0, player?.CorrectAnswers ?? 0);
        }).ToArray();
        return Touch(state with
        {
            Match = null,
            StoryVersionId = null,
            RouletteStartedAtUtc = null,
            SeriesScores = Array.AsReadOnly(totals),
            PreparedFromMatchId = completedMatchId,
            CompletedGames = state.CompletedGames + 1,
            Members = Array.AsReadOnly(state.Members.Select(member => member with { Ready = false }).ToArray()),
            StateVersion = state.StateVersion + 1
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
