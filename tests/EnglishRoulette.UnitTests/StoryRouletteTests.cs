using EnglishRoulette.Application.Admin;
using EnglishRoulette.Application.Presentation;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.UnitTests;

public sealed class StoryRouletteTests
{
    [Fact]
    public void Draw_FiltersLanguageBeforeGenre_AndDoesNotFallbackToAnotherLanguage()
    {
        var english = Story("Mistério");
        var portuguese = Story("Comédia") with { Language = StoryLanguage.Portuguese };
        Assert.Equal(english.Id, StoryRoulette.Draw([english, portuguese]));
        Assert.Equal(portuguese.Id, StoryRoulette.Draw([english, portuguese], language: StoryLanguage.Portuguese));
        Assert.Equal("CONTENT_NOT_PUBLISHED", Assert.Throws<GameRuleException>(() =>
            StoryRoulette.Draw([english], language: StoryLanguage.Portuguese)).Code);
        Assert.Equal("INVALID_LANGUAGE", Assert.Throws<GameRuleException>(() =>
            StoryRoulette.Draw([english, portuguese], language: "es")).Code);
    }

    [Fact]
    public void Draw_UsesOnlySelectedLevel_AndRejectsUnavailableOrInvalidLevels()
    {
        var beginner = Story("Terror") with { Difficulty = "A1" };
        var intermediate = Story("Mistério") with { Difficulty = "B1" };
        var portuguese = Story("Comédia") with { Difficulty = "A1", Language = StoryLanguage.Portuguese };
        Assert.Equal(beginner.Id, StoryRoulette.Draw([beginner, intermediate, portuguese], difficulty: "A1"));
        Assert.Equal(intermediate.Id, StoryRoulette.Draw([beginner, intermediate], difficulty: "B1"));
        Assert.Equal("CONTENT_NOT_PUBLISHED", Assert.Throws<GameRuleException>(() =>
            StoryRoulette.Draw([beginner, intermediate], difficulty: "C1")).Code);
        Assert.Equal("INVALID_DIFFICULTY", Assert.Throws<GameRuleException>(() =>
            StoryRoulette.Draw([beginner], difficulty: "Z9")).Code);
    }

    private static CatalogSummary Story(string genre, string status = "Published") =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, "Story", genre, "A2", status, 1, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Draw_ChoosesGenreBeforeStory_AndIgnoresDraftsAndArchivedVersions()
    {
        var horror = Story("Terror");
        CatalogSummary[] catalog = [Story("Mistério"), Story("Mistério"), horror, Story("Fantasia", "Draft"), Story("Comédia", "Archived")];
        var bounds = new List<int>();
        var result = StoryRoulette.Draw(catalog, count => { bounds.Add(count); return count - 1; });
        Assert.Equal(horror.Id, result);
        Assert.Equal(new[] { 2, 1 }, bounds);
    }

    [Fact]
    public void Draw_NormalizesGenreGroups_AndSelectsAnyStoryWithinWinningGenre()
    {
        var first = Story("Terror"); var second = Story(" terror ");
        var draws = new Queue<int>([0, 1]);
        var bounds = new List<int>();
        Assert.Equal(second.Id, StoryRoulette.Draw([first, second], count =>
        {
            bounds.Add(count);
            return draws.Dequeue();
        }));
        Assert.Empty(draws);
        Assert.Equal(new[] { 1, 2 }, bounds);
    }

    [Fact]
    public void Draw_SmallCatalogReturnsItsOnlyPublishedStory_WithoutRetryLoop()
    {
        var only = Story("Terror");
        Assert.Equal(only.Id, StoryRoulette.Draw([only]));
        Assert.Equal("CONTENT_NOT_PUBLISHED", Assert.Throws<GameRuleException>(() => StoryRoulette.Draw([])).Code);
        Assert.Throws<GameRuleException>(() => StoryRoulette.Draw([Story("Terror", "Draft")]));
    }
}
