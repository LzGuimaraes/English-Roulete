using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;

namespace EnglishRoulette.Web.Services;

public sealed class AdventureApiException(string message) : HttpRequestException(message);

public sealed class AdventureClient(HttpClient http)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public Task<List<PublicStoryView>> CatalogAsync(CancellationToken token) => RequestAsync(async bounded =>
    {
        using var response = await http.GetAsync("api/catalog", bounded);
        await EnsureAsync(response, bounded);
        var stories = await response.Content.ReadFromJsonAsync<List<PublicStoryView>>(bounded) ?? throw InvalidResponse();
        if (stories.Any(story => story is null || story.Id == Guid.Empty || string.IsNullOrWhiteSpace(story.Title) ||
            string.IsNullOrWhiteSpace(story.Genre) || string.IsNullOrWhiteSpace(story.Difficulty)))
        { throw InvalidResponse(); }
        return stories;
    }, token);

    public Task<RoundPreviewView?> PreviewAsync(CancellationToken token) => RequestAsync(async bounded =>
    {
        using var response = await http.GetAsync("api/catalog/round-preview", bounded);
        if (response.StatusCode == HttpStatusCode.NoContent) { return null; }
        await EnsureAsync(response, bounded);
        return await response.Content.ReadFromJsonAsync<RoundPreviewView>(bounded) ?? throw InvalidResponse();
    }, token);

    public Task<AdventureView?> GetAsync(CancellationToken token) => RequestAsync<AdventureView?>(async bounded =>
    {
        using var response = await http.GetAsync("api/demo/session", bounded);
        if (response.StatusCode == HttpStatusCode.NotFound) { return null; }
        await EnsureAsync(response, bounded);
        return await ReadAsync(response, bounded);
    }, token);

    public Task<AdventureView> CreateAsync(Guid? versionId, CancellationToken token) => RequestAsync(async bounded =>
    {
        using var response = await http.PostAsJsonAsync("api/demo/session", new CreateAdventureRequest(versionId), bounded);
        await EnsureAsync(response, bounded);
        return await ReadAsync(response, bounded);
    }, token);

    public Task<AdventureView?> SendAsync(AdventureCommand command, CancellationToken token) => RequestAsync<AdventureView?>(async bounded =>
    {
        using var response = await http.PostAsJsonAsync("api/demo/commands", command, bounded);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            return await GetAsync(bounded);
        }
        await EnsureAsync(response, bounded);
        return await ReadAsync(response, bounded);
    }, token);

    private static async Task<T> RequestAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);
        try { return await action(timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new AdventureApiException("O servidor demorou para responder. Tente novamente ou recupere sua aventura para conferir se ela foi criada."); }
        catch (JsonException) { throw InvalidResponse(); }
        catch (HttpRequestException error) when (error is not AdventureApiException)
        { throw new AdventureApiException("Não foi possível conectar ao jogo. Confira sua conexão e tente novamente."); }
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) { return; }
        string? code = null;
        try { code = (await response.Content.ReadFromJsonAsync<AdventureProblem>(token))?.Detail; }
        catch (JsonException) { }
        var message = code switch
        {
            "CONTENT_NOT_PUBLISHED" => "Não há histórias publicadas disponíveis para iniciar. Atualize o catálogo e tente novamente mais tarde.",
            "INVALID_AUDIO" or "MEDIA_UNAVAILABLE" => "A narração desta história está indisponível. Tente novamente em instantes.",
            _ when response.StatusCode == HttpStatusCode.TooManyRequests => "Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente.",
            _ => "O jogo não conseguiu concluir a solicitação. Tente novamente ou recupere sua aventura."
        };
        throw new AdventureApiException(message);
    }

    private static async Task<AdventureView> ReadAsync(HttpResponseMessage response, CancellationToken token)
    {
        var state = await response.Content.ReadFromJsonAsync<AdventureView>(token);
        if (state is null || string.IsNullOrWhiteSpace(state.Title) || string.IsNullOrWhiteSpace(state.SceneKey) ||
            state.StoryVersionId is null || state.Choices is null ||
            state.Phase is not ("Preloading" or "Narrating" or "QuestionOpen" or "QuestionReveal" or "DecisionOpen" or "StoryResult"))
        { throw InvalidResponse(); }
        return state;
    }

    private static AdventureApiException InvalidResponse() => new("O servidor retornou uma resposta inválida. Tente novamente ou recupere sua aventura.");
    private sealed record AdventureProblem(string? Detail);
}
