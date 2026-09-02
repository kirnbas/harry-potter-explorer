using System.Diagnostics;
using HarryPotterExplorer.Models;
using HarryPotterExplorer.Services;
using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

public class HomeController(ICatalogService catalog) : Controller
{
    /// <summary>
    /// Faces most visitors will recognise. Matched by name rather than by upstream id,
    /// because the ids are opaque GUIDs and the names are the stable part of that API.
    /// </summary>
    private static readonly string[] FeaturedNames =
    [
        "Harry Potter", "Hermione Granger", "Ron Weasley", "Albus Dumbledore",
        "Severus Snape", "Luna Lovegood", "Draco Malfoy", "Rubeus Hagrid",
        "Minerva McGonagall", "Sirius Black", "Neville Longbottom", "Ginny Weasley"
    ];

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var houses = await catalog.GetHousesAsync(ct);
        var stats = await catalog.GetLiveStatsAsync(ct);
        var featured = await catalog.GetCharactersByNamesAsync(FeaturedNames, ct);
        var artifacts = await catalog.GetArtifactsAsync(null, "Deathly Hallow", ct);

        return View(new HomeViewModel(houses, stats, featured, artifacts));
    }

    [Route("about")]
    public IActionResult About() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        var status = code ?? 500;

        var (title, message) = status switch
        {
            404 => ("This corridor does not exist",
                    "The staircase moved. Nothing here matches that address."),
            403 => ("The door will not open",
                    "You do not have permission to be in this part of the castle."),
            _ => ("Something went wrong in the dungeons",
                  "An unexpected error occurred. The house-elves have been notified.")
        };

        Response.StatusCode = status;

        return View(new ErrorViewModel(
            status, title, message,
            Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }
}
