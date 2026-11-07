using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts.Admin;
using Microsoft.AspNetCore.Components.Forms;

namespace EnglishRoulette.Web.Services;

public sealed class AdminApiException(string message, bool unauthorized = false) : Exception(message)
{
    public bool Unauthorized { get; } = unauthorized;
}

public sealed class AdminClient(HttpClient http)
{
    public async Task<AdminUserView?> MeAsync(CancellationToken token)
    {
        using var response = await http.GetAsync("api/admin/me", token);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { return null; }
        await EnsureAsync(response, token);
        return await response.Content.ReadFromJsonAsync<AdminUserView>(token);
    }

    public async Task LoginAsync(string email, string password, CancellationToken token) =>
        await SendAsync(HttpMethod.Post, "api/admin/login", new AdminLoginRequest(email, password), token);
    public async Task LogoutAsync(CancellationToken token) => await SendAsync(HttpMethod.Post, "api/admin/logout", new { }, token);
    public async Task PasswordAsync(string current, string next, CancellationToken token) =>
        await SendAsync(HttpMethod.Post, "api/admin/password", new ChangePasswordRequest(current, next), token);

    public async Task<List<AdminStorySummary>> ListAsync(int page, CancellationToken token) =>
        await GetAsync<List<AdminStorySummary>>($"api/admin/stories?page={page}", token);
    public async Task<AdminStoryEditor> FindAsync(Guid id, CancellationToken token) =>
        await GetAsync<AdminStoryEditor>($"api/admin/stories/{id}", token);
    public async Task<AdminStoryEditor> CreateAsync(CancellationToken token) =>
        await SendReadAsync<AdminStoryEditor>(HttpMethod.Post, "api/admin/stories", new { }, token);
    public async Task<AdminStoryEditor> SaveAsync(AdminStoryEditor editor, CancellationToken token) =>
        await SendReadAsync<AdminStoryEditor>(HttpMethod.Put, $"api/admin/stories/{editor.Id}", editor, token);
    public async Task<AdminStoryEditor> DuplicateAsync(Guid id, CancellationToken token) =>
        await SendReadAsync<AdminStoryEditor>(HttpMethod.Post, $"api/admin/stories/{id}/duplicate", new { }, token);
    public async Task<PublicationCheckView> ValidateAsync(Guid id, CancellationToken token) =>
        await GetAsync<PublicationCheckView>($"api/admin/stories/{id}/validation", token);
    public async Task<PreviewSceneView> PreviewAsync(AdminStoryEditor editor, string? sceneKey, string? choice, CancellationToken token) =>
        await GetAsync<PreviewSceneView>($"api/admin/stories/{editor.Id}/preview?revision={editor.Revision}" +
            (sceneKey is null ? "" : $"&currentSceneKey={Uri.EscapeDataString(sceneKey)}") +
            (choice is null ? "" : $"&transitionKey={Uri.EscapeDataString(choice)}"), token);
    public async Task<PreviewAnswerView> PreviewAnswerAsync(AdminStoryEditor editor, string sceneKey, string option, CancellationToken token) =>
        await SendReadAsync<PreviewAnswerView>(HttpMethod.Post, $"api/admin/stories/{editor.Id}/preview-answer",
            new PreviewAnswerRequest(editor.Revision, sceneKey, option), token);
    public async Task PublishAsync(AdminStoryEditor editor, bool reviewed, bool rights, CancellationToken token) =>
        await SendAsync(HttpMethod.Post, $"api/admin/stories/{editor.Id}/publish", new PublishRequest(editor.Revision, reviewed, rights), token);
    public async Task ArchiveAsync(AdminStoryEditor editor, CancellationToken token) =>
        await SendAsync(HttpMethod.Post, $"api/admin/stories/{editor.Id}/archive", new RevisionRequest(editor.Revision), token);

    public async Task<AdminStoryEditor> UploadAsync(AdminStoryEditor editor, AdminSceneEditor scene,
        IBrowserFile file, string attribution, string evidence, CancellationToken token)
    {
        using var form = new MultipartFormDataContent();
        using var stream = file.OpenReadStream(10 * 1024 * 1024, token);
        form.Add(new StreamContent(stream), "file", "narration.wav");
        form.Add(new StringContent(attribution), "attribution");
        form.Add(new StringContent(evidence), "licenseEvidence");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/admin/stories/{editor.Id}/scenes/{scene.Id}/audio?revision={editor.Revision}") { Content = form };
        await AddCsrfAsync(request, token);
        using var response = await http.SendAsync(request, token);
        await EnsureAsync(response, token);
        return await ReadAsync<AdminStoryEditor>(response, token);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken token)
    {
        using var response = await http.GetAsync(path, token);
        await EnsureAsync(response, token);
        return await ReadAsync<T>(response, token);
    }
    private async Task<T> SendReadAsync<T>(HttpMethod method, string path, object data, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(data, data.GetType()) };
        await AddCsrfAsync(request, token);
        using var response = await http.SendAsync(request, token);
        await EnsureAsync(response, token);
        return await ReadAsync<T>(response, token);
    }
    private async Task SendAsync(HttpMethod method, string path, object data, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(data, data.GetType()) };
        await AddCsrfAsync(request, token);
        using var response = await http.SendAsync(request, token);
        await EnsureAsync(response, token);
    }
    private async Task AddCsrfAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var response = await http.GetAsync("api/admin/antiforgery", token);
        await EnsureAsync(response, token);
        var csrf = await ReadAsync<AntiforgeryView>(response, token);
        request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken token) =>
        await response.Content.ReadFromJsonAsync<T>(token) ?? throw new AdminApiException("O servidor não retornou os dados esperados.");
    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) { return; }
        var message = response.StatusCode == HttpStatusCode.TooManyRequests ? "Muitas tentativas. Aguarde um minuto e tente novamente." : "Não foi possível concluir a operação.";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemView>(token);
            message = problem?.Detail ?? problem?.Title ?? message;
        }
        catch (System.Text.Json.JsonException) { }
        throw new AdminApiException(message, response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }
    private sealed record ProblemView(string? Title, string? Detail);
}
