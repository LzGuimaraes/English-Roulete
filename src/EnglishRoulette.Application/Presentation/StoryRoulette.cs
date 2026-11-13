using System.Security.Cryptography;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Stories;

namespace EnglishRoulette.Application.Presentation;

public static class StoryRoulette
{
    // Draw a genre first so a large genre does not crowd out smaller ones.
    public static Guid Draw(IReadOnlyList<CatalogSummary> catalog, Func<int, int>? next = null, string language = StoryLanguage.English, string? difficulty = null)
    {
        language = StoryLanguage.Require(language);
        difficulty = StoryDifficulty.Require(difficulty);
        var genres = catalog.Where(story => story.Status == "Published" && story.Language == language &&
                (difficulty is null || story.Difficulty == difficulty))
            .GroupBy(story => story.Genre.Trim(), StringComparer.OrdinalIgnoreCase).ToArray();
        if (genres.Length == 0)
        { throw new GameRuleException("CONTENT_NOT_PUBLISHED", "Ainda não há histórias publicadas para este idioma e nível."); }
        next ??= RandomNumberGenerator.GetInt32;
        var stories = genres[next(genres.Length)].ToArray();
        return stories[next(stories.Length)].Id;
    }
}
