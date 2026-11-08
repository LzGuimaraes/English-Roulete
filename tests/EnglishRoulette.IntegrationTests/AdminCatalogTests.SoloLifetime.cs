using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EnglishRoulette.IntegrationTests;

public sealed partial class AdminCatalogTests
{
    [PostgresFact]
    public async Task SoloLifetime_RequiresPageCredential_ExitRevokesIt_AndNewGameStartsFresh()
    {
        await Login(); var story = await AssistedStory(); using var published = await Publish(story); published.EnsureSuccessStatusCode();
        using var page = NewClient(); page.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        using var created = await page.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(story.Id)); created.EnsureSuccessStatusCode();
        Assert.False(created.Headers.Contains("Set-Cookie"));
        Assert.Equal("no-store", created.Headers.CacheControl?.ToString());
        HoldSoloCredential(page, created);
        var credential = Assert.Single(created.Headers.GetValues(SoloSessionHeaders.Credential));
        var state = (await created.Content.ReadFromJsonAsync<AdventureView>())!;
        using var started = await page.PostAsJsonAsync("/api/demo/commands", new AdventureCommand(Guid.NewGuid(), state.StateVersion, "start")); started.EnsureSuccessStatusCode();
        state = (await started.Content.ReadFromJsonAsync<AdventureView>())!;
        using var hint = await page.PostAsJsonAsync("/api/demo/commands", new AdventureCommand(Guid.NewGuid(), state.StateVersion, "hint", HintKey: "notebook", SceneInstanceId: state.SceneInstanceId)); hint.EnsureSuccessStatusCode();
        Assert.Equal(1, (await hint.Content.ReadFromJsonAsync<AdventureView>())!.HintCost!.UsedCount);

        using var reopened = NewClient();
        reopened.DefaultRequestHeaders.Add("Cookie", "englishroulette.demo=" + credential);
        Assert.Equal(HttpStatusCode.NotFound, (await reopened.GetAsync("/api/demo/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reopened.DeleteAsync("/api/demo/session")).StatusCode);
        using var exited = await page.DeleteAsync("/api/demo/session"); Assert.Equal(HttpStatusCode.NoContent, exited.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await page.GetAsync("/api/demo/session")).StatusCode);
        using var oldCommand = await page.PostAsJsonAsync("/api/demo/commands", new AdventureCommand(Guid.NewGuid(), state.StateVersion, "start"));
        Assert.Equal(HttpStatusCode.NotFound, oldCommand.StatusCode);
        using var retry = await page.DeleteAsync("/api/demo/session"); Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);

        using var next = await page.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(story.Id)); next.EnsureSuccessStatusCode();
        HoldSoloCredential(page, next);
        var fresh = (await next.Content.ReadFromJsonAsync<AdventureView>())!;
        Assert.NotEqual(state.SceneInstanceId, fresh.SceneInstanceId);
        Assert.Equal("Preloading", fresh.Phase);
        Assert.Equal(1, fresh.SceneNumber);
        Assert.Equal(0, fresh.Score!.Total);
        Assert.Equal(0, fresh.HintCost!.UsedCount);
        Assert.All(fresh.WordHints!, word => Assert.Null(word.Translation));
        await using var db = Context(); Assert.Single(await db.SoloSessions.ToListAsync());
    }

    [PostgresFact]
    public async Task SoloLifetime_SeparatePagesStayIndependent_AndLegacyCookieCanOnlyBeCleared()
    {
        await Login(); var story = await AssistedStory(); using var publication = await Publish(story); publication.EnsureSuccessStatusCode();
        using var first = NewClient(); using var second = NewClient();
        foreach (var page in new[] { first, second })
        {
            page.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
            using var created = await page.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(story.Id)); created.EnsureSuccessStatusCode();
            HoldSoloCredential(page, created);
        }
        using var ended = await first.DeleteAsync("/api/demo/session"); ended.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/demo/session")).StatusCode);
        using var legacy = NewClient(); legacy.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        legacy.DefaultRequestHeaders.Add("Cookie", "englishroulette.demo=" + Assert.Single(second.DefaultRequestHeaders.GetValues(SoloSessionHeaders.Credential)));
        Assert.Equal(HttpStatusCode.NotFound, (await legacy.GetAsync("/api/demo/session")).StatusCode);
        using var cleared = await legacy.DeleteAsync("/api/demo/session"); cleared.EnsureSuccessStatusCode();
        Assert.Contains(cleared.Headers.GetValues("Set-Cookie"), value => value.StartsWith("englishroulette.demo=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/demo/session")).StatusCode);
    }
}
