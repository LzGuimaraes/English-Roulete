using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnglishRoulette.Application.Multiplayer;
using EnglishRoulette.Contracts;
using EnglishRoulette.Domain.Multiplayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnglishRoulette.IntegrationTests;

public sealed partial class AdminCatalogTests
{
    [PostgresFact]
    public async Task Lobby_HostSettingsAndChatWorkBeforeSecondPlayerJoins()
    {
        using var host = await Player("Host"); using var guest = await Player("Guest");
        using var create = await RoomPost(host, "/api/rooms/", new CreateRoomRequest(8, Difficulty: "A1"));
        create.EnsureSuccessStatusCode();
        var room = (await create.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal("A1", room.Difficulty);

        using var chat = await RoomPost(host, $"/api/rooms/{room.Id}/chat", new ChatMessageRequest(Guid.NewGuid(), "Olá!"));
        chat.EnsureSuccessStatusCode();
        room = (await chat.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Single(room.ChatMessages!);
        using var settings = await RoomPost(host, $"/api/rooms/{room.Id}/settings", new RoomSettingsRequest(room.StateVersion, 30, 2, "pt-BR", "B1"));
        settings.EnsureSuccessStatusCode();
        room = (await settings.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal((30, 2, "pt-BR"), (room.QuestionSeconds, room.Capacity, room.Language));
        Assert.Equal("B1", room.Difficulty);

        using var join = await RoomPost(guest, "/api/rooms/join", new JoinRoomRequest(room.Code));
        join.EnsureSuccessStatusCode();
        var guestRoom = (await join.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal("pt-BR", guestRoom.Language);
        Assert.Equal("B1", guestRoom.Difficulty);
        Assert.Equal("Olá!", Assert.Single(guestRoom.ChatMessages!).Text);
        using var forbidden = await RoomPost(guest, $"/api/rooms/{room.Id}/settings", new RoomSettingsRequest(guestRoom.StateVersion, 45, 3, "en"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [PostgresFact]
    public async Task Multiplayer_ConcurrentReadinessAnswers_PrivateUntilClosed_DurableResultAndIdempotency()
    {
        await Login();
        var story = await Upload(await Draft());
        using var publication = await Publish(story);
        publication.EnsureSuccessStatusCode();
        using var host = await Player("Host"); using var guest = await Player("Guest"); using var stranger = await Player("Stranger");
        var room = await StartRoom(host, guest);
        Assert.Equal("Roulette", room.Match!.Scene.Phase);
        Assert.Null(room.Match.Scene.Question);
        Assert.Null(room.Match.Scene.Answer);
        var original = room.Match.Id;
        _clock.Now = _clock.Now.AddSeconds(5);
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        var command = Command(room, "audio-ready");
        using var forbidden = await RoomPost(stranger, $"/api/rooms/{room.Id}/commands", command);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var noCsrf = await host.PostAsJsonAsync($"/api/rooms/{room.Id}/commands", command);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var ready = await Task.WhenAll(RoomPost(host, $"/api/rooms/{room.Id}/commands", command),
            RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready")));
        foreach (var response in ready) { Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); response.Dispose(); }
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("Countdown", room.Match!.Scene.Phase);
        var narrationStart = room.Match.Scene.PhaseDeadlineUtc!.Value;
        _clock.Now = narrationStart.AddSeconds(3); // 1s file + 2s margin.
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("QuestionOpen", room.Match!.Scene.Phase);
        var deadline = room.Match.Scene.PhaseDeadlineUtc;
        var answer = Command(room, "answer", "a");
        using var accepted = await RoomPost(host, $"/api/rooms/{room.Id}/commands", answer);
        accepted.EnsureSuccessStatusCode();
        var text = await accepted.Content.ReadAsStringAsync();
        var pending = (await accepted.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.True(pending.Match!.MyAnswerAccepted);
        Assert.Null(pending.Match.Scene.Answer);
        Assert.Empty(pending.Match.RoundResults);
        Assert.All(pending.Match.Players, player => Assert.Equal(0, player.Score));
        Assert.DoesNotContain("correctOptionKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("She found a notebook", text);
        using var replay = await RoomPost(host, $"/api/rooms/{room.Id}/commands", answer);
        replay.EnsureSuccessStatusCode();
        using var altered = await RoomPost(host, $"/api/rooms/{room.Id}/commands", answer with { Key = "b" });
        Assert.Equal(HttpStatusCode.Conflict, altered.StatusCode);
        using var duplicate = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(pending, "answer", "a"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        _clock.Now = _clock.Now.AddMilliseconds(250);
        using var second = await RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "answer", "a"));
        second.EnsureSuccessStatusCode();
        var closed = (await second.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal("QuestionReveal", closed.Match!.Scene.Phase);
        Assert.All(closed.Match.Players, player => Assert.Equal(200, player.Score));
        Assert.All(closed.Match.RoundResults, result => Assert.True(result.Correct));
        Assert.Equal("She found a notebook.", closed.Match.Scene.Answer!.Explanation);
        // New scope reloads JSON from PostgreSQL; elapsed time is not restarted.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var restored = await scope.ServiceProvider.GetRequiredService<ILobbyStore>().ReadAsync(room.Id, room.ViewerId, default);
            Assert.Equal(original, restored!.Match!.Id);
            Assert.Single(restored.Match.Rounds);
            Assert.Equal(deadline, restored.Match.Rounds[0].ClosedAtUtc.AddSeconds(14.75));
        }
        _clock.Now = closed.Match.Scene.PhaseDeadlineUtc!.Value;
        await using (var scope = _factory.Services.CreateAsyncScope())
        { await scope.ServiceProvider.GetRequiredService<ILobbyStore>().TickMatchesAsync(default); }
        // The hosted worker may win the lock first. Verify the committed result,
        // without another advancing read, rather than which caller performed it.
        await using (var db = Context())
        {
            var json = await db.Lobbies.AsNoTracking().Where(row => row.Id == room.Id).Select(row => row.StateJson).SingleAsync();
            var persisted = JsonSerializer.Deserialize<LobbyState>(json)!;
            Assert.Equal(MultiplayerPhase.StoryResult, persisted.Match!.Phase);
            Assert.Single(persisted.Match.Rounds);
        }
        var final = (await guest.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("StoryResult", final.Match!.Scene.Phase);
        Assert.Equal(2, final.Match.Winners.Count);
        Assert.All(final.Match.Standings!, standing => Assert.Equal(1, standing.Position));
        Assert.All(final.Match.Players, player => { Assert.Equal(200, player.Score); Assert.Equal(1, player.CorrectAnswers); });
        using var late = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(final, "answer", "a"));
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        using var third = await RoomPost(stranger, "/api/rooms/join", new JoinRoomRequest(room.Code));
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
    }

    [PostgresFact]
    public async Task Multiplayer_DeadlineRejectsLateAnswer_AndLeavingCancelsWithoutWinners()
    {
        await Login(); var story = await Upload(await Draft()); using var publication = await Publish(story);
        using var host = await Player("Host"); using var guest = await Player("Guest");
        var room = await StartRoom(host, guest);
        _clock.Now = _clock.Now.AddSeconds(5);
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        using var readyHost = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready"));
        using var readyGuest = await RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready"));
        room = (await readyGuest.Content.ReadFromJsonAsync<RoomView>())!;
        _clock.Now = room.Match!.Scene.PhaseDeadlineUtc!.Value.AddSeconds(3);
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        _clock.Now = room.Match!.Scene.PhaseDeadlineUtc!.Value;
        using var late = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "answer", "a"));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("QuestionReveal", room.Match!.Scene.Phase);
        Assert.All(room.Match.Players, player => Assert.Equal(0, player.Score));
        using var leave = await RoomPost(guest, $"/api/rooms/{room.Id}/leave", new { });
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("Cancelled", room.Match!.Scene.Phase);
        Assert.Empty(room.Match.Winners);
        Assert.NotNull(room.Match.CancellationReason);
    }

    private static MatchCommandRequest Command(RoomView room, string kind, string? key = null) =>
        new(Guid.NewGuid(), room.Match!.Scene.StateVersion, room.Match.SceneInstanceId, kind, key);
    private async Task<RoomView> StartRoom(HttpClient host, HttpClient guest, string language = "en")
    {
        using var create = await RoomPost(host, "/api/rooms/", new CreateRoomRequest(2, language)); create.EnsureSuccessStatusCode();
        var room = (await create.Content.ReadFromJsonAsync<RoomView>())!;
        using var join = await RoomPost(guest, "/api/rooms/join", new JoinRoomRequest(room.Code)); join.EnsureSuccessStatusCode();
        room = (await join.Content.ReadFromJsonAsync<RoomView>())!;
        using var first = await RoomPost(host, $"/api/rooms/{room.Id}/ready", new ReadyRoomRequest(room.StateVersion, true)); first.EnsureSuccessStatusCode();
        room = (await first.Content.ReadFromJsonAsync<RoomView>())!;
        using var second = await RoomPost(guest, $"/api/rooms/{room.Id}/ready", new ReadyRoomRequest(room.StateVersion, true)); second.EnsureSuccessStatusCode();
        room = (await second.Content.ReadFromJsonAsync<RoomView>())!;
        using var spin = await RoomPost(host, $"/api/rooms/{room.Id}/spin", new SpinRoomRequest(room.StateVersion)); spin.EnsureSuccessStatusCode();
        return (await spin.Content.ReadFromJsonAsync<RoomView>())!;
    }
}
