using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;

namespace EnglishRoulette.Web.Services;

public sealed class RoomApiException(string message) : Exception(message);
public sealed class RoomClient(HttpClient http)
{
    public async Task<GuestView?> MeAsync(CancellationToken token) => await Optional<GuestView>("api/rooms/me", token);
    public async Task<RoomView?> CurrentAsync(CancellationToken token) => await Optional<RoomView>("api/rooms/current", token);
    public async Task<GuestView> IdentifyAsync(string nickname, string avatar, CancellationToken token)
    {
        using var response = await http.PostAsJsonAsync("api/rooms/identity", new GuestRequest(nickname, avatar), token);
        await Ensure(response, token); return (await response.Content.ReadFromJsonAsync<GuestView>(token))!;
    }
    public Task<RoomView> CreateAsync(int capacity, CancellationToken token) => Send<RoomView>("api/rooms/", new CreateRoomRequest(capacity), token);
    public Task<RoomView> JoinAsync(string code, CancellationToken token) => Send<RoomView>("api/rooms/join", new JoinRoomRequest(code), token);
    public Task<RoomView> ReadyAsync(RoomView room, bool ready, CancellationToken token) => Send<RoomView>($"api/rooms/{room.Id}/ready", new ReadyRoomRequest(room.StateVersion, ready), token);
    public Task<RoomView> StoryAsync(RoomView room, Guid versionId, CancellationToken token) => Send<RoomView>($"api/rooms/{room.Id}/story", new RoomStoryRequest(room.StateVersion, versionId), token);
    public async Task LeaveAsync(Guid id, CancellationToken token)
    { using var response = await Post($"api/rooms/{id}/leave", new { }, token); await Ensure(response, token); }
    private async Task<T?> Optional<T>(string path, CancellationToken token)
    {
        using var response = await http.GetAsync(path, token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound) { return default; }
        await Ensure(response, token); return await response.Content.ReadFromJsonAsync<T>(token);
    }
    private async Task<T> Send<T>(string path, object body, CancellationToken token)
    {
        using var response = await Post(path, body, token);
        await Ensure(response, token); return (await response.Content.ReadFromJsonAsync<T>(token))!;
    }
    private async Task<HttpResponseMessage> Post(string path, object body, CancellationToken token)
    {
        var csrf = await http.GetFromJsonAsync<AntiforgeryView>("api/rooms/antiforgery", token);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, body.GetType()) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token); return await http.SendAsync(request, token);
    }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) { return; }
        var message = response.StatusCode == HttpStatusCode.TooManyRequests ? "Muitas tentativas. Aguarde um minuto." : "Não foi possível atualizar a sala.";
        try { var problem = await response.Content.ReadFromJsonAsync<RoomProblem>(token); message = problem?.Detail ?? problem?.Title ?? message; }
        catch (System.Text.Json.JsonException) { }
        throw new RoomApiException(message);
    }
    private sealed record RoomProblem(string? Title, string? Detail);
}
