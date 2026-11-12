using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class SceneSupportTests
{
    private const string Text = "The ledger is empty. Wait here.";
    private static SceneSupport Valid() => new()
    {
        FullTranslation = "O livro de registros está vazio. Espere aqui.",
        Vocabulary = [new("ledger", "ledger", "livro de registros")],
        Dialogue = [new("narrator", "Narrador", "The ledger is empty.", 0, 2000), new("anna", "Anna", "Wait here.", 2100, 3000)]
    };

    [Fact]
    public void ValidContent_PreservesWordsAndExplicitDialogueInFrozenStory()
    {
        var hints = Valid().Vocabulary.ToList();
        var support = Valid() with { Vocabulary = hints };
        var scene = new StoryScene(StoryFixture.VersionId, "platform", Text, StoryFixture.Challenge(), true, [], support);
        hints.Clear();
        Assert.Single(scene.Support!.Vocabulary);
        var story = StoryFixture.WithScenes(scene);
        var restored = StoryDefinition.Capture(story).Restore().Scenes.Single();
        Assert.Equal("Anna", restored.Support!.Dialogue[1].Speaker);
        Assert.Equal(2100, restored.Support.Dialogue[1].StartMilliseconds);
        Assert.Equal("O livro de registros está vazio. Espere aqui.", restored.Support.FullTranslation);
    }

    [Theory]
    [InlineData("LEDGER", true)]
    [InlineData("ledger is", true)]
    [InlineData("ledge", false)]
    [InlineData("empty. Wait", true)]
    [InlineData("absent", false)]
    public void Words_MustActuallyOccurAtWordBoundaries(string term, bool expected) =>
        Assert.Equal(expected, SceneSupport.ContainsTerm(Text, term));

    [Fact]
    public void Vocabulary_RejectsMissingTermsDuplicateTermsAndKeys()
    {
        Assert.Throws<GameRuleException>(() => (Valid() with { Vocabulary = [new("absent", "ghost", "fantasma")] }).Validate(Text));
        Assert.Throws<GameRuleException>(() => (Valid() with { Vocabulary = [new("a", "ledger", "livro"), new("b", "Ledger", "registro")] }).Validate(Text));
        Assert.Throws<GameRuleException>(() => (Valid() with { Vocabulary = [new("a", "ledger", "livro"), new("a", "empty", "vazio")] }).Validate(Text));
        Assert.Throws<GameRuleException>(() => (Valid() with { Vocabulary = [new("a", "ledger", "")] }).Validate(Text));
    }

    [Fact]
    public void Dialogue_RejectsOverlapOmissionsAlteredTextAndIntervalsBeyondAudio()
    {
        Assert.Throws<GameRuleException>(() => (Valid() with { Dialogue = [Valid().Dialogue[0], Valid().Dialogue[1] with { StartMilliseconds = 1999 }] }).Validate(Text));
        Assert.Throws<GameRuleException>(() => (Valid() with { Dialogue = [Valid().Dialogue[0]] }).Validate(Text));
        Assert.Throws<GameRuleException>(() => (Valid() with { Dialogue = [new("a", "Anna", "A different text.", 0, 2000)] }).Validate(Text));
        Assert.Throws<GameRuleException>(() => Valid().Validate(Text, 2999));
        Valid().Validate(Text, 3000);
    }
}
