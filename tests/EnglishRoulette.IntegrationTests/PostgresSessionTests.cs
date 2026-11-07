using System.Security.Cryptography;
using EnglishRoulette.Application.Solo;
using EnglishRoulette.Domain;
using EnglishRoulette.Domain.Sessions;
using EnglishRoulette.Infrastructure.Persistence;
using EnglishRoulette.UnitTests;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EnglishRoulette.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ENGLISHROULETTE_TEST_CONNECTION")))
        {
            Skip = "Set ENGLISHROULETTE_TEST_CONNECTION to run against an isolated PostgreSQL test database.";
        }
    }
}

public sealed class PostgresSessionTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly string _schema = "test_" + Guid.NewGuid().ToString("N");
    private readonly string _hash = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray()));
    private readonly ManualClock _clock = new();
    private readonly string _connection = Environment.GetEnvironmentVariable("ENGLISHROULETTE_TEST_CONNECTION") ?? "";

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
        await create.ExecuteNonQueryAsync();
        await using var db = Context();
        await db.Database.MigrateAsync();
        var story = StoryFixture.Story();
        var session = new SoloAdventureSession(Guid.NewGuid(), Guid.NewGuid(), story,
            story.Scenes.Select(scene => new SceneTiming(scene.Key, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15))));
        await Store(db).CreateAsync(_hash, session.Capture(), null, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_connection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task NewContext_RestoresOpenQuestionWithoutResettingDeadline()
    {
        var command = new StartSoloNarration(Guid.NewGuid(), 1);
        await Execute(command);
        _clock.Now = Start.AddSeconds(30);
        var open = await Snapshot();
        _clock.Now = Start.AddSeconds(35);

        var restored = await Snapshot();

        Assert.Equal(open?.Question?.InstanceId, restored?.Question?.InstanceId);
        Assert.Equal(Start.AddSeconds(45), restored?.PhaseDeadlineUtc);
        Assert.Equal(open?.StateVersion, restored?.StateVersion);
    }

    [PostgresFact]
    public async Task Jsonb_ReplaySurvivesDisposalAndPreservesOriginalReceipt()
    {
        var command = new StartSoloNarration(Guid.NewGuid(), 1);
        var accepted = await Execute(command);
        _clock.Now = Start.AddMinutes(2);

        var replay = await Execute(command);

        Assert.NotNull(accepted);
        Assert.NotNull(replay);
        Assert.Equal(accepted.Phase, replay.Phase);
        Assert.Equal(accepted.ServerNowUtc, replay.ServerNowUtc);
        Assert.Equal(accepted.StateVersion, replay.StateVersion);
        var expired = await Snapshot();
        Assert.True(expired?.Answer?.TimedOut);
    }

    [PostgresFact]
    public async Task ExpiredAnswer_PersistsTimeoutEvenWhenCommandIsRejected()
    {
        await Execute(new StartSoloNarration(Guid.NewGuid(), 1));
        _clock.Now = Start.AddSeconds(30);
        var open = await Snapshot();
        Assert.NotNull(open?.Question);
        _clock.Now = Start.AddSeconds(45);

        var error = await Assert.ThrowsAsync<GameRuleException>(() => Execute(new SubmitSoloAnswer(
            Guid.NewGuid(), open.StateVersion, open.Question.InstanceId, "a")));

        Assert.Equal("DEADLINE_EXPIRED", error.Code);
        await using var db = Context();
        var stored = await Store(db).FindAsync(_hash, CancellationToken.None);
        Assert.Equal(SoloAdventurePhase.QuestionReveal, stored?.Checkpoint.Phase);
        Assert.True(stored?.Checkpoint.Answer?.TimedOut);
    }

    [PostgresFact]
    public async Task ConcurrentContexts_DuplicateCommandAppliesOnlyOnce()
    {
        var command = new StartSoloNarration(Guid.NewGuid(), 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Execute(command)));

        Assert.All(results, result => Assert.Equal(2, result?.StateVersion));
        await using var db = Context();
        var stored = await Store(db).FindAsync(_hash, CancellationToken.None);
        Assert.Equal(2, stored?.Revision);
        Assert.Single(stored?.Checkpoint.Receipts ?? []);
    }

    [PostgresFact]
    public async Task ConcurrentContexts_DifferentAnswersAcceptOnlyOne()
    {
        await Execute(new StartSoloNarration(Guid.NewGuid(), 1));
        _clock.Now = Start.AddSeconds(30);
        var open = await Snapshot();
        Assert.NotNull(open?.Question);

        var errors = await Task.WhenAll(new[] { "a", "b" }.Select(option => Record.ExceptionAsync(() =>
            Execute(new SubmitSoloAnswer(Guid.NewGuid(), open.StateVersion, open.Question.InstanceId, option)))));

        Assert.Single(errors, error => error is null);
        Assert.Single(errors, error => error is GameRuleException { Code: "ALREADY_ANSWERED" });
        var final = await Snapshot();
        Assert.Equal(open.StateVersion + 1, final?.StateVersion);
        Assert.NotNull(final?.Answer);
    }

    [PostgresFact]
    public async Task StaleRevision_CannotOverwriteSavedState()
    {
        await using var db = Context();
        var store = Store(db);
        var original = await store.FindAsync(_hash, CancellationToken.None);
        Assert.NotNull(original);
        await Execute(new StartSoloNarration(Guid.NewGuid(), 1));

        Assert.False(await store.TrySaveAsync(_hash, original.Revision, original.Checkpoint, CancellationToken.None));
        Assert.Equal(SoloAdventurePhase.Narrating, (await Snapshot())?.Phase);
    }

    [PostgresFact]
    public async Task ReplaceCredential_RevokesOldCookieAndRetainsNewSessionAtomically()
    {
        await using var db = Context();
        var store = Store(db);
        var original = await store.FindAsync(_hash, CancellationToken.None);
        Assert.NotNull(original);
        var newHash = Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray()));

        await store.CreateAsync(newHash, original.Checkpoint, _hash, CancellationToken.None);

        Assert.Null(await store.FindAsync(_hash, CancellationToken.None));
        Assert.NotNull(await store.FindAsync(newHash, CancellationToken.None));
    }

    [PostgresFact]
    public async Task Database_RejectsInvalidCredentialHash()
    {
        await using var db = Context();
        var original = await Store(db).FindAsync(_hash, CancellationToken.None);
        Assert.NotNull(original);

        await Assert.ThrowsAsync<DbUpdateException>(() => Store(db).CreateAsync(
            "plaintext-token", original.Checkpoint, null, CancellationToken.None));
    }

    private AdventureDbContext Context()
    {
        var connection = new NpgsqlConnectionStringBuilder(_connection) { SearchPath = _schema }.ConnectionString;
        return new(new DbContextOptionsBuilder<AdventureDbContext>()
            .UseNpgsql(connection, options => options.MigrationsHistoryTable("__EFMigrationsHistory", _schema)).Options);
    }

    private PostgresSoloSessionStore Store(AdventureDbContext db) => new(db, _clock);
    private async Task<SoloAdventureSnapshot?> Execute(SoloAdventureCommand command)
    {
        await using var db = Context();
        return await new PersistentSoloAdventure(Store(db), _clock).ExecuteAsync(_hash, command, CancellationToken.None);
    }
    private async Task<SoloAdventureSnapshot?> Snapshot()
    {
        await using var db = Context();
        return await new PersistentSoloAdventure(Store(db), _clock).GetAsync(_hash, CancellationToken.None);
    }
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
