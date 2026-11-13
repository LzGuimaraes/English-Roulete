using EnglishRoulette.Api.Admin;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Multiplayer;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Multiplayer;
using EnglishRoulette.Domain.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace EnglishRoulette.Api.Multiplayer;

// Public projection only. The persisted match includes answer keys and stays on the server.
internal sealed class RoomGames(IAdminCatalog catalog, ILobbyStore lobbies, TimeProvider clock, IConfiguration configuration)
{
    internal static MultiplayerMatch Start(PublishedAdventure content, LobbyState state, DateTimeOffset now, EnglishRoulette.Domain.Scoring.AssistanceRules? hints = null) =>
        MultiplayerGame.Create(content.Story, content.Story.Scenes.Select(scene => new SceneTiming(scene.Key,
            TimeSpan.FromMilliseconds(content.Audio[scene.Key].DurationMs + 2000), TimeSpan.FromSeconds(state.QuestionSeconds))).ToArray(),
            state.Members, now, (state.RouletteStartedAtUtc ?? now).AddSeconds(5), new(QuestionSeconds: state.QuestionSeconds, Hints: hints), state.SeriesScores);

    public async Task<RoomView> ViewAsync(LobbyState state, Guid actor, CancellationToken token)
    {
        PublishedAdventure? content = null;
        if (state.StoryVersionId is { } version)
        {
            content = await catalog.LoadVersionAsync(version, token)
                ?? throw new GameRuleException("INVALID_CONTENT", "O conteúdo desta partida está indisponível. Saia para criar outra sala.");
            // Upgrade rooms drawn before real gameplay was deployed, under the same row lock.
            if (state.Match is null)
            {
                state = await lobbies.ChangeAsync(state.Id, actor, (current, now) => current.Match is not null ? current :
                    current with { Match = Start(content, current, now, GameAssistance.Rules(configuration).Hints), StateVersion = current.StateVersion + 1 }, token);
            }
        }
        var nowUtc = clock.GetUtcNow();
        return new(state.Id, state.Code, state.Capacity, state.HostId, actor, state.StateVersion, state.StoryVersionId,
            state.Members.Select(member => new RoomMemberView(member.PlayerId, member.Nickname, member.Avatar, member.Ready,
                nowUtc - member.LastSeenUtc < LobbyRoom.ConnectedWindow,
                state.Match?.Players.SingleOrDefault(player => player.Id == member.PlayerId)?.Score ??
                    state.SeriesScores?.SingleOrDefault(player => player.PlayerId == member.PlayerId)?.Score ?? 0)).ToArray(), nowUtc, state.RouletteStartedAtUtc,
            state.Match is { } match && content is not null ? Map(match, content, state, actor, nowUtc) : null, state.CompletedGames + 1, state.Language,
            state.QuestionSeconds, (state.ChatMessages ?? []).Select(message => new RoomChatMessageView(message.Id, message.PlayerId,
                message.Nickname, message.Avatar, message.Text, message.SentAtUtc)).ToArray(), state.Difficulty);
    }
    private static MultiplayerMatchView Map(MultiplayerMatch match, PublishedAdventure content, LobbyState room, Guid actor, DateTimeOffset now)
    {
        var scene = MultiplayerGame.Scene(match);
        var sceneDefinition = match.Story.Scenes.Single(item => item.Key == match.SceneKey);
        var answer = match.Answers.SingleOrDefault(item => item.PlayerId == actor);
        var revealed = match.Phase is MultiplayerPhase.QuestionReveal or MultiplayerPhase.StoryResult;
        var questionVisible = match.Phase is MultiplayerPhase.QuestionOpen or MultiplayerPhase.QuestionReveal;
        var audio = content.Audio[match.SceneKey];
        var consumed = (match.Hints ?? []).Where(hint => hint.PlayerId == actor && hint.SceneInstanceId == match.SceneInstanceId).Select(hint => hint.Key).ToArray();
        var view = new AdventureView(content.Metadata.Title, match.SceneKey, content.SceneTitles[match.SceneKey], match.VisitedScenes.Count,
            scene.Narration, match.StateVersion, match.Phase.ToString(), now, match.PhaseDeadlineUtc,
            questionVisible ? new(match.SceneInstanceId, scene.Challenge.Prompt,
                scene.Challenge.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray()) : null,
            revealed ? new(answer?.OptionKey, answer?.IsCorrect ?? false, answer is null,
                sceneDefinition.Challenge.Explanation, scene.Support?.FullTranslation) : null,
            match.Phase == MultiplayerPhase.DecisionOpen ? scene.Transitions.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray() : [],
            new($"/media/catalog/{audio.Id}", audio.DurationMs, 2000),
            match.VisitedScenes.Select(key => new VisitedSceneView(key, content.SceneTitles[key])).ToArray(), content.Story.Id,
            AdminContracts.Appearance(content.Metadata.Appearance ?? new()),
            content.Visuals.TryGetValue(match.SceneKey, out var visual) ? new($"/media/visuals/{visual.Id}", scene.Support?.VisualDescription ?? visual.AltText, visual.Animated) : null,
            content.Metadata.Genre, scene.Support?.Dialogue.Select(cue =>
                new DialogueCueView(cue.Speaker, cue.Text, cue.StartMilliseconds, cue.EndMilliseconds)).ToArray(), match.SceneInstanceId,
            GameAssistance.Words(scene.Support, consumed, match.Rules.Hints),
            GameAssistance.Cost(match.Rules.Hints, consumed.Length, answer is null && match.Phase is MultiplayerPhase.Narrating or MultiplayerPhase.QuestionOpen),
            new(match.Players.Single(player => player.Id == actor).Score, revealed ? answer is { BasePoints: 0, Points: > 0 } legacy ? legacy.Points : answer?.BasePoints ?? 0 : null,
                revealed ? answer?.HintDeduction ?? 0 : null, revealed ? answer?.Points ?? 0 : null,
                "Acertos: 200, 180, 160… por ordem de chegada. Empate em até 300 ms. Erro ou prazo encerrado: 0."), content.Metadata.Language);
        var last = match.Decisions.LastOrDefault();
        var lastChoice = last is null ? null : match.Story.Restore().Scenes.Single(item => item.Key == last.SceneKey)
            .Transitions.Single(item => item.Key == last.TransitionKey);
        return new(match.Id, match.SceneInstanceId, view,
            match.Players.Select(player => new MatchPlayerView(player.Id, player.Nickname, player.Avatar, player.Score, player.CorrectAnswers,
                room.Members.Any(member => member.PlayerId == player.Id && now - member.LastSeenUtc < LobbyRoom.ConnectedWindow),
                match.AudioReady.Contains(player.Id), match.Answers.Any(item => item.PlayerId == player.Id),
                match.Votes.Any(item => item.PlayerId == player.Id), player.Left)).ToArray(),
            match.AudioReady.Contains(actor), answer is not null, answer?.OptionKey,
            match.Votes.Any(vote => vote.PlayerId == actor), match.Votes.SingleOrDefault(vote => vote.PlayerId == actor)?.TransitionKey,
            revealed ? match.Players.Select(player => new MatchResultView(player.Id,
                match.Answers.Any(item => item.PlayerId == player.Id && item.IsCorrect),
                match.Answers.SingleOrDefault(item => item.PlayerId == player.Id)?.Points ?? 0)).ToArray() : [],
            match.Phase == MultiplayerPhase.StoryResult ? match.Players.Where(player => !player.Left &&
                player.Score == match.Players.Where(item => !item.Left).Max(item => item.Score)).Select(player => player.Id).ToArray() : [],
            last is null ? null : new(content.SceneTitles[last.SceneKey], lastChoice!.Label, last.Tied, last.NoVotes), match.CancellationReason,
            match.Phase == MultiplayerPhase.StoryResult ? match.Players.OrderBy(player => player.Left).ThenByDescending(player => player.Score)
                .ThenBy(player => player.Nickname, StringComparer.OrdinalIgnoreCase).ThenBy(player => player.Id)
                .Select(player => new MatchStandingView(player.Id, player.Left ? null : 1 + match.Players.Count(other => !other.Left && other.Score > player.Score),
                    player.Score, player.Score - player.StartingScore, player.Left)).ToArray() : []);
    }
}

internal sealed class RoomMatchWorker(IServiceScopeFactory scopes, TimeProvider clock, IHubContext<RoomHub> hub,
    ILogger<RoomMatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                foreach (var state in await scope.ServiceProvider.GetRequiredService<ILobbyStore>().TickMatchesAsync(stoppingToken))
                { await hub.Clients.Group(RoomHub.Group(state.Id)).SendAsync("RoomChanged", state.Id, state.StateVersion, stoppingToken); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Failed to advance multiplayer matches; snapshots can retry the transition."); }
        }
    }
}
