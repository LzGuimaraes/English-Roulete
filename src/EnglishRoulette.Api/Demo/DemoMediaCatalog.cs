using System.Security.Cryptography;
using System.Text.Json;
using EnglishRoulette.Domain;
using EnglishRoulette.Infrastructure.Media;

namespace EnglishRoulette.Api.Demo;

internal sealed record DemoAudioAsset(Guid StoryVersionId, string SceneKey, string Sha256, int DurationMs, long SizeBytes);

internal sealed class DemoMediaCatalog
{
    public const int SafetyMarginMilliseconds = 2000;
    private readonly string _root;
    private readonly Dictionary<(Guid, string), DemoAudioAsset> _assets;
    private readonly HashSet<string> _publishedHashes;

    public DemoMediaCatalog(IHostEnvironment environment, IConfiguration configuration)
    {
        _root = configuration["Media:Root"] ?? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../media"));
        var manifest = Path.Combine(_root, "manifest.json");
        if (!File.Exists(manifest))
        {
            throw new InvalidOperationException("Generate prerecorded narration with python3 scripts/generate_demo_audio.py.");
        }
        var entries = JsonSerializer.Deserialize<DemoAudioAsset[]>(File.ReadAllText(manifest),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        _assets = entries.ToDictionary(asset => (asset.StoryVersionId, asset.SceneKey));
        _publishedHashes = entries.Select(asset => asset.Sha256).ToHashSet(StringComparer.Ordinal);
        foreach (var asset in entries)
        {
            if (asset.Sha256.Length != 64 || asset.Sha256.Any(character => !char.IsAsciiHexDigit(character)) ||
                asset.StoryVersionId == Guid.Empty || string.IsNullOrWhiteSpace(asset.SceneKey)) { throw Invalid(); }
            var path = Path.Combine(_root, "audio", asset.Sha256 + ".wav");
            using var file = File.OpenRead(path);
            var hash = Convert.ToHexStringLower(SHA256.HashData(file));
            file.Position = 0;
            if (hash != asset.Sha256 || file.Length != asset.SizeBytes ||
                WavAudio.DurationMilliseconds(file) != asset.DurationMs) { throw Invalid(); }
        }
    }

    public DemoAudioAsset Get(Guid storyVersionId, string sceneKey)
    {
        if (!_assets.TryGetValue((storyVersionId, sceneKey), out var asset) || FilePath(asset.Sha256) is null)
        {
            throw new GameRuleException("MEDIA_UNAVAILABLE", "A narração desta cena não está disponível. Tente novamente.");
        }
        return asset;
    }

    public string? FilePath(string hash)
    {
        if (!_publishedHashes.Contains(hash)) { return null; }
        var path = Path.Combine(_root, "audio", hash + ".wav");
        return File.Exists(path) ? path : null;
    }

    private static InvalidDataException Invalid() => new("The published narration metadata does not match its audio file.");
}
