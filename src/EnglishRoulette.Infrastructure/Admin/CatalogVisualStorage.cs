using System.Security.Cryptography;
using EnglishRoulette.Domain;
using EnglishRoulette.Infrastructure.Media;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Admin;

public sealed class CatalogVisualStorage(CatalogMediaStorage media, AdventureDbContext db, TimeProvider clock)
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    public string? PathFor(CatalogVisualRow asset) => media.PathFor(new CatalogMediaRow { StorageKey = asset.StorageKey });
    public bool IsValid(CatalogVisualRow asset)
    {
        var path = PathFor(asset);
        return path is not null && new FileInfo(path).Length == asset.SizeBytes &&
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) == asset.Sha256;
    }
    public async Task UploadAsync(Guid versionId, Guid sceneId, long revision, Stream input, string altText,
        string attribution, string evidence, Guid actor, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(altText) || altText.Length > 300 || string.IsNullOrWhiteSpace(attribution) ||
            attribution.Length > 2000 || string.IsNullOrWhiteSpace(evidence) || evidence.Length > 2000)
        { throw new GameRuleException("INVALID_CONTENT", "Informe descrição acessível, créditos e licença da imagem."); }
        using var buffer = new MemoryStream();
        var chunk = new byte[81920]; int count;
        while ((count = await input.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + count > MaximumBytes) { throw new GameRuleException("INVALID_CONTENT", "A imagem deve ter até 8 MB."); }
            await buffer.WriteAsync(chunk.AsMemory(0, count), token);
        }
        var bytes = buffer.ToArray();
        ImageInfo info;
        try { info = ImageInspection.Inspect(bytes); }
        catch (InvalidDataException error) { throw new GameRuleException("INVALID_CONTENT", error.Message); }
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM catalog_versions WHERE id = {versionId} FOR UPDATE", token);
        var version = await db.CatalogVersions.SingleOrDefaultAsync(row => row.Id == versionId, token)
            ?? throw new GameRuleException("NOT_FOUND", "Versão não encontrada.");
        if (version.Status != "Draft" || version.Revision != revision)
        { throw new GameRuleException("STATE_CONFLICT", "Recarregue e salve o rascunho antes de enviar a imagem."); }
        var scene = await db.CatalogScenes.SingleOrDefaultAsync(row => row.Id == sceneId && row.VersionId == versionId, token)
            ?? throw new GameRuleException("NOT_FOUND", "Cena não encontrada.");
        var id = Guid.NewGuid(); var key = "private/visual/" + id.ToString("N");
        var asset = new CatalogVisualRow
        {
            Id = id,
            StorageKey = key,
            ContentType = info.ContentType,
            Width = info.Width,
            Height = info.Height,
            Animated = info.Animated,
            SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            AltText = altText,
            Attribution = attribution,
            LicenseEvidence = evidence,
            CreatedAtUtc = clock.GetUtcNow()
        };
        var path = Path.Combine(media.Root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var commitAttempted = false;
        try
        {
            await File.WriteAllBytesAsync(path, bytes, token);
            db.CatalogVisuals.Add(asset);
            scene.VisualId = id; version.Revision++; version.UpdatedAtUtc = clock.GetUtcNow();
            db.AdminAudit.Add(new() { Id = Guid.NewGuid(), ActorId = actor, TargetId = versionId, Action = "UploadVisual", AtUtc = clock.GetUtcNow() });
            await db.SaveChangesAsync(token);
            commitAttempted = true;
            await transaction.CommitAsync(token);
        }
        finally { if (!commitAttempted && File.Exists(path)) { File.Delete(path); } }
    }
}
