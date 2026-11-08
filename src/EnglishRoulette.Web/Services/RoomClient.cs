using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        using var response = await SendHttp(new(HttpMethod.Post, "api/rooms/identity") { Content = JsonContent.Create(new GuestRequest(nickname, avatar)) }, token);
        await Ensure(response, token); return await Read<GuestView>(response, token);
    }
    public Task<RoomView> CreateAsync(int capacity, CancellationToken token) => Send<RoomView>("api/rooms/", new CreateRoomRequest(capacity), token);
    public Task<RoomView> JoinAsync(string code, CancellationToken token) => Send<RoomView>("api/rooms/join", new JoinRoomRequest(code), token);
    public Task<RoomView> ReadyAsync(RoomView room, bool ready, CancellationToken token) => Send<RoomView>($"api/rooms/{room.Id}/ready", new ReadyRoomRequest(room.StateVersion, ready), token);
    public Task<RoomView> SpinAsync(RoomView room, CancellationToken token) => Send<RoomView>($"api/rooms/{room.Id}/spin", new SpinRoomRequest(room.StateVersion), token);
    public Task<RoomView> CommandAsync(RoomView room, MatchCommandRequest command, CancellationToken token) =>
        Send<RoomView>($"api/rooms/{room.Id}/commands", command, token);
    public async Task LeaveAsync(Guid id, CancellationToken token)
    { using var response = await Post($"api/rooms/{id}/leave", new { }, token); await Ensure(response, token); }
    private async Task<T?> Optional<T>(string path, CancellationToken token)
    {
        using var response = await SendHttp(new(HttpMethod.Get, path), token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound) { return default; }
        await Ensure(response, token); return await Read<T>(response, token);
    }
    private async Task<T> Send<T>(string path, object body, CancellationToken token)
    {
        using var response = await Post(path, body, token);
        await Ensure(response, token); return await Read<T>(response, token);
    }
    private async Task<HttpResponseMessage> Post(string path, object body, CancellationToken token)
    {
        var csrf = await Optional<AntiforgeryView>("api/rooms/antiforgery", token) ?? throw new RoomApiException("Sua identificação expirou. Atualize a página para entrar novamente.");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, body.GetType()) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.Token); return await SendHttp(request, token);
    }
    private async Task<HttpResponseMessage> SendHttp(HttpRequestMessage request, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { using (request) { return await http.SendAsync(request, timeout.Token); } }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new RoomApiException("O servidor demorou para responder. Tente novamente; a sala pode ter recebido seu envio."); }
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken token)
    {
        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(token);
            return value is null ? throw new RoomApiException("O servidor retornou uma sala inválida. Tente reconectar.") : value;
        }
        catch (JsonException) { throw new RoomApiException("Não foi possível ler a resposta do servidor. Tente reconectar."); }
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
