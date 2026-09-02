using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

[Route("characters")]
public class CharactersController(
    ICatalogService catalog,
    IHouseCatalog houses,
    ICatalogSyncCoordinator sync) : Controller
{
    /// <summary>
    /// The first page is rendered on the server so the catalogue is usable (and indexable)
    /// without JavaScript; the infinite scroll then continues from page 2 via /api/characters.
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search, string? house, string? species, string? role, string? status,
        string sort = "name", CancellationToken ct = default)
    {
        var query = new CharacterQuery
        {
            Search = search,
            House = house,
            Species = species,
            Role = role,
            Status = status,
            Sort = sort,
            Page = 1,
            PageSize = 24
        };

        var page = await catalog.SearchCharactersAsync(query, ct);
        var facets = await catalog.GetFacetsAsync(ct);

        return View(new CharactersIndexViewModel(query, page, facets, sync.Current));
    }

    /// <summary>
    /// Page 2 and beyond of the infinite scroll, rendered as HTML rather than JSON.
    /// The card markup then has exactly one definition (_CharacterCard.cshtml) instead of
    /// a Razor version and a near-identical JavaScript template that drift apart.
    /// Paging metadata rides along in response headers.
    /// </summary>
    [HttpGet("cards")]
    public async Task<IActionResult> Cards(
        string? search, string? house, string? species, string? role, string? status,
        string sort = "name", int page = 2, int pageSize = 24, CancellationToken ct = default)
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
            PageSize = pageSize
        }, ct);

        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        Response.Headers["X-Has-More"] = result.HasMore ? "true" : "false";
        Response.Headers["X-Page"] = result.Page.ToString();

        return PartialView("_CharacterCards", result.Items);
    }

    /// <summary>Renders the visitor's locally stored collection into the same cards.</summary>
    [HttpPost("cards/by-ids")]
    public async Task<IActionResult> CardsByIds([FromBody] string[] ids, CancellationToken ct)
    {
        var trimmed = (ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Take(300)
            .ToList();

        var cards = await catalog.GetCharactersByIdsAsync(trimmed, ct);

        Response.Headers["X-Total-Count"] = cards.Count.ToString();

        return PartialView("_CharacterCards", cards);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var character = await catalog.GetCharacterAsync(id, ct);

        if (character is null)
        {
            return RedirectToAction(nameof(HomeController.Error), "Home", new { code = 404 });
        }

        var house = houses.Find(character.House);

        var houseMates = character.House is null
            ? []
            : (await catalog.GetHouseMembersAsync(character.House, 7, ct))
                .Where(c => c.Id != character.Id)
                .Take(6)
                .ToList();

        return View(new CharacterDetailViewModel(character, house, houseMates));
    }
}
