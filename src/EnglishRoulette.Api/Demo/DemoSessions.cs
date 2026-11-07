using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnglishRoulette.Api.Admin;
using EnglishRoulette.Api.Multiplayer;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Presentation;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Infrastructure.Admin;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Api.Demo;

internal sealed class DemoSessions(ISoloSessionStore store, TimeProvider timeProvider, IAdminCatalog catalog,
    AdventureDbContext db, CatalogMediaStorage media)
{
    private const string CookieName = "englishroulette.demo";
    private const int SafetyMarginMs = 2000;
    private readonly PersistentSoloAdventure _adventure = new(store, timeProvider);

    public async Task<AdventureView?> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var hash = CredentialHash(context);
        if (hash is null) { return null; }
        var snapshot = await _adventure.GetAsync(hash, cancellationToken);
        return snapshot is null ? null : await MapAsync(snapshot, hash, cancellationToken);
    }

    public async Task<AdventureView> CreateAsync(HttpContext context, Guid? versionId, CancellationToken cancellationToken)
    {
        var previousHash = CredentialHash(context);
        var previous = previousHash is null ? null : await store.FindAsync(previousHash, cancellationToken);
        versionId ??= StoryRoulette.Draw(await catalog.PublishedAsync(cancellationToken));
        var content = await catalog.LoadPublishedAsync(versionId, cancellationToken)
            ?? throw new GameRuleException("CONTENT_NOT_PUBLISHED", "Escolha uma história publicada no catálogo.");
        var guest = await context.AuthenticateAsync(RoomEndpoints.PlayerScheme);
        var playerId = Guid.TryParse(guest.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var guestId)
            ? guestId : previous?.Checkpoint.PlayerId ?? Guid.NewGuid();
        var timings = content.Story.Scenes.Select(scene => new SceneTiming(scene.Key,
            TimeSpan.FromMilliseconds(content.Audio[scene.Key].DurationMs + SafetyMarginMs), TimeSpan.FromSeconds(20)));
        var session = new SoloAdventureSession(Guid.NewGuid(), playerId, content.Story, timings);
        var credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(credential);
        await store.CreateAsync(hash, session.Capture(), previousHash, cancellationToken);
        context.Response.Cookies.Append(CookieName, credential, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/api/demo",
            MaxAge = TimeSpan.FromDays(30)
        });
        return await MapAsync(session.GetSnapshot(playerId, timeProvider.GetUtcNow()), hash, cancellationToken);
    }

    public async Task<AdventureView?> ExecuteAsync(HttpContext context, AdventureCommand request, CancellationToken cancellationToken)
    {
        var hash = CredentialHash(context);
        if (hash is null) { return null; }
        if (request.Kind == "start")
        {
            var stored = await store.FindAsync(hash, cancellationToken);
            if (stored is null) { return null; }
            var content = await catalog.LoadVersionAsync(stored.Checkpoint.Story.Id, cancellationToken);
            if (content is null || !content.Audio.TryGetValue(stored.Checkpoint.SceneKey, out var audio))
            { throw new GameRuleException("INVALID_AUDIO", "Narração indisponível."); }
            var asset = await db.CatalogMedia.AsNoTracking().SingleAsync(row => row.Id == audio.Id, cancellationToken);
            if (!media.IsValid(asset)) { throw new GameRuleException("INVALID_AUDIO", "Narração indisponível."); }
        }
        SoloAdventureCommand command = request.Kind switch
        {
            "start" => new StartSoloNarration(request.RequestId, request.ExpectedStateVersion),
            "answer" when request.QuestionInstanceId is { } instanceId && request.OptionKey is { } option =>
                new SubmitSoloAnswer(request.RequestId, request.ExpectedStateVersion, instanceId, option),
            "continue" => new ContinueSoloAdventure(request.RequestId, request.ExpectedStateVersion),
            "choose" when request.TransitionKey is { } choice => new ChooseSoloPath(request.RequestId, request.ExpectedStateVersion, choice),
            _ => throw new ArgumentException("Comando inválido.", nameof(request))
        };
        var snapshot = await _adventure.ExecuteAsync(hash, command, cancellationToken);
        return snapshot is null ? null : await MapAsync(snapshot, hash, cancellationToken);
    }

    private static string? CredentialHash(HttpContext context) =>
        context.Request.Cookies.TryGetValue(CookieName, out var credential) ? Hash(credential) : null;
    private static string Hash(string credential) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential)));

    private async Task<AdventureView> MapAsync(SoloAdventureSnapshot snapshot, string hash, CancellationToken token)
    {
        var content = await catalog.LoadVersionAsync(snapshot.StoryVersionId, token)
            ?? throw new GameRuleException("NOT_FOUND", "O conteúdo desta aventura não está disponível.");
        var checkpoint = (await store.FindAsync(hash, token))?.Checkpoint;
        var keys = (checkpoint?.Receipts ?? []).Where(receipt => receipt.Snapshot.StateVersion <= snapshot.StateVersion)
            .OrderBy(receipt => receipt.Snapshot.StateVersion).Select(receipt => receipt.Snapshot.SceneKey)
            .Append(snapshot.SceneKey).Distinct(StringComparer.Ordinal).ToArray();
        var audio = content.Audio[snapshot.SceneKey];
        return new(snapshot.StoryTitle, snapshot.SceneKey, content.SceneTitles[snapshot.SceneKey], keys.Length, snapshot.Narration,
            snapshot.StateVersion, snapshot.Phase.ToString(), snapshot.ServerNowUtc, snapshot.PhaseDeadlineUtc,
            snapshot.Question is { } question
                ? new(question.InstanceId, question.Prompt, question.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray()) : null,
            snapshot.Answer is { } answer ? new(answer.OptionKey, answer.IsCorrect, answer.TimedOut, answer.Explanation) : null,
            snapshot.Choices.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray(),
            new($"/media/catalog/{audio.Id}", audio.DurationMs, SafetyMarginMs),
            keys.Select(key => new VisitedSceneView(key, content.SceneTitles[key])).ToArray(), snapshot.StoryVersionId, AdminContracts.Appearance(content.Metadata.Appearance ?? new()),
            content.Visuals.TryGetValue(snapshot.SceneKey, out var visual) ? new SceneVisualView($"/media/visuals/{visual.Id}", visual.AltText, visual.Animated) : null,
            content.Metadata.Genre);
    }
}
