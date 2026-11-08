using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;

namespace EnglishRoulette.IntegrationTests;

public sealed partial class AdminCatalogTests
{
    private async Task<AdminStoryEditor> AssistedStory()
    {
        var story = await Upload(await Draft());
        story.Scenes[0].Vocabulary = [new() { Key = "notebook", Term = "notebook", Translation = "caderno de anotações" }];
        story.Scenes[0].Dialogue = [new() { Key = "narrator", Speaker = "Narrador", Text = story.Scenes[0].Narration, StartMilliseconds = 0, EndMilliseconds = 1000 }];
        return await Save(story);
    }

    [PostgresFact]
    public async Task SceneSupport_ValidatesPersistsAndSurvivesDuplication()
    {
        await Login();
        var story = await AssistedStory();
        Assert.Equal("caderno de anotações", Assert.Single(story.Scenes[0].Vocabulary).Translation);
        using var publish = await Publish(story); publish.EnsureSuccessStatusCode();
        using var duplicate = await Mutate(HttpMethod.Post, $"/api/admin/stories/{story.Id}/duplicate", new { });
        duplicate.EnsureSuccessStatusCode();
        var copy = (await duplicate.Content.ReadFromJsonAsync<AdminStoryEditor>())!;
        Assert.Equal(1000, Assert.Single(copy.Scenes[0].Dialogue).EndMilliseconds);
        Assert.Equal(story.Scenes[0].Vocabulary[0].Translation, copy.Scenes[0].Vocabulary[0].Translation);
        copy.Scenes[0].Dialogue[0].EndMilliseconds = 1001;
        using var tooLong = await Mutate(HttpMethod.Put, $"/api/admin/stories/{copy.Id}", copy);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        copy.Scenes[0].Dialogue[0].EndMilliseconds = 1000;
        copy.Scenes[0].Vocabulary[0].Term = "absent";
        using var absent = await Mutate(HttpMethod.Put, $"/api/admin/stories/{copy.Id}", copy);
        Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
    }

    [PostgresFact]
    public async Task SoloHints_WithholdTranslation_PersistConsumptionAndNetScore_AcrossRetries()
    {
        await Login(); var story = await AssistedStory(); using var publish = await Publish(story); publish.EnsureSuccessStatusCode();
        using var player = NewClient();
        player.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        using var created = await player.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(story.Id)); created.EnsureSuccessStatusCode();
        var state = (await created.Content.ReadFromJsonAsync<AdventureView>())!;
        Assert.DoesNotContain("caderno de anotações", await created.Content.ReadAsStringAsync());
        Assert.Null(Assert.Single(state.WordHints!).Translation);
        Assert.Equal(0, state.Score!.Total);
        Assert.Single(state.Dialogue!);
        async Task<AdventureView> Send(AdventureCommand command)
        {
            using var response = await player.PostAsJsonAsync("/api/demo/commands", command);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<AdventureView>())!;
        }
        state = await Send(new(Guid.NewGuid(), state.StateVersion, "start"));
        var hint = new AdventureCommand(Guid.NewGuid(), state.StateVersion, "hint", HintKey: "notebook", SceneInstanceId: state.SceneInstanceId);
        var translated = await Send(hint);
        Assert.Equal("caderno de anotações", Assert.Single(translated.WordHints!).Translation);
        Assert.Equal(1, translated.HintCost!.UsedCount);
        Assert.Equal(translated.StateVersion, (await Send(hint)).StateVersion);
        state = (await player.GetFromJsonAsync<AdventureView>("/api/demo/session"))!;
        Assert.Equal(1, state.HintCost!.UsedCount);
        _clock.Now = state.PhaseDeadlineUtc!.Value;
        state = (await player.GetFromJsonAsync<AdventureView>("/api/demo/session"))!;
        var answer = new AdventureCommand(Guid.NewGuid(), state.StateVersion, "answer", state.Question!.InstanceId, "a");
        state = await Send(answer);
        Assert.Equal(150, state.Score!.RoundBase);
        Assert.Equal(15, state.Score.RoundDeduction);
        Assert.Equal(135, state.Score.Total);
        Assert.Equal(135, (await Send(answer)).Score!.Total);
        state = await Send(new(Guid.NewGuid(), state.StateVersion, "continue"));
        Assert.Equal("StoryResult", state.Phase);
        Assert.Equal(135, (await player.GetFromJsonAsync<AdventureView>("/api/demo/session"))!.Score!.Total);
    }

    [PostgresFact]
    public async Task MultiplayerHints_ArePrivateAndConcurrent_RematchPreservesTotalsAndResetsReadiness()
    {
        await Login(); var story = await AssistedStory(); using var publish = await Publish(story); publish.EnsureSuccessStatusCode();
        using var host = await Player("Host"); using var guest = await Player("Guest");
        var room = await StartRoom(host, guest);
        using var early = await RoomPost(host, $"/api/rooms/{room.Id}/next", new NextRoomRequest(room.StateVersion, room.Match!.Id));
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        _clock.Now = room.Match!.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        using var readyHost = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready"));
        using var readyGuest = await RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready"));
        room = (await readyGuest.Content.ReadFromJsonAsync<RoomView>())!;
        _clock.Now = room.Match!.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        var hints = await Task.WhenAll(RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "hint", "notebook")),
            RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "hint", "notebook")));
        foreach (var response in hints) { response.EnsureSuccessStatusCode(); response.Dispose(); }
        var mine = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        var other = (await guest.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal(1, mine.Match!.Scene.HintCost!.UsedCount);
        Assert.NotNull(mine.Match.Scene.WordHints![0].Translation);
        Assert.Null(other.Match!.Scene.WordHints![0].Translation);
        Assert.Equal(0, other.Match.Scene.HintCost!.UsedCount);
        _clock.Now = mine.Match.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        var answers = await Task.WhenAll(RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "answer", "a")),
            RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "answer", "a")));
        foreach (var response in answers) { response.EnsureSuccessStatusCode(); response.Dispose(); }
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal(180, room.Match!.Players.Single(player => player.Id == mine.ViewerId).Score);
        Assert.Equal(200, room.Match.Players.Single(player => player.Id == other.ViewerId).Score);
        _clock.Now = room.Match.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("StoryResult", room.Match!.Scene.Phase);
        Assert.Equal(new int?[] { 1, 2 }, room.Match.Standings!.Select(player => player.Position));
        var previous = room.Match.Id;
        var next = new NextRoomRequest(room.StateVersion, previous);
        using var denied = await RoomPost(guest, $"/api/rooms/{room.Id}/next", next);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var prepared = await RoomPost(host, $"/api/rooms/{room.Id}/next", next); prepared.EnsureSuccessStatusCode();
        room = (await prepared.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Null(room.Match); Assert.Null(room.StoryVersionId); Assert.Null(room.RouletteStartedAtUtc);
        Assert.All(room.Members, member => Assert.False(member.Ready));
        Assert.Equal(new[] { 180, 200 }, room.Members.Select(member => member.Score));
        Assert.Equal(2, room.GameNumber);
        using var retry = await RoomPost(host, $"/api/rooms/{room.Id}/next", next); retry.EnsureSuccessStatusCode();
        using var unready = await RoomPost(host, $"/api/rooms/{room.Id}/spin", new SpinRoomRequest(room.StateVersion));
        Assert.Equal(HttpStatusCode.Conflict, unready.StatusCode);
        using var first = await RoomPost(host, $"/api/rooms/{room.Id}/ready", new ReadyRoomRequest(room.StateVersion, true));
        room = (await first.Content.ReadFromJsonAsync<RoomView>())!;
        using var second = await RoomPost(guest, $"/api/rooms/{room.Id}/ready", new ReadyRoomRequest(room.StateVersion, true));
        room = (await second.Content.ReadFromJsonAsync<RoomView>())!;
        using var spin = await RoomPost(host, $"/api/rooms/{room.Id}/spin", new SpinRoomRequest(room.StateVersion)); spin.EnsureSuccessStatusCode();
        room = (await spin.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.NotEqual(previous, room.Match!.Id);
        Assert.Equal(new[] { 180, 200 }, room.Match.Players.Select(player => player.Score));
        Assert.Empty(room.Match.RoundResults);
        using var stale = await RoomPost(host, $"/api/rooms/{room.Id}/next", next);
        Assert.False(stale.IsSuccessStatusCode);
        Assert.Equal(room.Match.Id, (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!.Match!.Id);
    }
}
