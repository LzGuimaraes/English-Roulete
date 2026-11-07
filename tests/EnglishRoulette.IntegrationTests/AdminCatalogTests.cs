using System.Net;
using System.Net.Http.Json;
using System.Text;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;
using EnglishRoulette.Infrastructure.Admin;
using EnglishRoulette.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EnglishRoulette.IntegrationTests;

public sealed class AdminCatalogTests : IAsyncLifetime
{
    private readonly string _schema = "admin_test_" + Guid.NewGuid().ToString("N");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "roulette-admin-" + Guid.NewGuid().ToString("N"));
    private readonly string _baseConnection = Environment.GetEnvironmentVariable("ENGLISHROULETTE_TEST_CONNECTION") ?? "";
    private readonly string _password = "Er1!" + Guid.NewGuid().ToString("N");
    private readonly TestClock _clock = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string Connection => new NpgsqlConnectionStringBuilder(_baseConnection) { SearchPath = _schema }.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_baseConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
        await create.ExecuteNonQueryAsync();
        await using (var db = Context()) { await db.Database.MigrateAsync(); }
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Adventure", Connection);
            builder.UseSetting("Media:Root", Path.Combine(_directory, "media"));
            builder.UseSetting("DataProtection:KeysPath", Path.Combine(_directory, "keys"));
            builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Critical));
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(_clock));
        });
        _client = NewClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        Assert.True((await roles.CreateAsync(new("ADMIN") { Id = Guid.NewGuid() })).Succeeded);
        var admin = new AdminUser { Id = Guid.NewGuid(), UserName = "admin@test.local", Email = "admin@test.local", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, _password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "ADMIN")).Succeeded);
        var regular = new AdminUser { Id = Guid.NewGuid(), UserName = "reader@test.local", Email = "reader@test.local", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(regular, _password)).Succeeded);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) { await _factory.DisposeAsync(); }
        await using var connection = new NpgsqlConnection(_baseConnection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
        await drop.ExecuteNonQueryAsync();
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); }
    }

    [PostgresFact]
    public async Task Access_RequiresAdminRoleAndCsrf_AndNeverStoresPlaintextPassword()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/stories")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/admin/login", new AdminLoginRequest("admin@test.local", _password))).StatusCode);
        await Login("reader@test.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/admin/stories")).StatusCode);
        using var adminClient = NewClient();
        await Login("admin@test.local", adminClient);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync("/api/admin/stories", new { })).StatusCode);
        await using var db = Context();
        Assert.Empty(await db.CatalogVersions.ToListAsync());
        Assert.All(await db.Users.ToListAsync(), user => { Assert.NotNull(user.PasswordHash); Assert.NotEqual(_password, user.PasswordHash); });
    }

    [PostgresFact]
    public async Task Login_LocksAfterFiveFailures_AndRateLimitsRepeatedRequests()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest("admin@test.local", "wrong"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var locked = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest("admin@test.local", _password));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        await using var db = Context();
        Assert.True((await db.Users.SingleAsync(user => user.Email == "admin@test.local")).LockoutEnd > DateTimeOffset.UtcNow);
        for (var attempt = 0; attempt < 4; attempt++)
        { using var response = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest("missing@test.local", "wrong")); }
        using var limited = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest("missing@test.local", "wrong"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [PostgresFact]
    public async Task PasswordChange_RequiresCurrentPassword_AndLogoutEndsAccess()
    {
        await Login();
        using var bad = await Mutate(HttpMethod.Post, "/api/admin/password", new ChangePasswordRequest("wrong", _password + "new"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using var changed = await Mutate(HttpMethod.Post, "/api/admin/password", new ChangePasswordRequest(_password, _password + "new"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var logout = await Mutate(HttpMethod.Post, "/api/admin/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/me")).StatusCode);
        using var old = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest("admin@test.local", _password));
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var next = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest("admin@test.local", _password + "new"));
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [PostgresFact]
    public async Task DraftSave_PersistsChildren_RejectsStaleRevisionAndMalformedInput()
    {
        await Login();
        var editor = await Draft();
        var stale = editor.Revision;
        editor.Metadata.Title = "Revised title";
        editor = await Save(editor);
        Assert.Equal("Revised title", (await _client.GetFromJsonAsync<AdminStoryEditor>($"/api/admin/stories/{editor.Id}"))?.Metadata.Title);
        editor.Revision = stale;
        using var conflict = await Mutate(HttpMethod.Put, $"/api/admin/stories/{editor.Id}", editor);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var malformed = await Mutate(HttpMethod.Put, $"/api/admin/stories/{editor.Id}", new { scenes = new object?[] { null } });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        await using var db = Context();
        Assert.Single(await db.CatalogScenes.ToListAsync());
        Assert.True(await db.AdminAudit.AnyAsync(row => row.Action == "SaveDraft"));
    }

    [PostgresFact]
    public async Task Publication_BlocksMissingAudio_InvalidGraph_UnconfirmedRights_AndChangedNarration()
    {
        await Login();
        var editor = await Draft();
        Assert.False((await Check(editor)).CanPublish);
        using var missing = await Publish(editor);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        editor = await Upload(editor);
        Assert.True((await Check(editor)).CanPublish);
        using var rights = await Publish(editor, reviewed: true, rights: false);
        Assert.Equal(HttpStatusCode.Conflict, rights.StatusCode);
        editor.Scenes[0].IsEnding = false;
        editor.Scenes[0].Transitions.Add(new() { Key = "next", Label = "Missing", TargetSceneKey = "absent" });
        editor = await Save(editor);
        Assert.False((await Check(editor)).CanPublish);
        editor.Scenes[0].IsEnding = true;
        editor.Scenes[0].Transitions.Clear();
        editor.Scenes[0].Narration += " Another sentence.";
        editor = await Save(editor);
        Assert.Contains((await Check(editor)).Issues, issue => issue.Contains("texto mudou", StringComparison.Ordinal));
        Assert.Empty(await _client.GetFromJsonAsync<List<PublicStoryView>>("/api/catalog") ?? []);
    }

    [PostgresFact]
    public async Task Audio_IsPrivateUntilPublished_RejectsInvalidWav_AndSupportsRanges()
    {
        await Login();
        var editor = await Draft();
        using var invalid = await UploadResponse(editor, Encoding.UTF8.GetBytes("not a WAV"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        editor = await Upload(editor);
        var audio = Assert.IsType<AdminAudioView>(editor.Scenes[0].Audio);
        using var anonymous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(audio.PreviewUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/media/catalog/{audio.Id}")).StatusCode);
        using var published = await Publish(editor);
        Assert.Equal(HttpStatusCode.NoContent, published.StatusCode);
        using var range = new HttpRequestMessage(HttpMethod.Get, $"/media/catalog/{audio.Id}");
        range.Headers.Range = new(0, 43);
        using var response = await anonymous.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(44, (await response.Content.ReadAsByteArrayAsync()).Length);
    }

    [PostgresFact]
    public async Task PublishedVersion_IsImmutable_ReplacementAndArchivePreserveActiveSessions()
    {
        await Login();
        var original = await Upload(await Draft());
        using var published = await Publish(original);
        Assert.Equal(HttpStatusCode.NoContent, published.StatusCode);
        original = (await _client.GetFromJsonAsync<AdminStoryEditor>($"/api/admin/stories/{original.Id}"))!;
        using var edit = await Mutate(HttpMethod.Put, $"/api/admin/stories/{original.Id}", original);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        _client.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        var session = await _client.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(original.Id));
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        var initial = (await session.Content.ReadFromJsonAsync<AdventureView>())!;
        using var duplicate = await Mutate(HttpMethod.Post, $"/api/admin/stories/{original.Id}/duplicate", new { });
        var next = (await duplicate.Content.ReadFromJsonAsync<AdminStoryEditor>())!;
        Assert.NotEqual(original.Scenes[0].Id, next.Scenes[0].Id);
        next.Metadata.Title = "New edition";
        next = await Save(next);
        using var republished = await Publish(next);
        Assert.Equal(HttpStatusCode.NoContent, republished.StatusCode);
        var catalog = await _client.GetFromJsonAsync<List<PublicStoryView>>("/api/catalog");
        Assert.Equal(next.Id, Assert.Single(catalog!).Id);
        using var start = await _client.PostAsJsonAsync("/api/demo/commands", new AdventureCommand(Guid.NewGuid(), initial.StateVersion, "start"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        _clock.Now = _clock.Now.AddSeconds(3);
        using var currentResponse = await _client.GetAsync("/api/demo/session");
        var raw = await currentResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("correctOptionKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("explanation", raw, StringComparison.OrdinalIgnoreCase);
        var current = (await currentResponse.Content.ReadFromJsonAsync<AdventureView>())!;
        Assert.Equal(original.Metadata.Title, current.Title);
        Assert.Equal("QuestionOpen", current.Phase);
        next = (await _client.GetFromJsonAsync<AdminStoryEditor>($"/api/admin/stories/{next.Id}"))!;
        using var archived = await Mutate(HttpMethod.Post, $"/api/admin/stories/{next.Id}/archive", new RevisionRequest(next.Revision));
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        Assert.Empty(await _client.GetFromJsonAsync<List<PublicStoryView>>("/api/catalog") ?? []);
        Assert.Equal(current.Question?.InstanceId, (await _client.GetFromJsonAsync<AdventureView>("/api/demo/session"))?.Question?.InstanceId);
        using var fresh = NewClient();
        fresh.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        Assert.Equal(HttpStatusCode.Conflict, (await fresh.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(original.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync(initial.Audio!.Url)).StatusCode);
    }

    [PostgresFact]
    public async Task Preview_UsesServerEvaluation_AndRejectsStaleVersion()
    {
        await Login();
        var editor = await Draft();
        var preview = await _client.GetFromJsonAsync<PreviewSceneView>($"/api/admin/stories/{editor.Id}/preview?revision={editor.Revision}");
        Assert.Equal("ending", preview?.Key);
        using var wrong = await Mutate(HttpMethod.Post, $"/api/admin/stories/{editor.Id}/preview-answer", new PreviewAnswerRequest(editor.Revision, "ending", "b"));
        Assert.False((await wrong.Content.ReadFromJsonAsync<PreviewAnswerView>())?.IsCorrect);
        using var correct = await Mutate(HttpMethod.Post, $"/api/admin/stories/{editor.Id}/preview-answer", new PreviewAnswerRequest(editor.Revision, "ending", "a"));
        Assert.True((await correct.Content.ReadFromJsonAsync<PreviewAnswerView>())?.IsCorrect);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.GetAsync($"/api/admin/stories/{editor.Id}/preview?revision=0")).StatusCode);
    }

    [PostgresFact]
    public async Task Publication_RejectsCycleWithExit_AndPreviewFollowsSavedChoices()
    {
        await Login();
        var editor = await Upload(await Draft());
        var ending = editor.Scenes[0];
        editor.Scenes.Insert(0, new()
        {
            Key = "start",
            Title = "A choice",
            Narration = ending.Narration,
            Prompt = ending.Prompt,
            Options = ending.Options,
            CorrectOptionKey = ending.CorrectOptionKey,
            Explanation = ending.Explanation,
            Audio = ending.Audio,
            Transitions = [new() { Key = "loop", Label = "Return", TargetSceneKey = "start" },
                new() { Key = "finish", Label = "Finish", TargetSceneKey = "ending" }]
        });
        editor.InitialSceneKey = "start";
        editor = await Save(editor);
        Assert.Contains((await Check(editor)).Issues, issue => issue.Contains("ciclos", StringComparison.Ordinal));
        using var blocked = await Publish(editor);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        editor.Scenes[0].Transitions.RemoveAt(0);
        editor = await Save(editor);
        Assert.True((await Check(editor)).CanPublish);
        var initial = await _client.GetFromJsonAsync<PreviewSceneView>($"/api/admin/stories/{editor.Id}/preview?revision={editor.Revision}");
        Assert.Equal("start", initial?.Key);
        var next = await _client.GetFromJsonAsync<PreviewSceneView>(
            $"/api/admin/stories/{editor.Id}/preview?revision={editor.Revision}&currentSceneKey=start&transitionKey=finish");
        Assert.Equal("ending", next?.Key);
        Assert.True(next?.IsEnding);
    }

    private AdventureDbContext Context() => new(new DbContextOptionsBuilder<AdventureDbContext>()
        .UseNpgsql(Connection, options => options.MigrationsHistoryTable("__EFMigrationsHistory", _schema)).Options);
    private HttpClient NewClient() => _factory.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
    private async Task Login(string email = "admin@test.local", HttpClient? client = null)
    {
        using var response = await Mutate(HttpMethod.Post, "/api/admin/login", new AdminLoginRequest(email, _password), client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private async Task<HttpResponseMessage> Mutate(HttpMethod method, string path, object body, HttpClient? client = null)
    {
        client ??= _client;
        var csrf = await client.GetFromJsonAsync<AntiforgeryView>("/api/admin/antiforgery");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body, body.GetType()) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await client.SendAsync(request);
    }
    private async Task<AdminStoryEditor> Draft()
    {
        using var response = await Mutate(HttpMethod.Post, "/api/admin/stories", new { });
        response.EnsureSuccessStatusCode();
        var editor = (await response.Content.ReadFromJsonAsync<AdminStoryEditor>())!;
        editor.Metadata = new()
        {
            Title = "A short story",
            Synopsis = "One afternoon.",
            Genre = "Adventure",
            Difficulty = "A1",
            Author = "Test author",
            Attribution = "Test content",
            LicenseEvidence = "Original fixture",
            LicenseType = "Original"
        };
        editor.InitialSceneKey = "ending";
        editor.Scenes = [new() { Key = "ending", Title = "The end", Narration = "Mia found a blue notebook.", IsEnding = true,
            Prompt = "What did Mia find?", CorrectOptionKey = "a", Explanation = "She found a notebook.",
            Options = [new() { Key = "a", Text = "A notebook" }, new() { Key = "b", Text = "A ticket" }] }];
        return await Save(editor);
    }
    private async Task<AdminStoryEditor> Save(AdminStoryEditor editor)
    {
        using var response = await Mutate(HttpMethod.Put, $"/api/admin/stories/{editor.Id}", editor);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminStoryEditor>())!;
    }
    private async Task<AdminStoryEditor> Upload(AdminStoryEditor editor)
    {
        using var response = await UploadResponse(editor, Wav());
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminStoryEditor>())!;
    }
    private async Task<HttpResponseMessage> UploadResponse(AdminStoryEditor editor, byte[] data)
    {
        var csrf = await _client.GetFromJsonAsync<AntiforgeryView>("/api/admin/antiforgery");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(data), "file", "narration.wav");
        form.Add(new StringContent("Test narration"), "attribution");
        form.Add(new StringContent("Generated test fixture"), "licenseEvidence");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/admin/stories/{editor.Id}/scenes/{editor.Scenes[0].Id}/audio?revision={editor.Revision}")
        { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        return await _client.SendAsync(request);
    }
    private async Task<PublicationCheckView> Check(AdminStoryEditor editor) =>
        (await _client.GetFromJsonAsync<PublicationCheckView>($"/api/admin/stories/{editor.Id}/validation"))!;
    private Task<HttpResponseMessage> Publish(AdminStoryEditor editor, bool reviewed = true, bool rights = true) =>
        Mutate(HttpMethod.Post, $"/api/admin/stories/{editor.Id}/publish", new PublishRequest(editor.Revision, reviewed, rights));
    private static byte[] Wav()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(16036); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
        writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(16000); writer.Write(new byte[16000]);
        return stream.ToArray();
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
