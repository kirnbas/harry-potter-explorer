using System.Net.Http.Json;
using System.Text.Json;
using HarryPotterExplorer.Models.External;

namespace HarryPotterExplorer.Services;

public interface IHarryPotterApiClient
{
    Task<IReadOnlyList<HpApiCharacter>> GetCharactersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<HpApiSpell>> GetSpellsAsync(CancellationToken ct = default);
}

/// <summary>
/// The one and only place in the solution that talks to hp-api.onrender.com.
/// It is registered as a typed <see cref="HttpClient"/> and is never reachable from the
/// browser: every request the client makes goes to our own /api/* endpoints instead.
/// </summary>
public sealed class HarryPotterApiClient(HttpClient http, ILogger<HarryPotterApiClient> logger)
    : IHarryPotterApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<HpApiCharacter>> GetCharactersAsync(CancellationToken ct = default)
        => GetListAsync<HpApiCharacter>("api/characters", ct);

    public Task<IReadOnlyList<HpApiSpell>> GetSpellsAsync(CancellationToken ct = default)
        => GetListAsync<HpApiSpell>("api/spells", ct);

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        logger.LogInformation("Sending an owl to {BaseAddress}{Path}", http.BaseAddress, path);

        using var response = await http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, ct);
        var items = payload ?? [];

        logger.LogInformation("Owl returned with {Count} item(s) from {Path}", items.Count, path);
        return items;
    }
}
