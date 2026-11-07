using System.Net;
using System.Net.Http.Json;
using EnglishRoulette.Contracts;
using EnglishRoulette.Contracts.Admin;

namespace EnglishRoulette.Web.Services;

public sealed class AdventureClient(HttpClient http)
{
    public async Task<List<PublicStoryView>> CatalogAsync(CancellationToken token) =>
        await http.GetFromJsonAsync<List<PublicStoryView>>("api/catalog", token) ?? [];

    public async Task<AdventureView?> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync("api/demo/session", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) { return null; }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdventureView>(cancellationToken);
    }

    public async Task<AdventureView?> CreateAsync(Guid? versionId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/demo/session", new CreateAdventureRequest(versionId), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<AdventureView?> SendAsync(AdventureCommand command, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/demo/commands", command, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            return await GetAsync(cancellationToken);
        }
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response, cancellationToken);
    }

    private static async Task<AdventureView> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<AdventureView>(cancellationToken)
        ?? throw new HttpRequestException("A aventura não retornou um estado válido.");
}
