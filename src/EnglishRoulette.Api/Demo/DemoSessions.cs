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
using EnglishRoulette.Domain.Stories;
using EnglishRoulette.Infrastructure.Admin;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Api.Demo;

internal sealed class DemoSessions(ISoloSessionStore store, TimeProvider timeProvider, IAdminCatalog catalog,
    AdventureDbContext db, CatalogMediaStorage media, IConfiguration configuration)
{
    private const string CookieName = "englishroulette.demo";
    private const int SafetyMarginMs = 2000;
    private readonly PersistentSoloAdventure _adventure = new(store, timeProvider);

    public async Task EndAsync(HttpContext context, CancellationToken token)
    {
        var hash = CredentialHash(context);
        if (hash is not null) { await store.DeleteAsync(hash, token); }
        // Old persistent cookies are accepted only for cleanup, never for play/resume.
        if (context.Request.Cookies.TryGetValue(CookieName, out var legacy))
        { await store.DeleteAsync(Hash(legacy), token); }
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/api/demo" });
    }

    public async Task<AdventureView?> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var hash = CredentialHash(context);
        if (hash is null) { return null; }
        var snapshot = await _adventure.GetAsync(hash, cancellationToken);
        return snapshot is null ? null : await MapAsync(snapshot, hash, cancellationToken);
    }

    public async Task<AdventureView> CreateAsync(HttpContext context, Guid? versionId, CancellationToken cancellationToken, string language = StoryLanguage.English)
    {
        language = StoryLanguage.Require(language);
        var previousHash = CredentialHash(context);
        var previous = previousHash is null ? null : await store.FindAsync(previousHash, cancellationToken);
        versionId ??= StoryRoulette.Draw(await catalog.PublishedAsync(cancellationToken), language: language);
        var content = await catalog.LoadPublishedAsync(versionId, cancellationToken)
            ?? throw new GameRuleException("CONTENT_NOT_PUBLISHED", "Escolha uma história publicada no catálogo.");
        if (content.Metadata.Language != language)
        { throw new GameRuleException("LANGUAGE_MISMATCH", "Esta história pertence a outro idioma. Volte ao catálogo e escolha o idioma desejado."); }
        var guest = await context.AuthenticateAsync(RoomEndpoints.PlayerScheme);
        var playerId = Guid.TryParse(guest.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var guestId)
            ? guestId : previous?.Checkpoint.PlayerId ?? Guid.NewGuid();
        var timings = content.Story.Scenes.Select(scene => new SceneTiming(scene.Key,
            TimeSpan.FromMilliseconds(content.Audio[scene.Key].DurationMs + SafetyMarginMs), TimeSpan.FromSeconds(20)));
        var session = new SoloAdventureSession(Guid.NewGuid(), playerId, content.Story, timings, GameAssistance.Rules(configuration));
        var credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(credential);
        await store.CreateAsync(hash, session.Capture(), previousHash, cancellationToken);
        context.Response.Headers[SoloSessionHeaders.Credential] = credential;
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
            "hint" when request.SceneInstanceId is { } sceneInstance && request.HintKey is { } hint =>
                new UseSoloHint(request.RequestId, request.ExpectedStateVersion, sceneInstance, hint),
            "continue" => new ContinueSoloAdventure(request.RequestId, request.ExpectedStateVersion),
            "choose" when request.TransitionKey is { } choice => new ChooseSoloPath(request.RequestId, request.ExpectedStateVersion, choice),
            _ => throw new ArgumentException("Comando inválido.", nameof(request))
        };
        var snapshot = await _adventure.ExecuteAsync(hash, command, cancellationToken);
        return snapshot is null ? null : await MapAsync(snapshot, hash, cancellationToken);
    }

    private static string? CredentialHash(HttpContext context)
    {
        var values = context.Request.Headers[SoloSessionHeaders.Credential];
        var credential = values.Count == 1 ? values[0] : null;
        return credential is { Length: 64 } && credential.All(char.IsAsciiHexDigit) ? Hash(credential) : null;
    }
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
        var scene = content.Story.Scenes.Single(item => item.Key == snapshot.SceneKey);
        var hints = snapshot.UsedHintKeys ?? [];
        var round = snapshot.RoundScores?.SingleOrDefault(item => item.SceneInstanceId == snapshot.SceneInstanceId);
        return new(snapshot.StoryTitle, snapshot.SceneKey, content.SceneTitles[snapshot.SceneKey], keys.Length, snapshot.Narration,
            snapshot.StateVersion, snapshot.Phase.ToString(), snapshot.ServerNowUtc, snapshot.PhaseDeadlineUtc,
            snapshot.Question is { } question
                ? new(question.InstanceId, question.Prompt, question.Options.Select(option => new OptionView(option.Key, option.Text)).ToArray()) : null,
            snapshot.Answer is { } answer ? new(answer.OptionKey, answer.IsCorrect, answer.TimedOut, answer.Explanation) : null,
            snapshot.Choices.Select(choice => new ChoiceView(choice.Key, choice.Label)).ToArray(),
            new($"/media/catalog/{audio.Id}", audio.DurationMs, SafetyMarginMs),
            keys.Select(key => new VisitedSceneView(key, content.SceneTitles[key])).ToArray(), snapshot.StoryVersionId, AdminContracts.Appearance(content.Metadata.Appearance ?? new()),
            content.Visuals.TryGetValue(snapshot.SceneKey, out var visual) ? new SceneVisualView($"/media/visuals/{visual.Id}", scene.Support?.VisualDescription ?? visual.AltText, visual.Animated) : null,
            content.Metadata.Genre,
            content.Story.Scenes.Single(scene => scene.Key == snapshot.SceneKey).Support?.Dialogue.Select(cue =>
                new DialogueCueView(cue.Speaker, cue.Text, cue.StartMilliseconds, cue.EndMilliseconds)).ToArray(), snapshot.SceneInstanceId,
            GameAssistance.Words(scene.Support, hints, snapshot.ScoringRules?.Hints),
            GameAssistance.Cost(snapshot.ScoringRules?.Hints, hints.Count, snapshot.Phase is SoloAdventurePhase.Narrating or SoloAdventurePhase.QuestionOpen),
            snapshot.ScoringRules is { } rules ? new((snapshot.RoundScores ?? []).Sum(item => item.Points), round?.BasePoints, round?.Deduction, round?.Points,
                GameAssistance.SoloDescription(rules)) : null, content.Metadata.Language);
    }
}
