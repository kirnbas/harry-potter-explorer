using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers.Api;

/// <summary>
/// The browser's only data source. Nothing on the client ever talks to hp-api.onrender.com:
/// these endpoints read our own mirror, which the server fills in the background.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class CatalogApiController(
    ICatalogService catalog,
    ICatalogSyncCoordinator sync) : ControllerBase
{
    private const int MaxHydrationIds = 300;

    [HttpGet("characters")]
    public async Task<ActionResult<PagedResult<CharacterSummary>>> GetCharacters(
        string? search, string? house, string? species, string? role, string? status,
        string sort = "name", int page = 1, int pageSize = 24, bool withImage = false,
        CancellationToken ct = default)
    {
        var result = await catalog.SearchCharactersAsync(new CharacterQuery
        {
            Search = search,
            House = house,
            Species = species,
            Role = role,
            Status = status,
            Sort = sort,
            Page = page,
            PageSize = pageSize,
            WithImage = withImage
        }, ct);

        return Ok(result);
    }

    [HttpGet("characters/{id}")]
    public async Task<ActionResult<CharacterDetail>> GetCharacter(string id, CancellationToken ct)
    {
        var character = await catalog.GetCharacterAsync(id, ct);
        return character is null ? NotFound(new { message = "No such character." }) : Ok(character);
    }

    /// <summary>
    /// Hydrates a locally stored collection. The client posts the ids it kept in
    /// localStorage and gets back full cards, in the order it asked for them.
    /// </summary>
    [HttpPost("characters/by-ids")]
    public async Task<ActionResult<IReadOnlyList<CharacterSummary>>> GetCharactersByIds(
        [FromBody] string[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
        {
            return Ok(Array.Empty<CharacterSummary>());
        }

        var trimmed = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Take(MaxHydrationIds)
            .ToList();

        return Ok(await catalog.GetCharactersByIdsAsync(trimmed, ct));
    }

    [HttpGet("spells")]
    public async Task<ActionResult<PagedResult<SpellSummary>>> GetSpells(
        string? search, string? category, int page = 1, int pageSize = 30,
        CancellationToken ct = default)
        => Ok(await catalog.SearchSpellsAsync(search, category, page, pageSize, ct));

    [HttpGet("artifacts")]
    public async Task<ActionResult<IReadOnlyList<ArtifactSummary>>> GetArtifacts(
        string? search, string? category, CancellationToken ct = default)
        => Ok(await catalog.GetArtifactsAsync(search, category, ct));

    [HttpGet("houses")]
    public async Task<ActionResult<IReadOnlyList<HouseWithCount>>> GetHouses(CancellationToken ct)
        => Ok(await catalog.GetHousesAsync(ct));

    [HttpGet("facets")]
    public async Task<ActionResult<CatalogFacets>> GetFacets(CancellationToken ct)
        => Ok(await catalog.GetFacetsAsync(ct));

    [HttpGet("stats")]
    public async Task<ActionResult<LiveStats>> GetStats(CancellationToken ct)
        => Ok(await catalog.GetLiveStatsAsync(ct));

    /// <summary>
    /// Asks for an out-of-band refresh of the mirror. Concurrent calls are collapsed into
    /// the run that is already in flight, so this is safe to hammer.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<SyncStatus>> Sync(CancellationToken ct)
        => Ok(await sync.SyncAsync(ct));

    [HttpGet("sync")]
    public ActionResult<SyncStatus> SyncStatus() => Ok(sync.Current);
}
