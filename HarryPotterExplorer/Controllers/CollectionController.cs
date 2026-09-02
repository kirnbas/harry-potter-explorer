using Microsoft.AspNetCore.Mvc;

namespace HarryPotterExplorer.Controllers;

/// <summary>
/// The visitor's own Chocolate Frog Card album. The page ships empty on purpose: the list
/// of ids lives in localStorage and is hydrated in the browser through /api/characters/by-ids,
/// so a collection is never tied to an account and never leaves the device as a list.
/// </summary>
[Route("collection")]
public class CollectionController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
