using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts;
using EnglishRoulette.Web.Services;

namespace EnglishRoulette.IntegrationTests;

public sealed class AdventureClientTests
{
    [Fact]
    public async Task Create_UsesTheRealServerSnapshot_AndDoesNotSendAChosenStory()
    {
        var snapshot = Snapshot();
        using var http = Http(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/demo/session", request.RequestUri?.AbsolutePath);
            var body = await request.Content!.ReadFromJsonAsync<CreateAdventureRequest>(token);
            Assert.Null(body?.StoryVersionId);
            return Created(snapshot);
        });
        var result = await new AdventureClient(http).CreateAsync(null, CancellationToken.None);
        Assert.Equal(snapshot.StoryVersionId, result.StoryVersionId);
        Assert.Equal("Preloading", result.Phase);
    }

    [Theory]
    [InlineData("{\"title\":")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task Create_InvalidResponseBecomesARecoverableError(string body)
    {
        using var http = Http((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") }));
        var error = await Assert.ThrowsAsync<AdventureApiException>(() => new AdventureClient(http).CreateAsync(null, CancellationToken.None));
        Assert.Contains("resposta inválida", error.Message);
    }

    [Fact]
    public async Task Create_ContentUnavailableShowsAUsefulError_AndRetryCanSucceed()
    {
        var calls = 0;
        using var http = Http((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { detail = "CONTENT_NOT_PUBLISHED" }) }
            : Created(Snapshot())));
        var client = new AdventureClient(http);
        var error = await Assert.ThrowsAsync<AdventureApiException>(() => client.CreateAsync(null, CancellationToken.None));
        Assert.Contains("Não há histórias publicadas", error.Message);
        Assert.Equal("Preloading", (await client.CreateAsync(null, CancellationToken.None)).Phase);
    }

    [Fact]
    public async Task Create_TimeoutDoesNotLeaveAnUnobservedCancellation_AndCallerCancellationIsPreserved()
    {
        using var http = Http(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(HttpStatusCode.OK); });
        http.Timeout = TimeSpan.FromMilliseconds(50);
        var client = new AdventureClient(http);
        var error = await Assert.ThrowsAsync<AdventureApiException>(() => client.CreateAsync(null, CancellationToken.None));
        Assert.Contains("demorou", error.Message);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CreateAsync(null, canceled.Token));
    }

    [Fact]
    public async Task CatalogFailureCannotBeMistakenForAnEmptyCatalog_AndMissingSessionIsNormal()
    {
        using var http = Http((request, _) => Task.FromResult(new HttpResponseMessage(
            request.RequestUri?.AbsolutePath == "/api/catalog" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound)
        { Content = new StringContent("upstream unavailable") }));
        var client = new AdventureClient(http);
        await Assert.ThrowsAsync<AdventureApiException>(() => client.CatalogAsync(CancellationToken.None));
        Assert.Null(await client.GetAsync(CancellationToken.None));
    }

    private static AdventureView Snapshot() => new("Story", "start", "Scene", 1, "A real server scene.", 1, "Preloading",
        DateTimeOffset.UnixEpoch, null, null, null, [], StoryVersionId: Guid.NewGuid());

    [Fact]
    public async Task PageCredential_IsMemoryOnly_AndExitClearsItEvenWhenCleanupFails()
    {
        var gets = 0;
        using var http = Http((request, _) =>
        {
            if (request.Method == HttpMethod.Post) { return Task.FromResult(Created(Snapshot())); }
            Assert.Equal(new string('A', 64), Assert.Single(request.Headers.GetValues(SoloSessionHeaders.Credential)));
            if (request.Method == HttpMethod.Delete) { return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }
            gets++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Snapshot()) });
        });
        var page = new AdventureClient(http);
        await page.CreateAsync(null, CancellationToken.None);
        Assert.NotNull(await page.GetAsync(CancellationToken.None));
        Assert.Null(await new AdventureClient(http).GetAsync(CancellationToken.None));
        Assert.Equal(1, gets);
        await Assert.ThrowsAsync<AdventureApiException>(() => page.EndAsync(CancellationToken.None));
        Assert.Null(await page.GetAsync(CancellationToken.None));
        Assert.Equal(1, gets);
    }

    [Fact]
    public async Task LateCreateResponse_CannotRestoreAnExitedPage()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = Http((request, _) => request.Method == HttpMethod.Post
            ? pending.Task : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));
        var page = new AdventureClient(http);
        var create = page.CreateAsync(null, CancellationToken.None);
        await page.EndAsync(CancellationToken.None);
        pending.SetResult(Created(Snapshot()));
        await Assert.ThrowsAsync<AdventureApiException>(() => create);
        Assert.Null(await page.GetAsync(CancellationToken.None));
    }
    private static HttpResponseMessage Created(AdventureView state)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(state) };
        response.Headers.Add(SoloSessionHeaders.Credential, new string('A', 64));
        return response;
    }
    private static HttpClient Http(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
        new(new Handler(respond)) { BaseAddress = new("https://test.local") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request, token);
    }
}
