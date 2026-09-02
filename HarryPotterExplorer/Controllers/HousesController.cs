using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

[Route("houses")]
public class HousesController(ICatalogService catalog, IHouseCatalog houses) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new HousesIndexViewModel(await catalog.GetHousesAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var house = houses.Find(slug);

        if (house is null)
        {
            return RedirectToAction(nameof(HomeController.Error), "Home", new { code = 404 });
        }

        var all = await catalog.GetHousesAsync(ct);
        var count = all.FirstOrDefault(h => h.House.Slug == house.Slug)?.MemberCount ?? 0;
        var members = await catalog.GetHouseMembersAsync(house.Name, 12, ct);

        return View(new HouseDetailViewModel(house, count, members));
    }
}
