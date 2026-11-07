using System.Security.Cryptography;
using EnglishRoulette.Application.Admin;
using EnglishRoulette.Domain;

namespace EnglishRoulette.Application.Presentation;

public static class StoryRoulette
{
    // Draw a genre first so a large genre does not crowd out smaller ones.
    public static Guid Draw(IReadOnlyList<CatalogSummary> catalog, Func<int, int>? next = null)
    {
        var genres = catalog.Where(story => story.Status == "Published")
            .GroupBy(story => story.Genre.Trim(), StringComparer.OrdinalIgnoreCase).ToArray();
        if (genres.Length == 0)
        { throw new GameRuleException("CONTENT_NOT_PUBLISHED", "Ainda não há histórias publicadas para a roleta."); }
        next ??= RandomNumberGenerator.GetInt32;
        var stories = genres[next(genres.Length)].ToArray();
        return stories[next(stories.Length)].Id;
    }
}
