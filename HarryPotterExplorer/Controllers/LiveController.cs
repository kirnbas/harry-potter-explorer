using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

[Route("live")]
public class LiveController(ICatalogService catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new LiveViewModel(await catalog.GetLiveStatsAsync(ct)));
}
