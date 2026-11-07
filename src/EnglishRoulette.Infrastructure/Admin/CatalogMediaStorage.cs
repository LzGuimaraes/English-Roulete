using System.Security.Cryptography;
using System.Text;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Domain;
using EnglishRoulette.Infrastructure.Media;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.Infrastructure.Admin;

public sealed class CatalogMediaStorage(string root, AdventureDbContext db, TimeProvider timeProvider)
{
    public const long MaximumUploadBytes = 10 * 1024 * 1024;
    public const int MaximumDurationMs = 120000;
    public string Root { get; } = Path.GetFullPath(root);
    public static string TextHash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public string? PathFor(CatalogMediaRow asset)
    {
        var path = Path.GetFullPath(Path.Combine(Root, asset.StorageKey));
        return path.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && File.Exists(path) ? path : null;
    }

    public bool IsValid(CatalogMediaRow asset)
    {
        var path = PathFor(asset);
        if (path is null) { return false; }
        using var stream = File.OpenRead(path);
        var sha = Convert.ToHexStringLower(SHA256.HashData(stream));
        stream.Position = 0;
        try { return stream.Length == asset.SizeBytes && sha == asset.Sha256 && WavAudio.DurationMilliseconds(stream) == asset.DurationMs; }
        catch (InvalidDataException) { return false; }
    }

    public async Task<CatalogAudio> UploadAsync(Guid versionId, Guid sceneId, long revision, Stream input,
        string attribution, string licenseEvidence, Guid actorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(attribution) || string.IsNullOrWhiteSpace(licenseEvidence) ||
            attribution.Length > 2000 || licenseEvidence.Length > 2000)
        { throw new GameRuleException("INVALID_AUDIO", "Informe créditos e evidência de direitos da narração."); }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM catalog_versions WHERE id = {versionId} FOR UPDATE", cancellationToken);
        var version = await db.CatalogVersions.SingleOrDefaultAsync(row => row.Id == versionId, cancellationToken)
            ?? throw new GameRuleException("NOT_FOUND", "Versão não encontrada.");
        if (version.Status != "Draft" || version.Revision != revision)
        { throw new GameRuleException("STATE_CONFLICT", "Salve um rascunho atualizado antes de enviar o áudio."); }
        var scene = await db.CatalogScenes.SingleOrDefaultAsync(row => row.Id == sceneId && row.VersionId == versionId, cancellationToken)
            ?? throw new GameRuleException("NOT_FOUND", "Cena não encontrada.");
        if (string.IsNullOrWhiteSpace(scene.Narration))
        { throw new GameRuleException("INVALID_AUDIO", "Salve o texto da cena antes de enviar sua narração."); }
        var id = Guid.NewGuid();
        var key = "private/audio/" + id.ToString("N") + ".wav";
        var path = Path.Combine(Root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Root);
        var saved = false;
        var commitAttempted = false;
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long written = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    written += count;
                    if (written > MaximumUploadBytes) { throw new GameRuleException("INVALID_AUDIO", "O áudio deve ter até 10 MB."); }
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
            }
            await using var file = File.OpenRead(path);
            int duration;
            try { duration = WavAudio.DurationMilliseconds(file); }
            catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
            { throw new GameRuleException("INVALID_AUDIO", "Envie um WAV PCM de 16 bits válido."); }
            if (duration > MaximumDurationMs) { throw new GameRuleException("INVALID_AUDIO", "Use uma narração de até dois minutos por cena."); }
            file.Position = 0;
            var asset = new CatalogMediaRow
            {
                Id = id,
                StorageKey = key,
                Sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken)),
                NarrationSha256 = TextHash(scene.Narration),
                SizeBytes = file.Length,
                DurationMs = duration,
                Attribution = attribution,
                LicenseEvidence = licenseEvidence,
                CreatedAtUtc = timeProvider.GetUtcNow()
            };
            db.CatalogMedia.Add(asset);
            scene.AudioId = id;
            version.Revision++;
            version.UpdatedAtUtc = timeProvider.GetUtcNow();
            db.AdminAudit.Add(new()
            {
                Id = Guid.NewGuid(),
                ActorId = actorId,
                TargetId = versionId,
                Action = "UploadNarration",
                AtUtc = timeProvider.GetUtcNow()
            });
            await db.SaveChangesAsync(cancellationToken);
            commitAttempted = true;
            await transaction.CommitAsync(cancellationToken);
            saved = true;
            return new(id, duration, attribution, licenseEvidence);
        }
        // A lost COMMIT response is ambiguous: retain a possible orphan rather than delete a committed asset.
        finally { if (!saved && !commitAttempted && File.Exists(path)) { File.Delete(path); } }
    }
}
