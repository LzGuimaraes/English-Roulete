using EnglishRoulette.Application.Admin;
using EnglishRoulette.Domain;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Admin;

public sealed class CatalogService(AdventureDbContext db, TimeProvider timeProvider, CatalogMediaStorage media) : IAdminCatalog
{
    private IQueryable<CatalogVersionRow> Complete() => db.CatalogVersions.AsNoTracking().AsSplitQuery()
        .Include(version => version.Scenes).ThenInclude(scene => scene.Options)
        .Include(version => version.Scenes).ThenInclude(scene => scene.Transitions)
        .Include(version => version.Scenes).ThenInclude(scene => scene.Audio);

    public async Task<IReadOnlyList<CatalogSummary>> ListAsync(int page, CancellationToken cancellationToken) =>
        await db.CatalogVersions.AsNoTracking().OrderByDescending(version => version.UpdatedAtUtc).ThenBy(version => version.Id)
            .Skip((Math.Clamp(page, 1, 10000) - 1) * 25).Take(25).Select(version => new CatalogSummary(
                version.Id, version.StoryId, version.Number, version.Title, version.Genre, version.Difficulty,
                version.Status, version.Revision, version.UpdatedAtUtc)).ToListAsync(cancellationToken);

    public async Task<CatalogDraft?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await Complete().SingleOrDefaultAsync(version => version.Id == id, cancellationToken) is { } row ? Map(row) : null;

    public async Task<CatalogDraft> CreateAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var story = new CatalogStoryRow { Id = Guid.NewGuid(), Slug = "story-" + Guid.NewGuid().ToString("N"), CreatedAtUtc = now };
        var version = new CatalogVersionRow { Id = Guid.NewGuid(), StoryId = story.Id, Number = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.CatalogStories.Add(story);
        db.CatalogVersions.Add(version);
        Audit(actorId, version.Id, "CreateDraft");
        await db.SaveChangesAsync(cancellationToken);
        return Map(version);
    }

    public async Task<CatalogDraft> SaveAsync(CatalogDraft draft, Guid actorId, CancellationToken cancellationToken)
    {
        var issues = CatalogRules.DraftIssues(draft);
        if (issues.Count != 0) { throw new GameRuleException("INVALID_CONTENT", string.Join(" ", issues)); }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockVersion(draft.Id, cancellationToken);
        var version = await db.CatalogVersions.Include(row => row.Scenes).ThenInclude(scene => scene.Options)
            .Include(row => row.Scenes).ThenInclude(scene => scene.Transitions).AsSplitQuery()
            .SingleAsync(row => row.Id == draft.Id, cancellationToken);
        EnsureDraft(version, draft.Revision);
        var originalIds = version.Scenes.Select(scene => scene.Id).ToHashSet();
        if (draft.Scenes.Any(scene => scene.Id != Guid.Empty && !originalIds.Contains(scene.Id)))
        { throw new GameRuleException("INVALID_CONTENT", "Uma cena não pertence a este rascunho."); }
        var audioIds = draft.Scenes.Where(scene => scene.Audio is not null).Select(scene => scene.Audio!.Id).Distinct().ToArray();
        var assets = await db.CatalogMedia.Where(asset => audioIds.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken);
        if (assets.Count != audioIds.Length) { throw new GameRuleException("INVALID_AUDIO", "Um áudio selecionado não existe."); }
        // Replace only draft children, inside the same transaction. Published versions are never edited.
        db.RemoveRange(version.Scenes);
        version.Scenes = [];
        await db.SaveChangesAsync(cancellationToken);
        ApplyMetadata(version, draft.Metadata);
        version.InitialSceneKey = draft.InitialSceneKey;
        version.Scenes = draft.Scenes.Select((scene, position) => SceneRow(scene, version.Id, position, assets)).ToList();
        db.CatalogScenes.AddRange(version.Scenes);
        version.Revision++;
        version.UpdatedAtUtc = timeProvider.GetUtcNow();
        Audit(actorId, version.Id, "SaveDraft");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(version);
    }

    public async Task<CatalogDraft> DuplicateAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var original = await FindAsync(id, cancellationToken) ?? throw new GameRuleException("NOT_FOUND", "História não encontrada.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockStory(original.StoryId, cancellationToken);
        await LockVersion(id, cancellationToken);
        original = await FindAsync(id, cancellationToken) ?? throw new GameRuleException("NOT_FOUND", "História não encontrada.");
        var number = await db.CatalogVersions.Where(version => version.StoryId == original.StoryId).MaxAsync(version => version.Number, cancellationToken) + 1;
        var now = timeProvider.GetUtcNow();
        var copy = new CatalogVersionRow
        {
            Id = Guid.NewGuid(),
            StoryId = original.StoryId,
            Number = number,
            InitialSceneKey = original.InitialSceneKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ApplyMetadata(copy, original.Metadata);
        var audioIds = original.Scenes.Where(scene => scene.Audio is not null).Select(scene => scene.Audio!.Id).Distinct().ToArray();
        var assets = await db.CatalogMedia.Where(asset => audioIds.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken);
        copy.Scenes = original.Scenes.Select((scene, position) => SceneRow(scene with { Id = Guid.NewGuid() }, copy.Id, position, assets)).ToList();
        db.CatalogVersions.Add(copy);
        Audit(actorId, copy.Id, "DuplicateDraft");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(copy);
    }

    public async Task<PublicationCheck> ValidateAsync(Guid id, CancellationToken cancellationToken)
    {
        var draft = await FindAsync(id, cancellationToken) ?? throw new GameRuleException("NOT_FOUND", "História não encontrada.");
        var issues = CatalogRules.PublicationIssues(draft).ToList();
        var ids = draft.Scenes.Where(scene => scene.Audio is not null).Select(scene => scene.Audio!.Id).Distinct().ToArray();
        var assets = await db.CatalogMedia.AsNoTracking().Where(asset => ids.Contains(asset.Id))
            .ToDictionaryAsync(asset => asset.Id, cancellationToken);
        foreach (var scene in draft.Scenes.Where(scene => scene.Audio is not null))
        {
            var asset = assets[scene.Audio!.Id];
            if (asset.NarrationSha256 != CatalogMediaStorage.TextHash(scene.Narration))
            { issues.Add($"Cena {scene.Key}: o texto mudou depois do envio do áudio. Envie a narração correspondente."); }
            if (!media.IsValid(asset)) { issues.Add($"Cena {scene.Key}: o arquivo de áudio está ausente ou inválido."); }
            if (string.IsNullOrWhiteSpace(asset.Attribution) || string.IsNullOrWhiteSpace(asset.LicenseEvidence))
            { issues.Add($"Cena {scene.Key}: faltam créditos ou evidência de direitos do áudio."); }
        }
        return new(issues);
    }

    public async Task PublishAsync(Guid id, long revision, bool reviewed, bool rightsConfirmed, Guid actorId, CancellationToken cancellationToken)
    {
        var current = await FindAsync(id, cancellationToken) ?? throw new GameRuleException("NOT_FOUND", "História não encontrada.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockStory(current.StoryId, cancellationToken);
        await LockVersion(id, cancellationToken);
        current = await FindAsync(id, cancellationToken) ?? throw new GameRuleException("NOT_FOUND", "História não encontrada.");
        var row = await db.CatalogVersions.SingleAsync(version => version.Id == id, cancellationToken);
        EnsureDraft(row, revision);
        var check = await ValidateAsync(id, cancellationToken);
        var issues = check.Issues.ToList();
        if (!reviewed) { issues.Add("Confirme a revisão de texto, perguntas e correspondência da narração."); }
        if (!rightsConfirmed) { issues.Add("Confirme as permissões de uso, adaptação e distribuição."); }
        if (issues.Count != 0) { throw new GameRuleException("PUBLICATION_BLOCKED", string.Join(" ", issues)); }
        await db.CatalogVersions.Where(version => version.StoryId == row.StoryId && version.Status == "Published")
            .ExecuteUpdateAsync(update => update.SetProperty(version => version.Status, "Archived")
                .SetProperty(version => version.Revision, version => version.Revision + 1), cancellationToken);
        var assetIds = current.Scenes.Where(scene => scene.Audio is not null).Select(scene => scene.Audio!.Id).ToArray();
        await db.CatalogMedia.Where(asset => assetIds.Contains(asset.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(asset => asset.Published, true), cancellationToken);
        row.Status = "Published";
        row.Revision++;
        row.PublishedAtUtc = timeProvider.GetUtcNow();
        row.UpdatedAtUtc = timeProvider.GetUtcNow();
        Audit(actorId, row.Id, "PublishReviewedAndRightsConfirmed");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, long revision, Guid actorId, CancellationToken cancellationToken)
    {
        var row = await db.CatalogVersions.SingleAsync(version => version.Id == id, cancellationToken);
        if (row.Revision != revision) { throw Conflict(); }
        row.Status = "Archived";
        row.Revision++;
        row.UpdatedAtUtc = timeProvider.GetUtcNow();
        Audit(actorId, id, "Archive");
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogSummary>> PublishedAsync(CancellationToken cancellationToken) =>
        await db.CatalogVersions.AsNoTracking().Where(version => version.Status == "Published")
            .OrderBy(version => version.CreatedAtUtc).Take(100).Select(version => new CatalogSummary(version.Id,
                version.StoryId, version.Number, version.Title, version.Genre, version.Difficulty, version.Status,
                version.Revision, version.UpdatedAtUtc)).ToListAsync(cancellationToken);

    public async Task<PublishedAdventure?> LoadPublishedAsync(Guid? id, CancellationToken cancellationToken)
    {
        var query = Complete().Where(version => version.Status == "Published");
        var row = id is { } value ? await query.SingleOrDefaultAsync(version => version.Id == value, cancellationToken)
            : await query.OrderBy(version => version.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Published(row);
    }

    public async Task<PublishedAdventure?> LoadVersionAsync(Guid id, CancellationToken cancellationToken) =>
        await Complete().SingleOrDefaultAsync(version => version.Id == id, cancellationToken) is { } row ? Published(row) : null;

    private static PublishedAdventure Published(CatalogVersionRow row)
    {
        var draft = Map(row);
        return new(CatalogRules.ToStory(draft), draft.Metadata,
            draft.Scenes.ToDictionary(scene => scene.Key, scene => scene.Title),
            draft.Scenes.Where(scene => scene.Audio is not null).ToDictionary(scene => scene.Key, scene => scene.Audio!));
    }

    public static CatalogDraft Map(CatalogVersionRow row) => new(row.Id, row.StoryId, row.Number, row.Status, row.Revision,
        row.InitialSceneKey, new(row.Title, row.Synopsis, row.Genre, row.Difficulty, row.Author, row.LicenseType, row.LicenseEvidence, row.Attribution),
        row.Scenes.OrderBy(scene => scene.Position).Select(scene => new CatalogScene(scene.Id, scene.Key, scene.Title, scene.Narration, scene.IsEnding,
            scene.Prompt, scene.Options.OrderBy(option => option.Position).Select(option => new CatalogOption(option.Key, option.Text)).ToArray(),
            scene.CorrectOptionKey, scene.Explanation, scene.Transitions.OrderBy(transition => transition.Position)
                .Select(transition => new CatalogTransition(transition.Key, transition.Label, transition.TargetSceneKey)).ToArray(),
            scene.Audio is { } audio ? new CatalogAudio(audio.Id, audio.DurationMs, audio.Attribution, audio.LicenseEvidence) : null)).ToArray());

    public static CatalogSceneRow SceneRow(CatalogScene scene, Guid versionId, int position, IReadOnlyDictionary<Guid, CatalogMediaRow> assets) => new()
    {
        Id = scene.Id == Guid.Empty ? Guid.NewGuid() : scene.Id,
        VersionId = versionId,
        Key = scene.Key,
        Title = scene.Title,
        Position = position,
        Narration = scene.Narration,
        IsEnding = scene.IsEnding,
        Prompt = scene.Prompt,
        CorrectOptionKey = scene.CorrectOptionKey,
        Explanation = scene.Explanation,
        AudioId = scene.Audio?.Id,
        Audio = scene.Audio is { } audio ? assets[audio.Id] : null,
        Options = scene.Options.Select((option, index) => new CatalogOptionRow { Id = Guid.NewGuid(), Key = option.Key, Text = option.Text, Position = index }).ToList(),
        Transitions = scene.Transitions.Select((transition, index) => new CatalogTransitionRow
        {
            Id = Guid.NewGuid(),
            Key = transition.Key,
            Label = transition.Label,
            TargetSceneKey = transition.TargetSceneKey,
            Position = index
        }).ToList()
    };

    private static void ApplyMetadata(CatalogVersionRow row, CatalogMetadata metadata)
    {
        row.Title = metadata.Title; row.Synopsis = metadata.Synopsis; row.Genre = metadata.Genre; row.Difficulty = metadata.Difficulty;
        row.Author = metadata.Author; row.LicenseType = metadata.LicenseType; row.LicenseEvidence = metadata.LicenseEvidence; row.Attribution = metadata.Attribution;
    }
    private static void EnsureDraft(CatalogVersionRow row, long revision)
    {
        if (row.Status != "Draft") { throw new GameRuleException("PUBLISHED_IMMUTABLE", "Duplique a versão para editar um novo rascunho."); }
        if (row.Revision != revision) { throw Conflict(); }
    }
    private Task<int> LockStory(Guid id, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM catalog_stories WHERE id = {id} FOR UPDATE", cancellationToken);
    private Task<int> LockVersion(Guid id, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM catalog_versions WHERE id = {id} FOR UPDATE", cancellationToken);
    private static GameRuleException Conflict() => new("STATE_CONFLICT", "O conteúdo foi alterado. Recarregue antes de salvar.");
    private void Audit(Guid actorId, Guid targetId, string action) => db.AdminAudit.Add(new()
    { Id = Guid.NewGuid(), ActorId = actorId, TargetId = targetId, Action = action, AtUtc = timeProvider.GetUtcNow() });
}
