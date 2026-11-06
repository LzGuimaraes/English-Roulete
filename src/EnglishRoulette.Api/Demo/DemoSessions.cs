using System.Collections.Concurrent;
using System.Security.Cryptography;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.Api.Demo;

internal sealed class DemoSessions
{
    private const string CookieName = "englishroulette.demo";
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);
    private readonly SoloAdventureCoordinator _coordinator = new(TimeProvider.System);

    public AdventureView? Get(HttpContext context) => Find(context) is { } entry
        ? Map(_coordinator.GetSnapshot(entry.Session, entry.PlayerId)) : null;

    public AdventureView Create(HttpContext context)
    {
        var story = DemoStory.Create();
        var playerId = Guid.NewGuid();
        var timings = story.Scenes.Select(scene => new SceneTiming(scene.Key,
            TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(20)));
        var entry = new Entry(playerId, new(Guid.NewGuid(), playerId, story, timings));
        if (context.Request.Cookies.TryGetValue(CookieName, out var previous))
        {
            _sessions.TryRemove(previous, out _);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = entry;
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/api/demo",
            MaxAge = TimeSpan.FromHours(8)
        });
        return Map(_coordinator.GetSnapshot(entry.Session, playerId));
    }

    public AdventureView? Execute(HttpContext context, AdventureCommand request)
    {
        var entry = Find(context);
        if (entry is null)
        {
            return null;
        }

        SoloAdventureCommand command = request.Kind switch
        {
            "start" => new StartSoloNarration(request.RequestId, request.ExpectedStateVersion),
            "answer" when request.QuestionInstanceId is { } instanceId && request.OptionKey is { } option =>
                new SubmitSoloAnswer(request.RequestId, request.ExpectedStateVersion, instanceId, option),
            "continue" => new ContinueSoloAdventure(request.RequestId, request.ExpectedStateVersion),
            "choose" when request.TransitionKey is { } choice =>
                new ChooseSoloPath(request.RequestId, request.ExpectedStateVersion, choice),
            _ => throw new ArgumentException("Comando inválido.", nameof(request))
        };
        return Map(_coordinator.Execute(entry.Session, entry.PlayerId, command));
    }

    private Entry? Find(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out var token) && _sessions.TryGetValue(token, out var entry)
            ? entry : null;

    private static AdventureView Map(SoloAdventureSnapshot snapshot)
    {
        var (title, number) = DemoStory.Describe(snapshot.SceneKey);
        return new(snapshot.StoryTitle, snapshot.SceneKey, title, number, snapshot.Narration,
            snapshot.StateVersion, snapshot.Phase.ToString(), snapshot.ServerNowUtc, snapshot.PhaseDeadlineUtc,
            snapshot.Question is { } question
                ? new(question.InstanceId, question.Prompt, question.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray())
                : null,
            snapshot.Answer is { } answer ? new(answer.OptionKey, answer.IsCorrect, answer.TimedOut, answer.Explanation) : null,
            snapshot.Choices.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray());
    }

    private sealed record Entry(Guid PlayerId, SoloAdventureSession Session);
}
