using System.Text.Json;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Presentation;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class AuthoredStoryCatalogTests
{
    public static IEnumerable<object[]> StoryPaths()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EnglishRoulette.sln")))
        { directory = directory.Parent; }
        if (directory is null) { throw new DirectoryNotFoundException("Run the content tests from a source checkout."); }
        return Directory.GetFiles(Path.Combine(directory.FullName, "content"), "story.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(path => new object[] { path });
    }

    [Theory]
    [MemberData(nameof(StoryPaths))]
    public void AuthoredStory_IsAcceptedByTheExistingEngine_AndBothDeclaredRoutesReachTheirOwnEnding(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");
        var appearance = JsonSerializer.Deserialize<CatalogAppearance>(metadata.GetProperty("appearance").GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var scenes = root.GetProperty("scenes").EnumerateArray().Select(scene => new CatalogScene(Guid.NewGuid(),
            Text(scene, "key"), Text(scene, "title"), Text(scene, "narration"), scene.GetProperty("isEnding").GetBoolean(),
            Text(scene, "prompt"), scene.GetProperty("options").EnumerateArray().Select(option =>
                new CatalogOption(Text(option, "key"), Text(option, "text"))).ToArray(),
            Text(scene, "correctOptionKey"), Text(scene, "explanation"),
            scene.GetProperty("transitions").EnumerateArray().Select(transition => new CatalogTransition(
                Text(transition, "key"), Text(transition, "label"), Text(transition, "targetSceneKey"))).ToArray(), null, Support: new SceneSupport
                {
                    Vocabulary = scene.TryGetProperty("vocabulary", out var vocabulary)
                    ? JsonSerializer.Deserialize<VocabularyHint[]>(vocabulary.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))! : [],
                    Dialogue = scene.TryGetProperty("dialogue", out var dialogue)
                    ? JsonSerializer.Deserialize<DialogueCue[]>(dialogue.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web))! : []
                })).ToArray();
        var draft = new CatalogDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "Draft", 1, Text(root, "initialSceneKey"),
            new(Text(metadata, "title"), Text(metadata, "synopsis"), Text(metadata, "genre"), Text(metadata, "difficulty"),
                Text(metadata, "author"), Text(metadata, "licenseType"), Text(metadata, "licenseEvidence"),
                Text(metadata, "attribution"), appearance, metadata.TryGetProperty("language", out var language) ? language.GetString()! : StoryLanguage.English), scenes);
        Assert.Empty(CatalogRules.DraftIssues(draft));
        var story = CatalogRules.ToStory(draft);
        StoryFlowEngine.ValidateForPlay(story);
        var endings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in root.GetProperty("authoringNotes").GetProperty("routes").EnumerateArray())
        {
            var keys = route.EnumerateArray().Select(key => key.GetString() ?? throw new InvalidDataException("Route key missing.")).ToArray();
            Assert.InRange(keys.Length, 2, 40);
            var current = StoryFlowEngine.GetInitialScene(story);
            Assert.Equal(keys[0], current.Key);
            foreach (var nextKey in keys.Skip(1))
            {
                var transition = Assert.Single(current.Transitions, choice => choice.TargetSceneKey == nextKey);
                var next = StoryFlowEngine.GetNextScene(story, current.Key, transition.Key);
                current = Assert.IsType<StoryScene>(next);
                Assert.Equal(nextKey, current.Key);
            }
            Assert.True(current.IsEnding);
            Assert.Null(StoryFlowEngine.GetNextScene(story, current.Key));
            Assert.True(endings.Add(current.Key));
        }
        Assert.Equal(2, endings.Count);
    }

    private static string Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidDataException($"Missing {name} in authored content.");
}
