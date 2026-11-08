using EnglishRoulette.Api.Admin;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Multiplayer;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Challenges;
using EnglishRoulette.Domain.Multiplayer;
using EnglishRoulette.Domain.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace EnglishRoulette.Api.Multiplayer;

// Public projection only. The persisted match includes answer keys and stays on the server.
internal sealed class RoomGames(IAdminCatalog catalog, ILobbyStore lobbies, TimeProvider clock)
{
    internal static MultiplayerMatch Start(PublishedAdventure content, LobbyState state, DateTimeOffset now) =>
        MultiplayerGame.Create(content.Story, content.Story.Scenes.Select(scene => new SceneTiming(scene.Key,
            TimeSpan.FromMilliseconds(content.Audio[scene.Key].DurationMs + 2000), TimeSpan.FromSeconds(15))).ToArray(),
            state.Members, now, (state.RouletteStartedAtUtc ?? now).AddSeconds(5));

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
                    current with { Match = Start(content, current, now), StateVersion = current.StateVersion + 1 }, token);
            }
        }
        var nowUtc = clock.GetUtcNow();
        return new(state.Id, state.Code, state.Capacity, state.HostId, actor, state.StateVersion, state.StoryVersionId,
            state.Members.Select(member => new RoomMemberView(member.PlayerId, member.Nickname, member.Avatar, member.Ready,
                nowUtc - member.LastSeenUtc < LobbyRoom.ConnectedWindow)).ToArray(), nowUtc, state.RouletteStartedAtUtc,
            state.Match is { } match && content is not null ? Map(match, content, state, actor, nowUtc) : null);
    }
    private static MultiplayerMatchView Map(MultiplayerMatch match, PublishedAdventure content, LobbyState room, Guid actor, DateTimeOffset now)
    {
        var scene = MultiplayerGame.Scene(match);
        var answer = match.Answers.SingleOrDefault(item => item.PlayerId == actor);
        var revealed = match.Phase is MultiplayerPhase.QuestionReveal or MultiplayerPhase.StoryResult;
        var questionVisible = match.Phase is MultiplayerPhase.QuestionOpen or MultiplayerPhase.QuestionReveal;
        var audio = content.Audio[match.SceneKey];
        var view = new AdventureView(content.Metadata.Title, match.SceneKey, content.SceneTitles[match.SceneKey], match.VisitedScenes.Count,
            scene.Narration, match.StateVersion, match.Phase.ToString(), now, match.PhaseDeadlineUtc,
            questionVisible ? new(match.SceneInstanceId, scene.Challenge.Prompt,
                scene.Challenge.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray()) : null,
            revealed ? new(answer?.OptionKey, answer?.IsCorrect ?? false, answer is null,
                ChallengeEvaluator.Evaluate(scene.Challenge, scene.Challenge.Options[0].Key).Explanation) : null,
            match.Phase == MultiplayerPhase.DecisionOpen ? scene.Transitions.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray() : [],
            new($"/media/catalog/{audio.Id}", audio.DurationMs, 2000),
            match.VisitedScenes.Select(key => new VisitedSceneView(key, content.SceneTitles[key])).ToArray(), content.Story.Id,
            AdminContracts.Appearance(content.Metadata.Appearance ?? new()),
            content.Visuals.TryGetValue(match.SceneKey, out var visual) ? new($"/media/visuals/{visual.Id}", visual.AltText, visual.Animated) : null,
            content.Metadata.Genre);
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
            last is null ? null : new(content.SceneTitles[last.SceneKey], lastChoice!.Label, last.Tied, last.NoVotes), match.CancellationReason);
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
