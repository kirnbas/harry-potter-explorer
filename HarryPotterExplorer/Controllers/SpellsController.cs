using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

[Route("spells")]
public class SpellsController(ICatalogService catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? category, CancellationToken ct)
    {
        var page = await catalog.SearchSpellsAsync(search, category, 1, 200, ct);
        var facets = await catalog.GetFacetsAsync(ct);

        return View(new SpellsIndexViewModel(page, facets.SpellCategories, search, category));
    }

    /// <summary>HTML fragment for the live-filtered spell list. Same partial as the first render.</summary>
    [HttpGet("rows")]
    public async Task<IActionResult> Rows(
        string? search, string? category, int page = 1, int pageSize = 200, CancellationToken ct = default)
    {
        var result = await catalog.SearchSpellsAsync(search, category, page, pageSize, ct);

        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();

        return PartialView("_SpellRows", result.Items);
    }
}
