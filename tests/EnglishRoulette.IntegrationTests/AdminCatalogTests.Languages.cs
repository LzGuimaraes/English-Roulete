using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;

namespace EnglishRoulette.IntegrationTests;

public sealed partial class AdminCatalogTests
{
    private async Task<AdminStoryEditor> PortugueseStory()
    {
        var story = await Draft();
        story.Metadata.Language = "pt-BR";
        story.Metadata.Title = "O caderno azul";
        story.Metadata.Synopsis = "Uma descoberta na biblioteca.";
        var scene = story.Scenes[0];
        scene.Title = "Uma descoberta";
        scene.Narration = "Lia encontrou um caderno azul.";
        scene.Prompt = "O que Lia encontrou?";
        scene.Options = [new() { Key = "a", Text = "Um caderno" }, new() { Key = "b", Text = "Um ingresso" }];
        scene.Explanation = "Lia encontrou um caderno azul.";
        scene.Vocabulary = [new() { Key = "caderno", Term = "caderno", Translation = "Conjunto de folhas para escrever." }];
        scene.Dialogue = [new() { Key = "narrador", Speaker = "Narrador", Text = scene.Narration, StartMilliseconds = 0, EndMilliseconds = 1000 }];
        return await Upload(await Save(story));
    }

    [PostgresFact]
    public async Task Language_CatalogPreviewAndLegacyDefaultAreSeparated_WithoutFallbackOrAnswerLeaks()
    {
        await Login();
        var english = await Upload(await Draft());
        using var englishPublication = await Publish(english); englishPublication.EnsureSuccessStatusCode();
        Assert.Empty((await _client.GetFromJsonAsync<PublicStoryView[]>("/api/catalog?language=pt-BR"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.GetAsync("/api/catalog/round-preview?language=pt-BR")).StatusCode);
        using var player = NewClient(); player.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        using var unavailable = await player.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(Language: "pt-BR"));
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        var portuguese = await PortugueseStory();
        using var publication = await Publish(portuguese); publication.EnsureSuccessStatusCode();
        var legacy = Assert.Single((await _client.GetFromJsonAsync<PublicStoryView[]>("/api/catalog"))!);
        Assert.Equal(english.Id, legacy.Id); Assert.Equal("en", legacy.Language);
        var translated = Assert.Single((await _client.GetFromJsonAsync<PublicStoryView[]>("/api/catalog?language=pt-BR"))!);
        Assert.Equal(portuguese.Id, translated.Id); Assert.Equal("pt-BR", translated.Language);
        using var preview = await _client.GetAsync("/api/catalog/round-preview?language=pt-BR");
        var sample = (await preview.Content.ReadFromJsonAsync<RoundPreviewView>())!;
        Assert.Equal("O caderno azul", sample.StoryTitle); Assert.Equal("pt-BR", sample.Language);
        Assert.Equal("O que Lia encontrou?", sample.QuestionPrompt);
        Assert.DoesNotContain("correctOptionKey", await preview.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/catalog?language=es")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/catalog/round-preview?language=es")).StatusCode);
        portuguese = await Draft(); portuguese.Metadata.Language = "es";
        using var invalid = await Mutate(HttpMethod.Put, $"/api/admin/stories/{portuguese.Id}", portuguese);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [PostgresFact]
    public async Task Language_SoloUsesPortugueseAudioAndPaidDefinitions_AndCannotCrossCatalogs()
    {
        await Login(); var portuguese = await PortugueseStory();
        using var publication = await Publish(portuguese); publication.EnsureSuccessStatusCode();
        using var player = NewClient(); player.DefaultRequestHeaders.Add("X-English-Roulette", "demo");
        using var mismatch = await player.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(portuguese.Id));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Contains("LANGUAGE_MISMATCH", await mismatch.Content.ReadAsStringAsync());
        using var created = await player.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(Language: "pt-BR"));
        created.EnsureSuccessStatusCode(); HoldSoloCredential(player, created);
        var state = (await created.Content.ReadFromJsonAsync<AdventureView>())!;
        Assert.Equal("pt-BR", state.Language); Assert.Equal(portuguese.Id, state.StoryVersionId);
        Assert.Equal("Lia encontrou um caderno azul.", state.Narration);
        Assert.Equal($"/media/catalog/{portuguese.Scenes[0].Audio!.Id}", state.Audio!.Url);
        Assert.Null(Assert.Single(state.WordHints!).Translation);
        async Task<AdventureView> Send(string kind, string? hint = null, string? answer = null)
        {
            using var response = await player.PostAsJsonAsync("/api/demo/commands", new AdventureCommand(Guid.NewGuid(), state.StateVersion,
                kind, state.Question?.InstanceId, answer, HintKey: hint, SceneInstanceId: state.SceneInstanceId));
            response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<AdventureView>())!;
        }
        state = await Send("start"); state = await Send("hint", hint: "caderno");
        Assert.Equal("Conjunto de folhas para escrever.", Assert.Single(state.WordHints!).Translation);
        _clock.Now = state.PhaseDeadlineUtc!.Value;
        state = (await player.GetFromJsonAsync<AdventureView>("/api/demo/session"))!;
        state = await Send("answer", answer: "a");
        Assert.True(state.Answer!.IsCorrect); Assert.Equal(135, state.Score!.Total);
        var oldScene = state.SceneInstanceId;
        using var again = await player.PostAsJsonAsync("/api/demo/session", new CreateAdventureRequest(Language: "pt-BR"));
        again.EnsureSuccessStatusCode();
        state = (await again.Content.ReadFromJsonAsync<AdventureView>())!;
        Assert.Equal("pt-BR", state.Language); Assert.NotEqual(oldScene, state.SceneInstanceId);
        Assert.Equal(0, state.Score!.Total); Assert.Equal(0, state.HintCost!.UsedCount);
    }

    [PostgresFact]
    public async Task Language_MultiplayerUsesRoomLanguage_AcrossGuestsScoringAndNextMatch()
    {
        await Login();
        var english = await Upload(await Draft()); using var englishPublication = await Publish(english); englishPublication.EnsureSuccessStatusCode();
        var portuguese = await PortugueseStory(); using var publication = await Publish(portuguese); publication.EnsureSuccessStatusCode();
        using var host = await Player("Host"); using var guest = await Player("Guest");
        var room = await StartRoom(host, guest, "pt-BR");
        Assert.Equal("pt-BR", room.Language); Assert.Equal(portuguese.Id, room.StoryVersionId);
        Assert.Equal("pt-BR", room.Match!.Scene.Language);
        Assert.Equal("pt-BR", (await guest.GetFromJsonAsync<RoomView>("/api/rooms/current"))!.Language);
        _clock.Now = room.Match.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        using var readyHost = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready")); readyHost.EnsureSuccessStatusCode();
        using var readyGuest = await RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "audio-ready")); readyGuest.EnsureSuccessStatusCode();
        room = (await readyGuest.Content.ReadFromJsonAsync<RoomView>())!;
        _clock.Now = room.Match!.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        using var hint = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "hint", "caderno")); hint.EnsureSuccessStatusCode();
        room = (await hint.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal(1, room.Match!.Scene.HintCost!.UsedCount);
        Assert.Null((await guest.GetFromJsonAsync<RoomView>("/api/rooms/current"))!.Match!.Scene.WordHints![0].Translation);
        _clock.Now = room.Match.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        using var answer = await RoomPost(host, $"/api/rooms/{room.Id}/commands", Command(room, "answer", "a")); answer.EnsureSuccessStatusCode();
        room = (await answer.Content.ReadFromJsonAsync<RoomView>())!;
        using var otherAnswer = await RoomPost(guest, $"/api/rooms/{room.Id}/commands", Command(room, "answer", "b")); otherAnswer.EnsureSuccessStatusCode();
        room = (await otherAnswer.Content.ReadFromJsonAsync<RoomView>())!;
        _clock.Now = room.Match!.Scene.PhaseDeadlineUtc!.Value;
        room = (await host.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal("StoryResult", room.Match!.Scene.Phase);
        Assert.Equal(180, room.Match.Players.Single(player => player.Id == room.HostId).Score);
        using var next = await RoomPost(host, $"/api/rooms/{room.Id}/next", new NextRoomRequest(room.StateVersion, room.Match.Id)); next.EnsureSuccessStatusCode();
        room = (await next.Content.ReadFromJsonAsync<RoomView>())!;
        Assert.Equal("pt-BR", room.Language); Assert.Null(room.Match);
        Assert.All(room.Members, member => Assert.False(member.Ready));
        Assert.Equal(180, room.Members.Single(member => member.PlayerId == room.HostId).Score);
        using var leave = await RoomPost(host, $"/api/rooms/{room.Id}/leave", new { }); leave.EnsureSuccessStatusCode();
        room = (await guest.GetFromJsonAsync<RoomView>("/api/rooms/current"))!;
        Assert.Equal(room.ViewerId, room.HostId); Assert.Equal("pt-BR", room.Language);
        using var invalidRoom = await RoomPost(host, "/api/rooms/", new CreateRoomRequest(2, "es"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidRoom.StatusCode);
    }
}
