using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Scoring;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Domain.Multiplayer;

public enum MultiplayerPhase { Roulette = 0, Preloading = 1, Countdown = 2, Narrating = 3, QuestionOpen = 4, QuestionReveal = 5, DecisionOpen = 6, StoryResult = 7, Cancelled = 8 }
public sealed record MultiplayerRules(int LoadingSeconds = 45, int CountdownSeconds = 3, int QuestionSeconds = 15,
    int RevealSeconds = 5, int VoteSeconds = 15, int TieMilliseconds = 300, string ScoringPolicy = "correct-arrival-v1", AssistanceRules? Hints = null);
public sealed record MultiplayerPlayer(Guid Id, string Nickname, string Avatar, int Score = 0, int CorrectAnswers = 0, bool Left = false, int StartingScore = 0);
public sealed record MultiplayerAnswer(Guid PlayerId, string OptionKey, bool IsCorrect, DateTimeOffset ReceivedAtUtc, int Points = 0, int BasePoints = 0, int HintDeduction = 0);
public sealed record MultiplayerVote(Guid PlayerId, string TransitionKey);
public sealed record MultiplayerDecision(string SceneKey, string TransitionKey, bool Tied, bool NoVotes, DateTimeOffset ResolvedAtUtc);
public sealed record MultiplayerRound(Guid InstanceId, string SceneKey, IReadOnlyList<MultiplayerAnswer> Answers, DateTimeOffset ClosedAtUtc);
public sealed record MultiplayerCommand(Guid RequestId, long ExpectedStateVersion, Guid SceneInstanceId, string Kind, string? Key = null);
public sealed record MultiplayerReceipt(Guid PlayerId, MultiplayerCommand Command);

// Private durable state: contains answer keys. Never serialize it as an HTTP/SignalR contract.
public sealed record MultiplayerMatch(
    Guid Id, StoryDefinition Story, IReadOnlyList<SceneTiming> Timings, MultiplayerRules Rules,
    IReadOnlyList<MultiplayerPlayer> Players, string SceneKey, Guid SceneInstanceId, long StateVersion,
    MultiplayerPhase Phase, DateTimeOffset PhaseStartedAtUtc, DateTimeOffset? PhaseDeadlineUtc,
    DateTimeOffset? NarrationStartedAtUtc, IReadOnlyList<Guid> AudioReady,
    IReadOnlyList<MultiplayerAnswer> Answers, IReadOnlyList<MultiplayerVote> Votes,
    IReadOnlyList<string> VisitedScenes, IReadOnlyList<MultiplayerRound> Rounds,
    IReadOnlyList<MultiplayerDecision> Decisions, IReadOnlyList<MultiplayerReceipt> Receipts, string? CancellationReason = null, DateTimeOffset? QuorumLostAtUtc = null, IReadOnlyList<HintUse>? Hints = null);

public static class MultiplayerScoring
{
    public static IReadOnlyList<MultiplayerAnswer> Award(IReadOnlyList<MultiplayerAnswer> answers, int tieMilliseconds)
    {
        var correct = answers.Where(answer => answer.IsCorrect).OrderBy(answer => answer.ReceivedAtUtc).ThenBy(answer => answer.PlayerId).ToArray();
        var points = new Dictionary<Guid, int>();
        for (var start = 0; start < correct.Length;)
        {
            var end = start + 1;
            while (end < correct.Length && correct[end].ReceivedAtUtc - correct[start].ReceivedAtUtc <= TimeSpan.FromMilliseconds(tieMilliseconds)) { end++; }
            for (var index = start; index < end; index++) { points[correct[index].PlayerId] = 200 - 20 * start; }
            start = end;
        }
        return answers.Select(answer => answer with { Points = points.GetValueOrDefault(answer.PlayerId), BasePoints = points.GetValueOrDefault(answer.PlayerId) }).ToArray();
    }
}

public static class MultiplayerGame
{
    public static MultiplayerMatch Create(StoryVersion story, IReadOnlyList<SceneTiming> timings,
        IReadOnlyList<LobbyMember> members, DateTimeOffset now, DateTimeOffset rouletteEndsAtUtc, MultiplayerRules? rules = null, IReadOnlyList<SeriesPlayer>? startingScores = null)
    {
        StoryFlowEngine.ValidateForPlay(story);
        rules ??= new();
        rules.Hints?.Validate();
        if (members.Count is < 2 or > 8 || members.Any(member => member.PlayerId == Guid.Empty) || members.Select(member => member.PlayerId).Distinct().Count() != members.Count ||
            rules.LoadingSeconds is < 1 or > 120 || rules.CountdownSeconds is < 1 or > 10 || rules.QuestionSeconds is < 1 or > 60 ||
            rules.RevealSeconds is < 1 or > 30 || rules.VoteSeconds is < 1 or > 60 || rules.TieMilliseconds != 300 ||
            rules.ScoringPolicy != "correct-arrival-v1" || timings.Count != story.Scenes.Count ||
            timings.Select(timing => timing.SceneKey).Distinct(StringComparer.Ordinal).Count() != timings.Count ||
            timings.Any(timing => timing.NarrationDuration <= TimeSpan.Zero || !story.Scenes.Any(scene => scene.Key == timing.SceneKey)))
        { throw new GameRuleException("INVALID_MATCH", "Revise participantes, tempos e conteúdo da partida."); }
        if (startingScores is not null && (startingScores.Count > 8 || startingScores.Any(player => player.Score < 0 || player.CorrectAnswers < 0) ||
            startingScores.Select(player => player.PlayerId).Distinct().Count() != startingScores.Count))
        { throw Invalid("O placar acumulado da sala é inválido."); }
        return new(Guid.NewGuid(), StoryDefinition.Capture(story), timings.ToArray(), rules,
            members.Select(member =>
            {
                var previous = startingScores?.SingleOrDefault(player => player.PlayerId == member.PlayerId);
                return new MultiplayerPlayer(member.PlayerId, member.Nickname, member.Avatar, previous?.Score ?? 0,
                    previous?.CorrectAnswers ?? 0, StartingScore: previous?.Score ?? 0);
            }).ToArray(),
            story.InitialSceneKey, Guid.NewGuid(), 1, MultiplayerPhase.Roulette, now.ToUniversalTime(), rouletteEndsAtUtc.ToUniversalTime(),
            null, [], [], [], [story.InitialSceneKey], [], [], []);
    }

    public static MultiplayerMatch Execute(MultiplayerMatch match, Guid actor, MultiplayerCommand command,
        IReadOnlyList<LobbyMember> members, DateTimeOffset now, Func<int, int> choose)
    {
        Participant(match, actor);
        if (command.RequestId == Guid.Empty) { throw Invalid("Informe a identificação do comando."); }
        var receipt = match.Receipts.FirstOrDefault(item => item.Command.RequestId == command.RequestId);
        if (receipt is not null)
        {
            if (receipt.PlayerId != actor || receipt.Command != command)
            { throw new GameRuleException("IDEMPOTENCY_CONFLICT", "Este envio já foi usado para outra ação."); }
            return Advance(match, members, now, choose);
        }
        match = Advance(match, members, now, choose);
        Participant(match, actor);
        if (command.SceneInstanceId != match.SceneInstanceId) { throw new GameRuleException("INVALID_SCENE", "A cena mudou. Atualize a partida."); }
        if (command.ExpectedStateVersion != match.StateVersion) { throw new GameRuleException("STATE_CONFLICT", "A etapa mudou. Atualize a partida."); }
        now = now.ToUniversalTime();
        switch (command.Kind)
        {
            case "audio-ready":
                Phase(match, MultiplayerPhase.Preloading);
                if (!match.AudioReady.Contains(actor)) { match = match with { AudioReady = match.AudioReady.Append(actor).ToArray() }; }
                break;
            case "answer":
                Phase(match, MultiplayerPhase.QuestionOpen);
                if (match.Answers.Any(answer => answer.PlayerId == actor)) { throw new GameRuleException("ALREADY_ANSWERED", "Sua resposta já foi registrada."); }
                var evaluation = ChallengeEvaluator.Evaluate(Scene(match).Challenge, command.Key ?? "");
                match = match with { Answers = match.Answers.Append(new(actor, command.Key!, evaluation.IsCorrect, now)).ToArray() };
                break;
            case "hint":
                if (match.Rules.Hints is null || match.Phase is not (MultiplayerPhase.Narrating or MultiplayerPhase.QuestionOpen) ||
                    match.Answers.Any(answer => answer.PlayerId == actor))
                { throw new GameRuleException("HINT_UNAVAILABLE", "Use as dicas durante a cena, antes de responder e dentro do prazo."); }
                if (Scene(match).Support?.Vocabulary.Any(hint => hint.Key == command.Key) is not true)
                { throw new GameRuleException("INVALID_HINT", "Escolha uma palavra disponível nesta cena."); }
                if (!(match.Hints ?? []).Any(hint => hint.PlayerId == actor && hint.SceneInstanceId == match.SceneInstanceId && hint.Key == command.Key))
                { match = match with { Hints = (match.Hints ?? []).Append(new(actor, match.SceneInstanceId, command.Key!, now)).ToArray() }; }
                break;
            case "vote":
                Phase(match, MultiplayerPhase.DecisionOpen);
                if (match.Votes.Any(vote => vote.PlayerId == actor)) { throw new GameRuleException("ALREADY_VOTED", "Seu voto já foi registrado."); }
                if (!Scene(match).Transitions.Any(transition => transition.Key == command.Key)) { throw new GameRuleException("INVALID_CHOICE", "Escolha um caminho desta cena."); }
                match = match with { Votes = match.Votes.Append(new(actor, command.Key!)).ToArray() };
                break;
            default: throw Invalid("Ação de partida inválida.");
        }
        match = match with { Receipts = match.Receipts.Append(new(actor, command)).ToArray() };
        return Advance(match, members, now, choose);
    }

    public static MultiplayerMatch Advance(MultiplayerMatch match, IReadOnlyList<LobbyMember> members, DateTimeOffset now, Func<int, int> choose)
    {
        if (match.Phase is MultiplayerPhase.StoryResult or MultiplayerPhase.Cancelled) { return match; }
        now = now.ToUniversalTime();
        var ids = members.Select(member => member.PlayerId).ToHashSet();
        if (match.Players.Any(player => !player.Left && !ids.Contains(player.Id)))
        { match = match with { Players = match.Players.Select(player => player with { Left = player.Left || !ids.Contains(player.Id) }).ToArray() }; }
        if (match.Players.Count(player => !player.Left) < 2)
        { return Cancel(match, now, "A partida terminou porque restaram menos de dois participantes após saída ou prazo de reconexão."); }
        var connected = members.Where(member => match.Players.Any(player => player.Id == member.PlayerId && !player.Left) && now - member.LastSeenUtc < LobbyRoom.ConnectedWindow).Select(member => member.PlayerId).ToArray();
        if (connected.Length < 2 && match.QuorumLostAtUtc is null) { match = match with { QuorumLostAtUtc = now }; }
        else if (connected.Length >= 2 && match.QuorumLostAtUtc is not null)
        {
            // A participant returning within the grace period gets a fresh preparation window.
            // Already-open narration/question deadlines remain unchanged.
            if (match.Phase is MultiplayerPhase.Preloading or MultiplayerPhase.Countdown)
            { match = ChangePhase(match, MultiplayerPhase.Preloading, now, match.Rules.LoadingSeconds); }
            match = match with { QuorumLostAtUtc = null };
        }
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var expired = match.PhaseDeadlineUtc is { } deadline && now >= deadline;
            switch (match.Phase)
            {
                case MultiplayerPhase.Roulette when expired:
                    match = ChangePhase(match, MultiplayerPhase.Preloading, match.PhaseDeadlineUtc!.Value, match.Rules.LoadingSeconds);
                    continue;
                case MultiplayerPhase.Preloading:
                    if (connected.Length >= 2 && connected.All(match.AudioReady.Contains))
                    { return ChangePhase(match, MultiplayerPhase.Countdown, now, match.Rules.CountdownSeconds); }
                    if (expired && connected.Length >= 2)
                    { return Cancel(match, now, "O áudio não ficou pronto para todos no prazo. Confiram a conexão e criem outra partida."); }
                    return match;
                case MultiplayerPhase.Countdown when expired:
                    if (connected.Length < 2 || connected.Any(player => !match.AudioReady.Contains(player)))
                    { return ChangePhase(match, MultiplayerPhase.Preloading, now, match.Rules.LoadingSeconds); }
                    var start = match.PhaseDeadlineUtc!.Value;
                    match = ChangePhase(match, MultiplayerPhase.Narrating, start, NarrationSeconds(match)) with { NarrationStartedAtUtc = start };
                    continue;
                case MultiplayerPhase.Narrating when expired:
                    match = ChangePhase(match, MultiplayerPhase.QuestionOpen, match.PhaseDeadlineUtc!.Value, match.Rules.QuestionSeconds);
                    continue;
                case MultiplayerPhase.QuestionOpen:
                    if (expired || match.Players.Where(player => !player.Left).All(player => match.Answers.Any(answer => answer.PlayerId == player.Id)))
                    {
                        match = CloseQuestion(match, expired ? match.PhaseDeadlineUtc!.Value : now);
                        continue;
                    }
                    return match;
                case MultiplayerPhase.QuestionReveal when expired:
                    if (connected.Length < 2) { return match; }
                    var scene = Scene(match);
                    if (scene.IsEnding) { return ChangePhase(match, MultiplayerPhase.StoryResult, now, null); }
                    if (scene.Transitions.Count > 1)
                    { return ChangePhase(match, MultiplayerPhase.DecisionOpen, now, match.Rules.VoteSeconds); }
                    return Move(match, null, now);
                case MultiplayerPhase.DecisionOpen:
                    if (expired || match.Players.Where(player => !player.Left).All(player => match.Votes.Any(vote => vote.PlayerId == player.Id)))
                    {
                        if (connected.Length < 2) { return match; }
                        return Move(match, VoteResult(match, choose), now);
                    }
                    return match;
                default: return match;
            }
        }
        throw Invalid("A sequência de fases da partida é inválida.");
    }

    public static StoryScene Scene(MultiplayerMatch match) => match.Story.Restore().Scenes.Single(scene => scene.Key == match.SceneKey);

    private static double NarrationSeconds(MultiplayerMatch match) => match.Timings.Single(timing => timing.SceneKey == match.SceneKey).NarrationDuration.TotalSeconds;

    private static MultiplayerMatch CloseQuestion(MultiplayerMatch match, DateTimeOffset now)
    {
        // Rank correct arrivals first; assistance never changes another player's rank or points.
        var answers = MultiplayerScoring.Award(match.Answers, match.Rules.TieMilliseconds).Select(answer =>
        {
            var count = (match.Hints ?? []).Count(hint => hint.PlayerId == answer.PlayerId && hint.SceneInstanceId == match.SceneInstanceId);
            var deduction = match.Rules.Hints?.Discount(answer.BasePoints, count) ?? 0;
            return answer with { HintDeduction = deduction, Points = answer.BasePoints - deduction };
        }).ToArray();
        return ChangePhase(match, MultiplayerPhase.QuestionReveal, now, match.Rules.RevealSeconds) with
        {
            Answers = answers,
            Players = match.Players.Select(player =>
            {
                var answer = answers.SingleOrDefault(item => item.PlayerId == player.Id);
                return player with { Score = player.Score + (answer?.Points ?? 0), CorrectAnswers = player.CorrectAnswers + (answer?.IsCorrect == true ? 1 : 0) };
            }).ToArray(),
            Rounds = match.Rounds.Append(new(match.SceneInstanceId, match.SceneKey, answers, now)).ToArray()
        };
    }

    private static string VoteResult(MultiplayerMatch match, Func<int, int> choose)
    {
        var counts = Scene(match).Transitions.Select(transition => new
        {
            transition.Key,
            Count = match.Votes.Count(vote => vote.TransitionKey == transition.Key && match.Players.Any(player => player.Id == vote.PlayerId && !player.Left))
        }).ToArray();
        var candidates = counts.Where(item => item.Count == counts.Max(value => value.Count)).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        return candidates[candidates.Length == 1 ? 0 : choose(candidates.Length)].Key;
    }

    private static MultiplayerMatch Move(MultiplayerMatch match, string? key, DateTimeOffset now)
    {
        var current = Scene(match);
        var next = StoryFlowEngine.GetNextScene(match.Story.Restore(), match.SceneKey, key) ?? throw Invalid("A história terminou sem um desfecho válido.");
        var decisions = match.Decisions;
        if (key is not null)
        {
            var totals = current.Transitions.Select(transition => match.Votes.Count(vote => vote.TransitionKey == transition.Key && match.Players.Any(player => player.Id == vote.PlayerId && !player.Left))).ToArray();
            decisions = decisions.Append(new(match.SceneKey, key, totals.Count(count => count == totals.Max()) > 1, totals.Max() == 0, now)).ToArray();
        }
        return ChangePhase(match, MultiplayerPhase.Preloading, now, match.Rules.LoadingSeconds) with
        {
            SceneKey = next.Key,
            SceneInstanceId = Guid.NewGuid(),
            NarrationStartedAtUtc = null,
            AudioReady = [],
            Answers = [],
            Votes = [],
            VisitedScenes = match.VisitedScenes.Append(next.Key).ToArray(),
            Decisions = decisions
        };
    }

    private static MultiplayerMatch ChangePhase(MultiplayerMatch match, MultiplayerPhase phase, DateTimeOffset now, double? duration) => match with
    { Phase = phase, PhaseStartedAtUtc = now, PhaseDeadlineUtc = duration is { } seconds ? now.AddSeconds(seconds) : null, StateVersion = match.StateVersion + 1 };
    private static MultiplayerMatch Cancel(MultiplayerMatch match, DateTimeOffset now, string reason) =>
        ChangePhase(match, MultiplayerPhase.Cancelled, now, null) with { CancellationReason = reason };
    private static void Participant(MultiplayerMatch match, Guid actor)
    {
        if (!match.Players.Any(player => player.Id == actor && !player.Left))
        { throw new GameRuleException("NOT_PARTICIPANT", "Você não participa desta partida."); }
    }
    private static void Phase(MultiplayerMatch match, MultiplayerPhase expected)
    { if (match.Phase != expected) { throw new GameRuleException("INVALID_GAME_PHASE", "Esta ação não está disponível na etapa atual."); } }
    private static GameRuleException Invalid(string message) => new("INVALID_MATCH", message);
}
