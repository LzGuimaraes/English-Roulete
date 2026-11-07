using System.Security.Cryptography;
using System.Text;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain.Sessions;

namespace EnglishRoulette.Api.Demo;

internal sealed class DemoSessions(ISoloSessionStore store, TimeProvider timeProvider, DemoMediaCatalog media)
{
    private const string CookieName = "englishroulette.demo";
    private const int QuestionWindowSeconds = 20;
    private readonly PersistentSoloAdventure _adventure = new(store, timeProvider);

    public async Task<AdventureView?> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var hash = CredentialHash(context);
        if (hash is null) { return null; }
        var snapshot = await _adventure.GetAsync(hash, cancellationToken);
        return snapshot is null ? null : Map(snapshot);
    }

    public async Task<AdventureView> CreateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var previousHash = CredentialHash(context);
        var previous = previousHash is null ? null : await store.FindAsync(previousHash, cancellationToken);
        var story = DemoStory.Create();
        var playerId = previous?.Checkpoint.PlayerId ?? Guid.NewGuid();
        var timings = story.Scenes.Select(scene => new SceneTiming(scene.Key,
            TimeSpan.FromMilliseconds(media.Get(story.Id, scene.Key).DurationMs + DemoMediaCatalog.SafetyMarginMilliseconds),
            TimeSpan.FromSeconds(QuestionWindowSeconds)));
        var session = new SoloAdventureSession(Guid.NewGuid(), playerId, story, timings);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        await store.CreateAsync(hash, session.Capture(), previousHash, cancellationToken);
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/api/demo",
            MaxAge = TimeSpan.FromDays(30)
        });
        return Map(session.GetSnapshot(playerId, timeProvider.GetUtcNow()));
    }

    public async Task<AdventureView?> ExecuteAsync(HttpContext context, AdventureCommand request,
        CancellationToken cancellationToken)
    {
        var hash = CredentialHash(context);
        if (hash is null) { return null; }
        if (request.Kind == "start")
        {
            var stored = await store.FindAsync(hash, cancellationToken);
            if (stored is null) { return null; }
            media.Get(stored.Checkpoint.Story.Id, stored.Checkpoint.SceneKey);
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
        var snapshot = await _adventure.ExecuteAsync(hash, command, cancellationToken);
        return snapshot is null ? null : Map(snapshot);
    }

    private static string? CredentialHash(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out var token) ? Hash(token) : null;
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private AdventureView Map(SoloAdventureSnapshot snapshot)
    {
        var (title, number) = DemoStory.Describe(snapshot.SceneKey);
        var audio = media.Get(snapshot.StoryVersionId, snapshot.SceneKey);
        return new(snapshot.StoryTitle, snapshot.SceneKey, title, number, snapshot.Narration,
            snapshot.StateVersion, snapshot.Phase.ToString(), snapshot.ServerNowUtc, snapshot.PhaseDeadlineUtc,
            snapshot.Question is { } question
                ? new(question.InstanceId, question.Prompt, question.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray())
                : null,
            snapshot.Answer is { } answer ? new(answer.OptionKey, answer.IsCorrect, answer.TimedOut, answer.Explanation) : null,
            snapshot.Choices.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray(),
            new($"/media/audio/{audio.Sha256}.wav", audio.DurationMs, DemoMediaCatalog.SafetyMarginMilliseconds));
    }
}
