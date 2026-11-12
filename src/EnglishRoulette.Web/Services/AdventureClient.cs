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
    private string? _credential;
    private long _generation;

    public Task<List<PublicStoryView>> CatalogAsync(CancellationToken token, string language = "en") => RequestAsync(async bounded =>
    {
        using var response = await http.GetAsync(language == "en" ? "api/catalog" : "api/catalog?language=" + Uri.EscapeDataString(language), bounded);
        await EnsureAsync(response, bounded);
        var stories = await response.Content.ReadFromJsonAsync<List<PublicStoryView>>(bounded) ?? throw InvalidResponse();
        if (stories.Any(story => story is null || story.Id == Guid.Empty || string.IsNullOrWhiteSpace(story.Title) ||
            string.IsNullOrWhiteSpace(story.Genre) || string.IsNullOrWhiteSpace(story.Difficulty) || story.Language != language))
        { throw InvalidResponse(); }
        return stories;
    }, token);

    public Task<RoundPreviewView?> PreviewAsync(CancellationToken token, string language = "en") => RequestAsync(async bounded =>
    {
        using var response = await http.GetAsync(language == "en" ? "api/catalog/round-preview" : "api/catalog/round-preview?language=" + Uri.EscapeDataString(language), bounded);
        if (response.StatusCode == HttpStatusCode.NoContent) { return null; }
        await EnsureAsync(response, bounded);
        var preview = await response.Content.ReadFromJsonAsync<RoundPreviewView>(bounded) ?? throw InvalidResponse();
        return preview.Language == language ? preview : throw InvalidResponse();
    }, token);

    public Task<AdventureView?> GetAsync(CancellationToken token) => RequestAsync<AdventureView?>(async bounded =>
    {
        if (_credential is null) { return null; }
        using var request = SessionRequest(HttpMethod.Get, "api/demo/session");
        using var response = await http.SendAsync(request, bounded);
        if (response.StatusCode == HttpStatusCode.NotFound) { return null; }
        await EnsureAsync(response, bounded);
        return await ReadAsync(response, bounded);
    }, token);

    public Task<AdventureView> CreateAsync(Guid? versionId, CancellationToken token, string language = "en", int questionSeconds = 20) => RequestAsync(async bounded =>
    {
        var generation = _generation;
        using var request = SessionRequest(HttpMethod.Post, "api/demo/session");
        request.Content = JsonContent.Create(new CreateAdventureRequest(versionId, Language: language, QuestionSeconds: questionSeconds));
        using var response = await http.SendAsync(request, bounded);
        await EnsureAsync(response, bounded);
        var state = await ReadAsync(response, bounded);
        if (state.Language != language) { throw InvalidResponse(); }
        var credentials = response.Headers.TryGetValues(SoloSessionHeaders.Credential, out var values) ? values.ToArray() : [];
        var credential = credentials.Length == 1 ? credentials[0] : null;
        if (credential is not { Length: 64 } || !credential.All(char.IsAsciiHexDigit)) { throw InvalidResponse(); }
        bounded.ThrowIfCancellationRequested();
        if (_generation != generation) { throw new AdventureApiException("Esta aventura foi encerrada. Inicie uma nova história."); }
        _credential = credential;
        return state;
    }, token);

    public Task<AdventureView?> SendAsync(AdventureCommand command, CancellationToken token) => RequestAsync<AdventureView?>(async bounded =>
    {
        if (_credential is null) { return null; }
        using var request = SessionRequest(HttpMethod.Post, "api/demo/commands");
        request.Content = JsonContent.Create(command);
        using var response = await http.SendAsync(request, bounded);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            return await GetAsync(bounded);
        }
        await EnsureAsync(response, bounded);
        return await ReadAsync(response, bounded);
    }, token);

    public Task EndAsync(CancellationToken token)
    {
        var request = SessionRequest(HttpMethod.Delete, "api/demo/session");
        _credential = null;
        _generation++;
        return RequestAsync(async bounded =>
        {
            using (request)
            using (var response = await http.SendAsync(request, bounded))
            { await EnsureAsync(response, bounded); }
            return true;
        }, token);
    }

    private HttpRequestMessage SessionRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (_credential is not null) { request.Headers.Add(SoloSessionHeaders.Credential, _credential); }
        return request;
    }

    private static async Task<T> RequestAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(RequestTimeout);
        try { return await action(timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new AdventureApiException("O servidor demorou para responder. Tente novamente enquanto esta página estiver aberta."); }
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
            "CONTENT_NOT_PUBLISHED" => "Não há histórias publicadas neste idioma. Atualize o catálogo ou escolha outro idioma.",
            "INVALID_LANGUAGE" or "LANGUAGE_MISMATCH" => "O idioma solicitado não corresponde à história. Volte ao menu e escolha inglês ou português.",
            "INVALID_AUDIO" or "MEDIA_UNAVAILABLE" => "A narração desta história está indisponível. Tente novamente em instantes.",
            _ when response.StatusCode == HttpStatusCode.TooManyRequests => "Muitas tentativas em pouco tempo. Aguarde um minuto e tente novamente.",
            _ => "O jogo não conseguiu concluir a solicitação. Tente novamente enquanto esta página estiver aberta."
        };
        throw new AdventureApiException(message);
    }

    private static async Task<AdventureView> ReadAsync(HttpResponseMessage response, CancellationToken token)
    {
        var state = await response.Content.ReadFromJsonAsync<AdventureView>(token);
        if (state is null || string.IsNullOrWhiteSpace(state.Title) || string.IsNullOrWhiteSpace(state.SceneKey) ||
            state.StoryVersionId is null || state.Choices is null || state.Language is not ("en" or "pt-BR") ||
            state.Phase is not ("Preloading" or "Narrating" or "QuestionOpen" or "QuestionReveal" or "DecisionOpen" or "StoryResult"))
        { throw InvalidResponse(); }
        return state;
    }

    private static AdventureApiException InvalidResponse() => new("O servidor retornou uma resposta inválida. Tente novamente.");
    private sealed record AdventureProblem(string? Detail);
}
