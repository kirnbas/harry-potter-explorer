using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

[Route("artifacts")]
public class ArtifactsController(ICatalogService catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? category, CancellationToken ct)
    {
        var artifacts = await catalog.GetArtifactsAsync(search, category, ct);
        var facets = await catalog.GetFacetsAsync(ct);

        return View(new ArtifactsIndexViewModel(
            artifacts, facets.ArtifactCategories, search, category));
    }
}
